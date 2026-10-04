$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testPath = Join-Path $repoRoot 'dist\tests'
New-Item -ItemType Directory -Force -Path $testPath | Out-Null
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpfPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$exePath = Join-Path $testPath 'SettingsUsabilityTests.exe'
$buildLog = Join-Path $testPath 'settings-build.log'
$runLog = Join-Path $testPath 'settings-run.log'

& $compilerPath /nologo /codepage:65001 /target:exe /main:SettingsUsabilityTests `
    /out:$exePath /reference:System.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    /reference:"$wpfPath\UIAutomationClient.dll" `
    /reference:"$wpfPath\UIAutomationTypes.dll" /reference:"$wpfPath\WindowsBase.dll" `
    /resource:"$repoRoot\assets\system-icons-sheet.png",OrbitWheel.SystemIcons `
    "$repoRoot\OrbitWheelLite.cs" "$repoRoot\tests\SettingsUsabilityTests.cs" *> $buildLog
$buildExit = $LASTEXITCODE
Get-Content -LiteralPath $buildLog -TotalCount 60
if ($buildExit -ne 0) { throw "Settings regression harness build failed: $buildExit" }

$ErrorActionPreference = 'Continue'
try {
    & $exePath *> $runLog
    $testExit = $LASTEXITCODE
} finally { $ErrorActionPreference = 'Stop' }
Get-Content -LiteralPath $runLog -TotalCount 90
if ($testExit -ne 0) { throw "Settings usability regression failed: $testExit. See $runLog" }
