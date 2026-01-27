$git = 'C:\Program Files\Git\bin\git.exe'
cd 'C:\Users\Windfield\Cloud-Scale'

# Check git root
Write-Host "Git root: $(& $git rev-parse --show-toplevel)"

# Find large tracked files
Write-Host "`nLarge tracked files (>50MB):"
$tracked = & $git ls-files
foreach ($f in $tracked) {
    if (Test-Path $f) {
        $item = Get-Item $f -ErrorAction SilentlyContinue
        if ($item -and $item.Length -gt 50MB) {
            Write-Host ("  {0:N1} MB  {1}" -f ($item.Length/1MB), $f)
            & $git rm --cached $f
        }
    }
}

# Also remove ffmpeg binaries and mediamtx binaries
Write-Host "`nRemoving binary directories from tracking..."
$patterns = @(
    'ffmpeg/ffmpeg.zip',
    'ffmpeg/ffmpeg-master-latest-win64-gpl/bin/*.exe',
    'mediamtx/mediamtx.exe',
    'mediamtx.zip'
)
foreach ($p in $patterns) {
    $matched = & $git ls-files $p
    if ($matched) {
        foreach ($m in $matched) {
            Write-Host "  Removing: $m"
            & $git rm --cached $m
        }
    }
}

# Update .gitignore at Cloud-Scale root
$ignoreFile = 'C:\Users\Windfield\Cloud-Scale\.gitignore'
$entries = @(
    '# Large binaries',
    'ffmpeg/ffmpeg.zip',
    'ffmpeg/ffmpeg-master-latest-win64-gpl/bin/',
    'mediamtx/mediamtx.exe',
    'mediamtx.zip',
    '*.msi',
    'win-scale/installer/bin/',
    'win-scale/src-v2/*/bin/',
    'win-scale/src-v2/*/obj/',
    'win-scale/find-large-files.ps1',
    'win-scale/fix-large-files.ps1'
)

if (Test-Path $ignoreFile) {
    $existing = Get-Content $ignoreFile
} else {
    $existing = @()
}

$toAdd = $entries | Where-Object { $_ -notin $existing }
if ($toAdd) {
    Add-Content $ignoreFile ("`n" + ($toAdd -join "`n"))
    Write-Host "`nAdded to .gitignore: $($toAdd -join ', ')"
}

# Delete old tag
& $git tag -d v5.2.0 2>&1

# Stage and amend
& $git add -A
& $git commit --amend --no-edit

# Re-tag
& $git tag v5.2.0

# Push
Write-Host "`nPushing..."
& $git push origin main --force
& $git push origin v5.2.0

Write-Host "`nDone!"
