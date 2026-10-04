param([string]$Version)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $repoRoot 'build.ps1')

$distPath = Join-Path $repoRoot 'dist'
$exePath = Join-Path $distPath 'OrbitWheel.exe'
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($exePath).Version
$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
if ($fileVersion.ProductName -ne 'OrbitWheel') { throw 'Unexpected executable product name.' }

if ([string]::IsNullOrWhiteSpace($Version)) {
    $parts = @($assemblyVersion.ToString().Split('.'))
    while ($parts.Count -gt 2 -and $parts[-1] -eq '0') { $parts = $parts[0..($parts.Count - 2)] }
    $Version = $parts -join '.'
}

if ($Version -notmatch '^(?<base>\d+\.\d+(?:\.\d+)?)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw 'Use a two- or three-part product version, optionally followed by a prerelease suffix.'
}
$baseVersion = $Matches['base']
$versionParts = @($baseVersion.Split('.'))
while ($versionParts.Count -lt 4) { $versionParts += '0' }
$expectedVersion = [version]($versionParts -join '.')
if ($assemblyVersion -ne $expectedVersion -or [version]$fileVersion.FileVersion -ne $expectedVersion) {
    throw "Tag/product version $Version does not match executable version $assemblyVersion."
}

$files = @('OrbitWheel.exe', 'README.md', 'RELEASE_NOTES.md', 'LICENSE')
$filePaths = @($files | ForEach-Object { Join-Path $distPath $_ })
foreach ($filePath in $filePaths) {
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf) -or (Get-Item -LiteralPath $filePath).Length -eq 0) {
        throw "Missing or empty package file: $filePath"
    }
}

$notesHeader = (Get-Content -LiteralPath (Join-Path $distPath 'RELEASE_NOTES.md') -TotalCount 1).Trim()
if ($notesHeader -notin @("# OrbitWheel $Version", "# OrbitWheel $baseVersion")) {
    throw 'Release notes version does not match the product version.'
}
$readmeText = Get-Content -LiteralPath (Join-Path $distPath 'README.md') -Raw
$readmeVersions = @([regex]::Matches($readmeText, 'OrbitWheel-(?<version>[0-9][0-9A-Za-z.-]*)\.zip') |
    ForEach-Object { $_.Groups['version'].Value })
if ($readmeVersions.Count -eq 0 -or @($readmeVersions | Where-Object { $_ -notin @($Version, $baseVersion) }).Count -gt 0) {
    throw 'README package versions do not match the product version.'
}

$releasePath = Join-Path $distPath 'release'
New-Item -ItemType Directory -Force -Path $releasePath | Out-Null
$zipPath = Join-Path $releasePath "OrbitWheel-$Version.zip"
Compress-Archive -LiteralPath $filePaths -DestinationPath $zipPath -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
    if ($entryNames.Count -ne $files.Count -or @(Compare-Object $files $entryNames).Count -ne 0) {
        throw 'ZIP must contain exactly the four distribution files at its root.'
    }
    $entryStream = $archive.GetEntry('OrbitWheel.exe').Open()
    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $entryHash = [BitConverter]::ToString($hasher.ComputeHash($entryStream)).Replace('-', '')
        if ($entryHash -ne (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash) {
            throw 'Packaged executable differs from the verified build.'
        }
    } finally { $hasher.Dispose(); $entryStream.Dispose() }
} finally { $archive.Dispose() }

$checksumPath = Join-Path $releasePath 'SHA256SUMS.txt'
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($checksumPath, "$zipHash  $([IO.Path]::GetFileName($zipPath))`n", [Text.Encoding]::ASCII)
$notesPath = Join-Path $releasePath 'RELEASE_NOTES.md'
Copy-Item -LiteralPath (Join-Path $distPath 'RELEASE_NOTES.md') -Destination $notesPath -Force
Write-Host "Verified package: $zipPath"

[pscustomobject]@{
    Version = $Version
    ZipPath = $zipPath
    ChecksumPath = $checksumPath
    NotesPath = $notesPath
}
