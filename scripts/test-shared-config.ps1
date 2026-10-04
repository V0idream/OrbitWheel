param([switch]$WinUI, [switch]$RealHotkey, [switch]$Desktop, [switch]$LiveHost, [string]$SettingsPath = 'dist\prototype\self-contained\OrbitWheel.Settings.exe')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$evidencePath = Join-Path $repoRoot ('dist\sync-tests\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $evidencePath | Out-Null
if ($LiveHost -and -not $WinUI) { throw '-LiveHost requires -WinUI.' }
$exePath = Join-Path $evidencePath $(if ($LiveHost) { 'OrbitWheel.exe' } else { 'SharedConfigTests.exe' })
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
& $compiler /nologo /codepage:65001 /target:exe /main:SharedConfigTests /out:$exePath `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    /reference:"$wpf\UIAutomationClient.dll" /reference:"$wpf\UIAutomationTypes.dll" /reference:"$wpf\WindowsBase.dll" `
    /resource:"$repoRoot\assets\system-icons-sheet.png",OrbitWheel.SystemIcons `
    "$repoRoot\OrbitWheelLite.cs" "$repoRoot\shared\Models.cs" "$repoRoot\shared\ActionNames.cs" "$repoRoot\shared\ConfigStore.cs" "$repoRoot\shared\RuntimeState.cs" "$repoRoot\tests\SharedConfigTests.cs" *> (Join-Path $evidencePath 'build.log')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $evidencePath 'build.log') -TotalCount 30; throw 'Shared config harness build failed.' }
$oldConfig = $env:ORBITWHEEL_CONFIG_DIR
$oldSmoke = $env:ORBITWHEEL_UI_SMOKE
$oldDesktop = $env:ORBITWHEEL_DESKTOP_SMOKE
$peer = $null; $ui = $null
try {
    $env:ORBITWHEEL_CONFIG_DIR = $evidencePath
    if ($RealHotkey) { & $exePath '--real-hotkey' *> (Join-Path $evidencePath 'contract.log') }
    else { & $exePath *> (Join-Path $evidencePath 'contract.log') }
    $result = $LASTEXITCODE
    Get-Content (Join-Path $evidencePath 'contract.log') -TotalCount 35
    if ($result -ne 0) { throw 'Shared configuration regression failed.' }
    if ($WinUI) {
        $env:ORBITWHEEL_CONFIG_DIR = Join-Path $evidencePath 'ui'
        New-Item -ItemType Directory -Force $env:ORBITWHEEL_CONFIG_DIR | Out-Null
        $peerMode = if ($LiveHost) { '--live-peer' } else { '--peer' }
        $peer = Start-Process -FilePath $exePath -ArgumentList $peerMode -WindowStyle Hidden -PassThru
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while (-not (Test-Path (Join-Path $env:ORBITWHEEL_CONFIG_DIR 'host-ack.json'))) {
            if ($peer.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw 'WinForms peer did not start.' }
            Start-Sleep -Milliseconds 100
        }
        $env:ORBITWHEEL_UI_SMOKE = '1'
        $env:ORBITWHEEL_DESKTOP_SMOKE = if ($Desktop) { '1' } else { '0' }
        $settingsExecutable = if ([IO.Path]::IsPathRooted($SettingsPath)) { $SettingsPath } else { Join-Path $repoRoot $SettingsPath }
        $ui = Start-Process -FilePath $settingsExecutable -WindowStyle Hidden -PassThru
        $deadline = [DateTime]::UtcNow.AddSeconds(55)
        $reportPath = Join-Path $env:ORBITWHEEL_CONFIG_DIR 'ui-result.json'
        while (-not (Test-Path $reportPath)) {
            if ($ui.HasExited -or [DateTime]::UtcNow -gt $deadline) { throw "WinUI did not produce a result. Evidence: $evidencePath" }
            Start-Sleep -Milliseconds 200
        }
        $report = Get-Content $reportPath -Encoding UTF8 -Raw | ConvertFrom-Json
        if (-not $report.success) { throw $report.error }
        $report.steps | ForEach-Object { Write-Host "PASS $_" }
        Write-Host 'PASS real WinUI and WinForms processes synchronized through the production configuration contract.'
    }
} finally {
    if ($peer -and -not $peer.HasExited) {
        [IO.File]::WriteAllText((Join-Path $env:ORBITWHEEL_CONFIG_DIR 'stop-peer'), '')
        if (-not $peer.WaitForExit(5000)) { $peer.Kill() }
    }
    if ($ui -and -not $ui.HasExited -and -not $ui.WaitForExit(3000)) { $ui.Kill() }
    if ($ui) { $ui.Dispose() }; if ($peer) { $peer.Dispose() }
    $env:ORBITWHEEL_CONFIG_DIR = $oldConfig; $env:ORBITWHEEL_UI_SMOKE = $oldSmoke; $env:ORBITWHEEL_DESKTOP_SMOKE = $oldDesktop
}
Write-Host "Evidence: $evidencePath"
