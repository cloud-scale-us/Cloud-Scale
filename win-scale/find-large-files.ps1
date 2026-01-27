cd 'C:\Users\Windfield\Cloud-Scale\win-scale'
$git = 'C:\Program Files\Git\bin\git.exe'
$files = & $git ls-files
foreach ($f in $files) {
    if (Test-Path $f) {
        $item = Get-Item $f -ErrorAction SilentlyContinue
        if ($item -and $item.Length -gt 10MB) {
            Write-Host ('{0:N0} MB  {1}' -f ($item.Length/1MB), $f)
        }
    }
}
