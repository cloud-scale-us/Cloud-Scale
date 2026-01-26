net stop ScaleStreamerService
Start-Sleep 3
net start ScaleStreamerService
Start-Sleep 2
Get-Service ScaleStreamerService | Select-Object Name, Status
