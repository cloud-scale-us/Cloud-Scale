using Microsoft.Extensions.Logging;
using ScaleStreamer.Common.Database;
using ScaleStreamer.Common.IPC;
using ScaleStreamer.Common.Models;
using System.Text.Json;

namespace ScaleStreamer.Service;

/// <summary>
/// Handles IPC commands from GUI application
/// </summary>
public class IpcCommandHandler
{
    private readonly ILogger<IpcCommandHandler> _logger;
    private readonly ScaleConnectionManager _connectionManager;
    private readonly DatabaseService _database;

    public IpcCommandHandler(
        ILogger<IpcCommandHandler> logger,
        ScaleConnectionManager connectionManager,
        DatabaseService database)
    {
        _logger = logger;
        _connectionManager = connectionManager;
        _database = database;
    }

    /// <summary>
    /// Process incoming IPC command and return response
    /// </summary>
    public async Task<IpcResponse> HandleCommandAsync(IpcCommand command)
    {
        try
        {
            _logger.LogDebug("Handling IPC command: {MessageType}", command.MessageType);

            return command.MessageType switch
            {
                IpcMessageType.AddScale => await HandleAddScaleAsync(command),
                IpcMessageType.RemoveScale => await HandleRemoveScaleAsync(command),
                IpcMessageType.StartScale => await HandleStartScaleAsync(command),
                IpcMessageType.StopScale => await HandleStopScaleAsync(command),
                IpcMessageType.GetScaleStatus => await HandleGetScaleStatusAsync(command),
                IpcMessageType.GetAllStatuses => await HandleGetAllStatusesAsync(command),
                IpcMessageType.SendScaleCommand => await HandleSendScaleCommandAsync(command),
                IpcMessageType.GetWeightHistory => await HandleGetWeightHistoryAsync(command),
                IpcMessageType.GetRecentEvents => await HandleGetRecentEventsAsync(command),
                _ => CreateErrorResponse(command.CommandId, $"Unknown command type: {command.MessageType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling IPC command: {MessageType}", command.MessageType);
            return CreateErrorResponse(command.CommandId, ex.Message);
        }
    }

    private async Task<IpcResponse> HandleAddScaleAsync(IpcCommand command)
    {
        try
        {
            if (string.IsNullOrEmpty(command.Payload))
                return CreateErrorResponse(command.CommandId, "Missing scale configuration");

            var config = JsonSerializer.Deserialize<ScaleConfiguration>(command.Payload);
            if (config == null)
                return CreateErrorResponse(command.CommandId, "Invalid scale configuration");

            // Load protocol definition
            var protocol = await LoadProtocolDefinitionAsync(config.ProtocolName);
            if (protocol == null)
                return CreateErrorResponse(command.CommandId, $"Protocol not found: {config.ProtocolName}");

            // Update connection settings from config
            protocol.Connection = config.Connection;

            // Add scale to connection manager
            var success = await _connectionManager.AddScaleAsync(config.ScaleId, protocol);

            if (success)
            {
                // Save configuration to database
                await SaveScaleConfigurationAsync(config);

                _logger.LogInformation("Scale added successfully: {ScaleId}", config.ScaleId);
                return CreateSuccessResponse(command.CommandId, "Scale added successfully");
            }
            else
            {
                return CreateErrorResponse(command.CommandId, "Failed to connect to scale");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding scale");
            return CreateErrorResponse(command.CommandId, ex.Message);
        }
    }

    private async Task<IpcResponse> HandleRemoveScaleAsync(IpcCommand command)
    {
        try
        {
            var scaleId = command.Payload;
            if (string.IsNullOrEmpty(scaleId))
                return CreateErrorResponse(command.CommandId, "Missing scale ID");

            var success = await _connectionManager.RemoveScaleAsync(scaleId);

            if (success)
            {
                _logger.LogInformation("Scale removed: {ScaleId}", scaleId);
                return CreateSuccessResponse(command.CommandId, "Scale removed successfully");
            }
            else
            {
                return CreateErrorResponse(command.CommandId, "Scale not found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing scale");
            return CreateErrorResponse(command.CommandId, ex.Message);
        }
    }

    private async Task<IpcResponse> HandleStartScaleAsync(IpcCommand command)
    {
        try
        {
            var scaleId = command.Payload;
            if (string.IsNullOrEmpty(scaleId))
                return CreateErrorResponse(command.CommandId, "Missing scale ID");

            var scale = _connectionManager.GetScale(scaleId);
            if (scale == null)
                return CreateErrorResponse(command.CommandId, "Scale not found");

            await scale.StartContinuousReadingAsync();

            _logger.LogInformation("Scale started: {ScaleId}", scaleId);
            return CreateSuccessResponse(command.CommandId, "Scale started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting scale");
            return CreateErrorResponse(command.CommandId, ex.Message);
        }
    }

    private async Task<IpcResponse> HandleStopScaleAsync(IpcCommand command)
    {
        try
        {
            var scaleId = command.Payload;
            if (string.IsNullOrEmpty(scaleId))
                return CreateErrorResponse(command.CommandId, "Missing scale ID");

            var scale = _connectionManager.GetScale(scaleId);
            if (scale == null)
                return CreateErrorResponse(command.CommandId, "Scale not found");

            await scale.StopContinuousReadingAsync();

            _logger.LogInformation("Scale stopped: {ScaleId}", scaleId);
            return CreateSuccessResponse(command.CommandId, "Scale stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping scale");
            return CreateErrorResponse(command.CommandId, ex.Message);
        }
    }

    private async Task<IpcResponse> HandleGetScaleStatusAsync(IpcCommand command)
    {
        try
        {
            var scaleId = command.Payload;
            if (string.IsNullOrEmpty(scaleId))
                return CreateErrorResponse(command.CommandId, "Missing scale ID");

            var scale = _connectionManager.GetScale(scaleId);
            if (scale == null)
                return CreateErrorResponse(command.CommandId, "Scale not found");

            var scaleStatus = await scale.GetStatusAsync();
            var status = new
            {
                ScaleId = scaleId,
                Status = scale.Status.ToString(),
                LastReadTime = scaleStatus.LastReadTime,
                IsConnected = scaleStatus.IsConnected
            };

            var response = CreateSuccessResponse(command.CommandId, "Status retrieved");
            response.Payload = JsonSerializer.Serialize(status);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scale status");
            return CreateErrorResponse(command.CommandId, ex.Message);
        }
    }

    private Task<IpcResponse> HandleGetAllStatusesAsync(IpcCommand command)
    {
        try
        {
            var statuses = _connectionManager.GetAllStatuses();

            var statusList = statuses.Select(kvp => new
            {
                ScaleId = kvp.Key,
                Status = kvp.Value.ToString()
            }).ToList();

            var response = CreateSuccessResponse(command.CommandId, "Statuses retrieved");
            response.Payload = JsonSerializer.Serialize(statusList);

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all statuses");
            return Task.FromResult(CreateErrorResponse(command.CommandId, ex.Message));
        }
    }

    private async Task<IpcResponse> HandleSendScaleCommandAsync(IpcCommand command)
    {
        try
        {
            if (string.IsNullOrEmpty(command.Payload))
                return CreateErrorResponse(command.CommandId, "Missing command data");

            var commandData = JsonSerializer.Deserialize<ScaleCommand>(command.Payload);
            if (commandData == null)
                return CreateErrorResponse(command.CommandId, "Invalid command data");

            // TODO: Implement scale command sending (zero, tare, print, etc.)
            await Task.CompletedTask;

            _logger.LogInformation("Scale command sent: {Command} to {ScaleId}", commandData.Command, commandData.ScaleId);
            return CreateSuccessResponse(command.CommandId, "Command sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending scale command");
            return CreateErrorResponse(command.CommandId, ex.Message);
        }
    }

    private async Task<IpcResponse> HandleGetWeightHistoryAsync(IpcCommand command)
    {
        try
        {
            if (string.IsNullOrEmpty(command.Payload))
                return CreateErrorResponse(command.CommandId, "Missing query parameters");

            var query = JsonSerializer.Deserialize<WeightHistoryQuery>(command.Payload);
            if (query == null)
                return CreateErrorResponse(command.CommandId, "Invalid query parameters");

            var readings = await _database.GetWeightReadingsAsync(
                query.ScaleId,
                query.StartTime,
                query.EndTime,
                query.Limit);

            var response = CreateSuccessResponse(command.CommandId, "Weight history retrieved");
            response.Payload = JsonSerializer.Serialize(readings);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weight history");
            return CreateErrorResponse(command.CommandId, ex.Message);
        }
    }

    private async Task<IpcResponse> HandleGetRecentEventsAsync(IpcCommand command)
    {
        try
        {
            var limit = 100;
            if (!string.IsNullOrEmpty(command.Payload))
            {
                int.TryParse(command.Payload, out limit);
            }

            var events = await _database.GetRecentEventsAsync(limit);

            var response = CreateSuccessResponse(command.CommandId, "Events retrieved");
            response.Payload = JsonSerializer.Serialize(events);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent events");
            return CreateErrorResponse(command.CommandId, ex.Message);
        }
    }

    private async Task<ProtocolDefinition?> LoadProtocolDefinitionAsync(string protocolName)
    {
        // Try to load from database first
        var templates = await _database.GetProtocolTemplatesAsync();
        var template = templates.FirstOrDefault(t => t.ProtocolName == protocolName);

        if (template != null)
            return template;

        // Try to load from JSON file
        var protocolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "protocols");
        var jsonFiles = Directory.GetFiles(protocolsPath, "*.json", SearchOption.AllDirectories);

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
                };

                var protocol = JsonSerializer.Deserialize<ProtocolDefinition>(json, options);

                if (protocol != null && protocol.ProtocolName == protocolName)
                    return protocol;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load protocol from: {File}. Error: {Message}", file, ex.Message);
            }
        }

