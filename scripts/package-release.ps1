param([string]$Version)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $repoRoot 'build.ps1')
$distPath = Join-Path $repoRoot 'dist'
$exePath = Join-Path $distPath 'OrbitWheel.exe'
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($exePath).Version
if ([string]::IsNullOrWhiteSpace($Version)) {
    $parts = @($assemblyVersion.ToString().Split('.'))
    while ($parts.Count -gt 2 -and $parts[-1] -eq '0') { $parts = $parts[0..($parts.Count - 2)] }
    $Version = $parts -join '.'
}
if ($Version -notmatch '^(?<base>\d+\.\d+(?:\.\d+)?)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw 'Use a two- or three-part product version, optionally followed by a prerelease suffix.'
}
$baseVersion = $Matches['base']; $parts = @($baseVersion.Split('.'))
while ($parts.Count -lt 4) { $parts += '0' }
$expected = [version]($parts -join '.')
if ($assemblyVersion -ne $expected -or [version]([Diagnostics.FileVersionInfo]::GetVersionInfo($exePath).FileVersion) -ne $expected) {
    throw "Tag/product version $Version does not match executable version $assemblyVersion."
}
$notes = (Get-Content (Join-Path $repoRoot 'RELEASE_NOTES.md') -Encoding UTF8 -TotalCount 1).Trim()
if ($notes -notin @("# OrbitWheel $Version", "# OrbitWheel $baseVersion")) { throw 'Release notes version mismatch.' }
$readme = Get-Content (Join-Path $repoRoot 'README.md') -Encoding UTF8 -Raw
$mentions = @([regex]::Matches($readme, 'OrbitWheel-(?<version>\d+\.\d+(?:\.\d+)?)-(?:self-contained|framework-dependent)\.zip') |
    ForEach-Object { $_.Groups['version'].Value })
if ($mentions.Count -eq 0 -or @($mentions | Where-Object { $_ -notin @($Version,$baseVersion) }).Count) { throw 'README package version mismatch.' }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$releasePath = Join-Path $distPath 'release'
New-Item -ItemType Directory -Force $releasePath | Out-Null
$packages = @(); $checksums = @()
foreach ($mode in @('self-contained','framework-dependent')) {
    $settings = & (Join-Path $PSScriptRoot 'build-settings.ps1') -Mode $mode
    if ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $settings.Path 'OrbitWheel.Settings.dll')).Version -ne $expected) {
        throw 'WinForms/WinUI version mismatch.'
    }
    $stage = Join-Path $distPath ('package-staging\' + $mode + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $stage | Out-Null
    foreach ($file in @('README.md','en.md','CONTRIBUTING.md','RELEASE_NOTES.md','LICENSE')) { Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination $stage }
    foreach ($file in @('assets\settings-fluent.jpg','docs\winui-deployment.md')) {
        $destination = Join-Path $stage $file
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination $destination
    }
    Copy-Item -LiteralPath $exePath -Destination $stage
    Copy-Item -LiteralPath $settings.Path -Destination (Join-Path $stage 'Settings') -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\winui-deployment.md') -Destination (Join-Path $stage 'DEPLOYMENT.md')
    $files = @(Get-ChildItem -LiteralPath $stage -Recurse -File)
    $zipPath = Join-Path $releasePath "OrbitWheel-$Version-$mode.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath }
    $created = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($stage.Length + 1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($created, $file.FullName, $relative,
                [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $created.Dispose() }
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entries = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') })
        if ($entries.Count -ne $files.Count) { throw 'ZIP file count mismatch.' }
        foreach ($file in $files) {
            $name = $file.FullName.Substring($stage.Length + 1).Replace('\','/')
            $entry = $archive.GetEntry($name)
            if (-not $entry -or $entry.Length -ne $file.Length) { throw "Missing or truncated ZIP file: $name" }
            $stream = $entry.Open(); $hash = [Security.Cryptography.SHA256]::Create()
            try {
                $actual = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','')
                if ($actual -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw "ZIP hash mismatch: $name" }
            } finally { $stream.Dispose(); $hash.Dispose() }
        }
    } finally { $archive.Dispose() }
    $checksums += (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($zipPath)
    $packages += [pscustomobject]@{ Mode = $mode; ZipPath = $zipPath; SettingsPath = (Join-Path $stage 'Settings\OrbitWheel.Settings.exe');
        FileCount = $files.Count; ZipBytes = (Get-Item $zipPath).Length; UnpackedBytes = ($files | Measure-Object Length -Sum).Sum }
    Write-Host "Verified $mode package: $zipPath"
}
$checksumPath = Join-Path $releasePath 'SHA256SUMS.txt'
[IO.File]::WriteAllText($checksumPath, (($checksums -join "`n") + "`n"), [Text.Encoding]::ASCII)
$sizePath = Join-Path $releasePath 'PACKAGE-SIZES.json'
$sizeReport = @($packages | Select-Object Mode,FileCount,ZipBytes,UnpackedBytes)
[IO.File]::WriteAllText($sizePath, ($sizeReport | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
$notesPath = Join-Path $releasePath 'RELEASE_NOTES.md'
$history = Get-Content (Join-Path $repoRoot 'RELEASE_NOTES.md') -Encoding UTF8 -Raw
# The leading heading belongs to the current release; stop at the next version heading.
$versionHeading = '(?m)^#{1,2}\s+(?:OrbitWheel\s+)?\d+\.\d+(?:\.\d+)?(?:-[\w.-]+)?\s*\r?$'
$headings = [regex]::Matches($history, $versionHeading)
if ($headings.Count -gt 1) { $history = $history.Substring(0, $headings[1].Index) }
if ([regex]::Matches($history, $versionHeading).Count -ne 1) { throw 'Release body must describe exactly one version.' }
[IO.File]::WriteAllText($notesPath, ($history.TrimEnd() + "`n"), (New-Object Text.UTF8Encoding($false)))
[pscustomobject]@{ Version = $Version; ZipPath = $packages[0].ZipPath; SelfContainedZip = $packages[0].ZipPath;
    SharedZip = $packages[1].ZipPath; SelfContainedSettingsPath = $packages[0].SettingsPath;
    SharedSettingsPath = $packages[1].SettingsPath; ChecksumPath = $checksumPath; NotesPath = $notesPath; SizePath = $sizePath }
