$msi = Get-ChildItem "C:\Users\Windfield\Cloud-Scale\win-scale\installer\bin\ScaleStreamer-v4.2.0-*.msi" | Select-Object -First 1
$log = "C:\Users\Windfield\Cloud-Scale\install-test.log"

Write-Host "Testing installation with verbose logging..." -ForegroundColor Cyan
Write-Host "MSI: $($msi.FullName)" -ForegroundColor Yellow
Write-Host "Log: $log" -ForegroundColor Yellow

$args = "/i `"$($msi.FullName)`" /l*v `"$log`""
Write-Host "Running: msiexec $args" -ForegroundColor Gray

Start-Process msiexec -ArgumentList $args -Wait

Write-Host ""
Write-Host "Installation completed. Checking log for errors..." -ForegroundColor Yellow

# Check for error in log
$logContent = Get-Content $log -Tail 100
$errors = $logContent | Select-String -Pattern "error|failed|terminated" -Context 2,2

if ($errors) {
    Write-Host "Found errors in log:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
} else {
    Write-Host "No obvious errors found in log tail" -ForegroundColor Green
}

# Check service status
Write-Host ""
Write-Host "Service status:" -ForegroundColor Cyan
Get-Service ScaleStreamerService -ErrorAction SilentlyContinue | Format-Table Name, Status -AutoSize
