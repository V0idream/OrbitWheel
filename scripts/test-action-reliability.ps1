param([switch]$IncludeTray, [switch]$TrayOnly)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testPath = Join-Path $repoRoot 'dist\tests'
New-Item -ItemType Directory -Force -Path $testPath | Out-Null
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpfPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$exePath = Join-Path $testPath 'ActionReliabilityTests.exe'
$buildLog = Join-Path $testPath 'build.log'
$runLog = Join-Path $testPath 'run.log'

& $compilerPath /nologo /codepage:65001 /target:exe /main:ActionReliabilityTests `
    /out:$exePath /reference:System.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    /reference:"$wpfPath\UIAutomationClient.dll" `
    /reference:"$wpfPath\UIAutomationTypes.dll" /reference:"$wpfPath\WindowsBase.dll" `
    /resource:"$repoRoot\assets\system-icons-sheet.png",OrbitWheel.SystemIcons `
    "$repoRoot\OrbitWheelLite.cs" "$repoRoot\tests\ActionReliabilityTests.cs" *> $buildLog
$buildExit = $LASTEXITCODE
Get-Content -LiteralPath $buildLog -TotalCount 60
if ($buildExit -ne 0) { throw "Regression harness build failed: $buildExit" }

# The production launch path must receive a GUI executable, not a console harness
# whose extra console window can win the production window-size scoring on runners.
$fixturePath = Join-Path $testPath 'OrbitReliabilityTarget.exe'
& $compilerPath /nologo /codepage:65001 /target:winexe /main:ActionReliabilityTests `
    /out:$fixturePath /reference:System.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    /reference:"$wpfPath\UIAutomationClient.dll" `
    /reference:"$wpfPath\UIAutomationTypes.dll" /reference:"$wpfPath\WindowsBase.dll" `
    /resource:"$repoRoot\assets\system-icons-sheet.png",OrbitWheel.SystemIcons `
    "$repoRoot\OrbitWheelLite.cs" "$repoRoot\tests\ActionReliabilityTests.cs" *> $buildLog
$buildExit = $LASTEXITCODE
Get-Content -LiteralPath $buildLog -TotalCount 60
if ($buildExit -ne 0) { throw "GUI fixture build failed: $buildExit" }

$testArguments = @()
if ($IncludeTray) { $testArguments += '--include-tray' }
if ($TrayOnly) { $testArguments += '--tray-only' }
$ErrorActionPreference = 'Continue' # Preserve native stderr and the exit code under Windows PowerShell 5.1.
try {
    & $exePath @testArguments *> $runLog
    $testExit = $LASTEXITCODE
} finally { $ErrorActionPreference = 'Stop' }
Get-Content -LiteralPath $runLog -TotalCount 100
if ($testExit -ne 0) { throw "Action reliability regression failed: $testExit. See $runLog" }