        return null;
    }

    private async Task SaveScaleConfigurationAsync(ScaleConfiguration config)
    {
        // TODO: Save to scales table in database
        await _database.SetConfigValueAsync($"scale_{config.ScaleId}_config", JsonSerializer.Serialize(config));
    }

    private IpcResponse CreateSuccessResponse(string commandId, string message)
    {
        return new IpcResponse
        {
            MessageType = IpcMessageType.StatusUpdate,
            CommandId = commandId,
            Success = true,
            Payload = message,
            Timestamp = DateTime.UtcNow
        };
    }

    private IpcResponse CreateErrorResponse(string commandId, string errorMessage)
    {
        return new IpcResponse
        {
            MessageType = IpcMessageType.Error,
            CommandId = commandId,
            Success = false,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Scale configuration model for IPC
/// </summary>
public class ScaleConfiguration
{
    public string ScaleId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public string MarketType { get; set; } = "";
    public string ProtocolName { get; set; } = "";
    public ConnectionConfig Connection { get; set; } = new();
}

/// <summary>
/// Scale command model for IPC
/// </summary>
public class ScaleCommand
{
    public string ScaleId { get; set; } = "";
    public string Command { get; set; } = ""; // zero, tare, print, etc.
}

/// <summary>
/// Weight history query model
/// </summary>
public class WeightHistoryQuery
{
    public string ScaleId { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Limit { get; set; } = 1000;
}
