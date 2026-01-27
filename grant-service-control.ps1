# Grant-Service-Control.ps1
# Run this ONCE as Administrator to allow your user to start/stop ScaleStreamerService without UAC
# After running this, you can use: net start ScaleStreamerService / net stop ScaleStreamerService without elevation

$serviceName = "ScaleStreamerService"

# Get current user SID
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$userSid = $currentUser.User.Value
$userName = $currentUser.Name

Write-Host "Granting service control permissions to: $userName" -ForegroundColor Yellow
Write-Host "User SID: $userSid" -ForegroundColor Gray

# Get current SDDL
$currentSddl = (sc.exe sdshow $serviceName | Where-Object { $_ -match "^D:" }) -join ""

if (-not $currentSddl) {
    Write-Host "ERROR: Could not get current security descriptor for $serviceName" -ForegroundColor Red
    Write-Host "Make sure the service exists and you're running as Administrator" -ForegroundColor Red
    exit 1
}

Write-Host "Current SDDL: $currentSddl" -ForegroundColor Gray

# Create the ACE (Access Control Entry) for the user
# RP = SERVICE_START (0x0010)
# WP = SERVICE_STOP (0x0020)
# LC = SERVICE_QUERY_STATUS (0x0004)
# CC = SERVICE_QUERY_CONFIG (0x0001)
# The format is (A;;RPWPLCCC;;;SID) - Allow Start, Stop, Query Status, Query Config
$newAce = "(A;;RPWPLCCC;;;$userSid)"

# Insert the new ACE into the DACL (after D:)
# The SDDL format is D:(...)(...)S:(...) where D: is DACL and S: is SACL
if ($currentSddl -match "^D:(.*)$") {
    $newSddl = "D:" + $newAce + $Matches[1]
}
else {
    Write-Host "ERROR: Could not parse SDDL" -ForegroundColor Red
    exit 1
}

Write-Host "New SDDL: $newSddl" -ForegroundColor Gray

# Apply the new security descriptor
Write-Host "Applying new security descriptor..." -ForegroundColor Yellow
$result = sc.exe sdset $serviceName $newSddl 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "SUCCESS! You can now start/stop $serviceName without elevation." -ForegroundColor Green
    Write-Host ""
    Write-Host "Commands you can now use WITHOUT admin:" -ForegroundColor Cyan
    Write-Host "  net stop $serviceName" -ForegroundColor White
    Write-Host "  net start $serviceName" -ForegroundColor White
    Write-Host "  sc.exe query $serviceName" -ForegroundColor White
}
else {
    Write-Host "ERROR: Failed to set security descriptor" -ForegroundColor Red
    Write-Host $result -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure you're running this script as Administrator!" -ForegroundColor Yellow
}
