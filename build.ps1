$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF"
& $csc /nologo /codepage:65001 /target:winexe /optimize+ `
    /win32icon:"$root\OrbitWheel.ico" `
    /out:"$dist\OrbitWheel.exe" `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll `
    /reference:"$wpf\UIAutomationClient.dll" `
    /reference:"$wpf\UIAutomationTypes.dll" `
    /reference:"$wpf\WindowsBase.dll" `
    /resource:"$root\assets\system-icons-sheet.png",OrbitWheel.SystemIcons `
    "$root\OrbitWheelLite.cs" "$root\shared\Models.cs" "$root\shared\ActionNames.cs" "$root\shared\ConfigStore.cs" "$root\shared\RuntimeState.cs"

if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }

Copy-Item "$root\README.md" "$dist\README.md" -Force
Copy-Item "$root\README.en.md" "$dist\README.en.md" -Force
Copy-Item "$root\RELEASE_NOTES.md" "$dist\RELEASE_NOTES.md" -Force
Copy-Item "$root\LICENSE" "$dist\LICENSE" -Force
Write-Host "Built: $dist\OrbitWheel.exe"
