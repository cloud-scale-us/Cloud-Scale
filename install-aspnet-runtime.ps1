# Copy ASP.NET Core runtime to system dotnet and restart service
$src = 'C:\dotnet\shared\Microsoft.AspNetCore.App'
$dst = 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App'

if (!(Test-Path $dst)) {
    New-Item -ItemType Directory -Path $dst -Force | Out-Null
}
Copy-Item -Path "$src\8.0.23" -Destination $dst -Recurse -Force
Write-Host "ASP.NET Core 8.0.23 runtime copied to system dotnet."

# Restart service
net stop ScaleStreamerService 2>&1
Start-Sleep -Seconds 2

# Deploy latest DLLs
$svcDir = 'C:\Program Files\Scale Streamer\Service'
$cfgDir = 'C:\Program Files\Scale Streamer\Config'
$buildBase = 'C:\Users\Windfield\Cloud-Scale\win-scale\src-v2'

Copy-Item "$buildBase\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" $svcDir -Force
Copy-Item "$buildBase\ScaleStreamer.Service\bin\Release\net8.0\ScaleStreamer.Service.dll" $svcDir -Force
Copy-Item "$buildBase\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" $cfgDir -Force
Copy-Item "$buildBase\ScaleStreamer.Config\bin\Release\net8.0-windows\ScaleStreamer.Config.dll" $cfgDir -Force

# Also copy SharpOnvif and CoreWCF DLLs to service dir
$svcBin = "$buildBase\ScaleStreamer.Service\bin\Release\net8.0"
Get-ChildItem $svcBin -Filter "*.dll" | ForEach-Object {
    Copy-Item $_.FullName $svcDir -Force
}
Write-Host "All service DLLs deployed."

net start ScaleStreamerService
Start-Sleep -Seconds 2
Get-Service ScaleStreamerService | Select-Object Name, Status
