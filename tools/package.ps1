param([switch]$Build, [string]$ReferencePath, [string]$BaseRef)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($Build) {
    & (Join-Path $root 'build.ps1') -RunTests -ReferencePath $ReferencePath
    if (!$?) { throw 'Build and tests failed.' }
}
$exe = Join-Path $root 'bin\Release\NihongoDeskMemo.exe'
$version = [Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString(3)
$source = [IO.File]::ReadAllText((Join-Path $root 'src\VersionInfo.cs'))
if ($source -notmatch ('Number\s*=\s*"' + [regex]::Escape($version) + '"')) { throw 'Build/source version mismatch.' }
$changelog = [IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md')).Replace("`r`n", "`n")
$entry = [regex]::Match($changelog, '(?ms)^## \[' + [regex]::Escape($version) + '\] - [^\n]+\n(.*?)(?=^## |\z)')
if (!$entry.Success -or $entry.Groups[1].Value.Trim().Length -lt 20) { throw 'Version has no release notes.' }
if ($BaseRef) {
    $changed = @(& git -C $root diff --name-only "$BaseRef...HEAD" -- src)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot compare the base ref.' }
    $versionFile = & git -C $root ls-tree --name-only $BaseRef -- src/VersionInfo.cs
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect base version.' }
    if ($versionFile -and $changed.Count -gt 0) {
        $prior = & git -C $root show "${BaseRef}:src/VersionInfo.cs"
        if ($LASTEXITCODE -ne 0 -or ($prior -join "`n") -notmatch 'Number\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"') { throw 'Cannot read base version.' }
        if ([version]$version -le [version]$Matches[1]) { throw 'Source changes require a newer version.' }
    }
}
$commit = & git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify build commit.' }
$dirty = @(& git -C $root status --porcelain)
$dist = Join-Path $root 'dist'
$stage = Join-Path $root ('bin\package-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($stage)
[void][IO.Directory]::CreateDirectory($dist)
$metadata = [ordered]@{ version = $version; commit = $commit; dirty = ($dirty.Count -gt 0); builtUtc = [DateTime]::UtcNow.ToString('o') }
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'release.json') -Encoding UTF8
Copy-Item -LiteralPath $exe -Destination (Join-Path $stage 'NihongoDeskMemo.exe')
Copy-Item -LiteralPath (Join-Path $root 'README.md'),(Join-Path $root 'CHANGELOG.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'examples') -Destination $stage -Recurse
$name = "NihongoDeskMemo-v$version"
$zip = Join-Path $dist ($name + '.zip')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Copy-Item -LiteralPath $exe -Destination (Join-Path $dist ($name + '.exe'))
Copy-Item -LiteralPath (Join-Path $stage 'release.json') -Destination $dist
$entry.Groups[1].Value.Trim() | Set-Content -LiteralPath (Join-Path $dist 'release-notes.md') -Encoding UTF8
$sums = foreach ($path in @($zip, (Join-Path $dist ($name + '.exe')))) {
    $stream = [IO.File]::OpenRead($path)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($path) }
    finally { $stream.Dispose(); $hasher.Dispose() }
}
$sums | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
Write-Host "Packaged v$version at $dist (dirty=$($metadata.dirty))"
