param([ValidateSet('self-contained','framework-dependent')][string]$Mode = 'self-contained')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = if ($env:ORBITWHEEL_DOTNET) { $env:ORBITWHEEL_DOTNET } else { (Get-Command dotnet -ErrorAction Stop).Source }
$publishPath = Join-Path $repoRoot ('dist\settings-builds\' + $Mode + '-' + [guid]::NewGuid().ToString('N'))
$logPath = Join-Path $repoRoot ('dist\settings-' + $Mode + '.log')
New-Item -ItemType Directory -Force $publishPath | Out-Null
$selfContained = if ($Mode -eq 'self-contained') { 'true' } else { 'false' }
& $dotnet publish (Join-Path $repoRoot 'src\OrbitWheel.Settings\OrbitWheel.Settings.csproj') -c Release -r win-x64 `
    --self-contained $selfContained "-p:WindowsAppSDKSelfContained=$selfContained" -p:RestoreLockedMode=true -o $publishPath *> $logPath
$result = $LASTEXITCODE
Get-Content -LiteralPath $logPath -Tail 18 | ForEach-Object { Write-Host $_ }
if ($result -ne 0) { throw "WinUI settings build failed ($Mode): $result. See $logPath" }
foreach ($name in @('OrbitWheel.Settings.exe','OrbitWheel.Settings.dll','OrbitWheel.Settings.runtimeconfig.json','OrbitWheel.Settings.pri')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishPath $name))) { throw "Missing settings file: $name" }
}
[pscustomobject]@{ Mode = $Mode; Path = $publishPath }
