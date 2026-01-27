$git = 'C:\Program Files\Git\bin\git.exe'
cd 'C:\Users\Windfield\Cloud-Scale\win-scale'

# Find all tracked files > 50MB
Write-Host "Finding large tracked files..."
$tracked = & $git ls-files
foreach ($f in $tracked) {
    if (Test-Path $f) {
        $item = Get-Item $f -ErrorAction SilentlyContinue
        if ($item -and $item.Length -gt 50MB) {
            Write-Host ("  {0:N1} MB  {1}" -f ($item.Length/1MB), $f)
        }
    }
}

# Remove known large files from tracking
Write-Host "`nRemoving large files from git tracking..."
$largePatterns = @('ffmpeg/ffmpeg.zip', 'ffmpeg/ffmpeg.exe', 'ffmpeg/ffprobe.exe', 'mediamtx/mediamtx.exe', 'mediamtx/mediamtx.zip', 'installer/bin/*.msi')
foreach ($pattern in $largePatterns) {
    $matches = & $git ls-files $pattern
    if ($matches) {
        Write-Host "  Removing: $matches"
        & $git rm --cached $matches 2>&1
    }
}

# Update .gitignore
Write-Host "`nUpdating .gitignore..."
$ignoreEntries = @(
    '',
    '# Large binary files',
    'ffmpeg/*.zip',
    'ffmpeg/*.exe',
    'mediamtx/*.zip',
    'mediamtx/*.exe',
    'installer/bin/*.msi',
    '*.zip',
    '*.msi'
)

$gitignore = if (Test-Path .gitignore) { Get-Content .gitignore } else { @() }
$newEntries = $ignoreEntries | Where-Object { $_ -notin $gitignore }
if ($newEntries) {
    Add-Content .gitignore ($newEntries -join "`n")
    Write-Host "  Added entries to .gitignore"
}

# Also remove bin/Release publish directories if tracked
$publishDirs = & $git ls-files 'src-v2/*/bin/'
if ($publishDirs) {
    Write-Host "`nRemoving tracked build output..."
    & $git rm --cached -r 'src-v2/*/bin/' 2>&1
}

# Delete old tag
Write-Host "`nDeleting old v5.2.0 tag..."
& $git tag -d v5.2.0 2>&1

# Check authorship before amending
Write-Host "`nChecking commit authorship..."
& $git log -1 --format='%an %ae'

# Amend commit
Write-Host "`nAmending commit..."
& $git add -A
& $git commit --amend --no-edit

# Re-create tag
Write-Host "`nRe-creating v5.2.0 tag..."
& $git tag v5.2.0

# Push
Write-Host "`nPushing to remote..."
& $git push origin main --force
& $git push origin v5.2.0

Write-Host "`nDone!"
