using System.Text.Json.Serialization;

namespace ScaleStreamer.Common.IPC;

/// <summary>
/// Base message for IPC communication between Service and GUI
/// </summary>
public class IpcMessage
{
    [JsonPropertyName("message_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IpcMessageType MessageType { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}

/// <summary>
/// Message types for IPC communication
/// </summary>
public enum IpcMessageType
{
    // Commands (GUI -> Service)
    AddScale,
    RemoveScale,
    StartScale,
    StopScale,
    GetScaleStatus,
    GetAllStatuses,
    SendScaleCommand,
    GetWeightHistory,
    GetRecentEvents,

    // Responses (Service -> GUI)
    StatusUpdate,
    WeightReading,
    Error,
    ConnectionStatus,
    ScaleList,
    RawData,

    // Events (Service -> GUI)
    ScaleConnected,
    ScaleDisconnected,
    ServiceStarted,
    ServiceStopped
}

/// <summary>
/// Command message from GUI to Service
/// </summary>
public class IpcCommand : IpcMessage
{
    [JsonPropertyName("command_id")]
    public string CommandId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Response message from Service to GUI
/// </summary>
public class IpcResponse : IpcMessage
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("command_id")]
    public string? CommandId { get; set; }
}

/// <summary>
/// Event notification from Service to GUI
/// </summary>
public class IpcEvent : IpcMessage
{
    [JsonPropertyName("scale_id")]
    public string? ScaleId { get; set; }

    [JsonPropertyName("event_data")]
    public string? EventData { get; set; }
}
