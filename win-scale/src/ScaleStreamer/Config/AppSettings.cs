namespace ScaleStreamer.Config;

public class AppSettings
{
    public ConnectionSettings Connection { get; set; } = new();
    public StreamSettings Stream { get; set; } = new();
    public DisplaySettings Display { get; set; } = new();
    public bool AutoStart { get; set; } = true;
}

public class ConnectionSettings
{
    public string Type { get; set; } = "TCP"; // "TCP" or "Serial"
    public string TcpHost { get; set; } = "10.1.10.210";
    public int TcpPort { get; set; } = 5001;
    public string SerialPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
}

public class StreamSettings
{
    public int RtspPort { get; set; } = 8554;
    public string Resolution { get; set; } = "640x480";
    public int FrameRate { get; set; } = 30;
    public int Bitrate { get; set; } = 800;
}

public class DisplaySettings
{
    public string Title { get; set; } = "FAIRBANKS 6011";
    public string Unit { get; set; } = "LB";
    public bool ShowTimestamp { get; set; } = true;
    public bool ShowStreamRate { get; set; } = true;
    public bool ShowTransmitIndicator { get; set; } = true;
    public string CustomLabel { get; set; } = "";
    public string FontColor { get; set; } = "red";
}
