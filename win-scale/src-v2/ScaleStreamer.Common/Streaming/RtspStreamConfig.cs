namespace ScaleStreamer.Common.Streaming;

/// <summary>
/// RTSP streaming configuration
/// </summary>
public class RtspStreamConfig
{
    /// <summary>
    /// Video width in pixels
    /// </summary>
    public int VideoWidth { get; set; } = 1920;

    /// <summary>
    /// Video height in pixels
    /// </summary>
    public int VideoHeight { get; set; } = 1080;

    /// <summary>
    /// Frames per second
    /// </summary>
    public int FrameRate { get; set; } = 30;

    /// <summary>
    /// Video bitrate (e.g., "2M", "4M")
    /// </summary>
    public string VideoBitrate { get; set; } = "2M";

    /// <summary>
    /// Video codec (e.g., "libx264", "h264_nvenc")
    /// </summary>
    public string VideoCodec { get; set; } = "libx264";

    /// <summary>
    /// RTSP server port
    /// </summary>
    public int RtspPort { get; set; } = 8554;

    /// <summary>
    /// HLS server port
    /// </summary>
    public int HlsPort { get; set; } = 8888;

    /// <summary>
    /// Stream name/path
    /// </summary>
    public string StreamName { get; set; } = "scale";

    /// <summary>
    /// Font for overlay text
    /// </summary>
    public string FontName { get; set; } = "Arial";

    /// <summary>
    /// Font size for weight display
    /// </summary>
    public int FontSize { get; set; } = 72;

    /// <summary>
    /// Font color (hex format, e.g., "white", "0xFFFFFF")
    /// </summary>
    public string FontColor { get; set; } = "white";

    /// <summary>
    /// Background color for text (hex format, with alpha)
    /// </summary>
    public string BackgroundColor { get; set; } = "black@0.7";

    /// <summary>
    /// Text position X (pixels from left)
    /// </summary>
    public int TextPositionX { get; set; } = 50;

    /// <summary>
    /// Text position Y (pixels from top)
    /// </summary>
    public int TextPositionY { get; set; } = 50;

    /// <summary>
    /// Path to FFmpeg executable
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>
    /// Path to MediaMTX executable
    /// </summary>
    public string MediaMtxPath { get; set; } = "mediamtx";

    /// <summary>
    /// Enable hardware acceleration
    /// </summary>
    public bool UseHardwareAcceleration { get; set; } = false;

    /// <summary>
    /// Hardware acceleration method (e.g., "cuda", "qsv", "dxva2")
    /// </summary>
    public string HardwareAccelerationMethod { get; set; } = "cuda";

    /// <summary>
    /// Scale ID to stream
    /// </summary>
    public string? ScaleId { get; set; }

    /// <summary>
    /// Update interval for weight overlay (milliseconds)
    /// </summary>
    public int UpdateIntervalMs { get; set; } = 100;

    /// <summary>
    /// Require HTTP Basic Authentication (for NVR compatibility)
    /// </summary>
    public bool RequireAuth { get; set; } = false;

    /// <summary>
    /// Username for HTTP Basic Auth
    /// </summary>
    public string Username { get; set; } = "admin";

    /// <summary>
    /// Password for HTTP Basic Auth
    /// </summary>
    public string Password { get; set; } = "scale123";
}
