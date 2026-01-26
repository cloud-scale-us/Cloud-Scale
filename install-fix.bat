@echo off
:: Run this file as Administrator (right-click -> Run as administrator)

echo Stopping Scale Streamer Service...
net stop ScaleStreamerService
timeout /t 2 /nobreak >nul

echo Copying updated DLLs...
copy /Y "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "C:\Program Files\Scale Streamer\Service\"
copy /Y "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\Release\net8.0\ScaleStreamer.Service.dll" "C:\Program Files\Scale Streamer\Service\"
copy /Y "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "C:\Program Files\Scale Streamer\Config\"
copy /Y "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Config\bin\Release\net8.0-windows\ScaleStreamer.Config.dll" "C:\Program Files\Scale Streamer\Config\"

echo Starting Scale Streamer Service...
net start ScaleStreamerService

echo.
echo Done! Press any key to close...
pause >nul
