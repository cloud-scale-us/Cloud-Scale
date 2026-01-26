@echo off
echo Updating Scale Streamer Service...
echo.

echo Stopping service...
net stop ScaleStreamerService

echo.
echo Copying new DLLs...
copy /Y "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Common\bin\Release\net8.0\ScaleStreamer.Common.dll" "C:\Program Files\Scale Streamer\Service\"
copy /Y "C:\Users\Windfield\Cloud-Scale\win-scale\src-v2\ScaleStreamer.Service\bin\Release\net8.0\ScaleStreamer.Service.dll" "C:\Program Files\Scale Streamer\Service\"

echo.
echo Starting service...
net start ScaleStreamerService

echo.
echo Done!
pause
