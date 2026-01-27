# Start H.264 transcoding stream
# Reads MJPEG from ScaleStreamer and outputs H.264 to MediaMTX

$ffmpegPath = "C:\Users\Windfield\Cloud-Scale\ffmpeg\ffmpeg-master-latest-win64-gpl\bin\ffmpeg.exe"
# Read from MediaMTX proxy (port 554) which handles RTSP properly
$inputUrl = "rtsp://admin:scale123@127.0.0.1:554/scale-mjpeg"
$outputUrl = "rtsp://127.0.0.1:554/scale-h264"

Write-Host "Starting H.264 transcoding..." -ForegroundColor Cyan
Write-Host "Input:  $inputUrl (MJPEG via MediaMTX)" -ForegroundColor Gray
Write-Host "Output: $outputUrl (H.264 to MediaMTX)" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

# FFmpeg command:
# - Input: RTSP MJPEG from our service on port 8554
# - Codec: H.264 with CRF 18 for high quality
# - Preset: medium for good balance of quality and CPU
# - GOP: 30 frames (1 second at 30fps)
# - Output: RTSP to MediaMTX on port 554
& $ffmpegPath `
    -rtsp_transport tcp `
    -i $inputUrl `
    -c:v libx264 `
    -preset medium `
    -crf 18 `
    -pix_fmt yuv420p `
    -g 30 `
    -tune stillimage `
    -f rtsp `
    $outputUrl
