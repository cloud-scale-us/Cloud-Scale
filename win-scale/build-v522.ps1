$env:PATH = "C:\dotnet;$env:PATH"
Set-Location "C:\Users\Windfield\Cloud-Scale\win-scale"

Write-Host "=== Publishing Service (self-contained) ===" -ForegroundColor Yellow
C:\dotnet\dotnet publish src-v2\ScaleStreamer.Service\ScaleStreamer.Service.csproj -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { Write-Error "Service publish failed!"; exit 1 }

Write-Host "=== Publishing Config (self-contained) ===" -ForegroundColor Yellow
C:\dotnet\dotnet publish src-v2\ScaleStreamer.Config\ScaleStreamer.Config.csproj -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { Write-Error "Config publish failed!"; exit 1 }

Write-Host "=== Publishing Launcher (self-contained) ===" -ForegroundColor Yellow
C:\dotnet\dotnet publish src-v2\ScaleStreamer.Launcher\ScaleStreamer.Launcher.csproj -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { Write-Error "Launcher publish failed!"; exit 1 }

Write-Host "=== Publishing TestTool (self-contained) ===" -ForegroundColor Yellow
C:\dotnet\dotnet publish src-v2\ScaleStreamer.TestTool\ScaleStreamer.TestTool.csproj -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { Write-Error "TestTool publish failed!"; exit 1 }

Write-Host "All published successfully!" -ForegroundColor Green
