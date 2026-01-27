using ScaleStreamer.Common.Interfaces;
using ScaleStreamer.Common.Models;
using System.Diagnostics;

namespace ScaleStreamer.Common.Protocols;

/// <summary>
/// Abstract base class for all scale protocol implementations
/// Provides common functionality for connection management, event handling, and reconnection logic
/// </summary>
public abstract class BaseScaleProtocol : IScaleProtocol, IDisposable
{
    protected ConnectionConfig? _config;
    protected ConnectionStatus _status = ConnectionStatus.Disconnected;
    protected CancellationTokenSource? _readingCts;
    protected readonly SemaphoreSlim _connectionLock = new(1, 1);
    protected DateTime? _lastReadTime;
    protected int _consecutiveErrors = 0;
    protected const int MaxConsecutiveErrors = 5;

    // IScaleProtocol properties
    public string Name { get; protected set; } = "Unknown";
    public string Manufacturer { get; protected set; } = "Generic";
    public string Version { get; protected set; } = "1.0";
    public ConnectionType ConnectionType { get; protected set; } = ConnectionType.TcpIp;
    public ConnectionStatus Status => _status;

    // Additional properties for backward compatibility
    public string ProtocolName
    {
        get => Name;
        protected set => Name = value;
    }
    public DateTime? LastReadTime => _lastReadTime;
    public bool IsConnected => _status == ConnectionStatus.Connected;

    // IScaleProtocol events
    public event EventHandler<WeightReading>? WeightReceived;
    public event EventHandler<string>? RawDataReceived;
    public event EventHandler<ConnectionStatus>? StatusChanged;
    public event EventHandler<Exception>? ErrorOccurred;

    protected BaseScaleProtocol()
    {
    }

    public abstract Task<bool> ConnectAsync(ConnectionConfig config, CancellationToken cancellationToken = default);
    public abstract Task DisconnectAsync();
    public abstract Task<WeightReading?> ReadWeightAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current scale status
    /// </summary>
    public virtual Task<ScaleStatusInfo> GetStatusAsync()
    {
        var status = new ScaleStatusInfo
        {
            IsConnected = _status == ConnectionStatus.Connected,
            ConnectionStatus = _status,
            LastReadTime = _lastReadTime,
            ConsecutiveErrors = _consecutiveErrors
        };
        return Task.FromResult(status);
    }

    /// <summary>
    /// Send a command to the scale (protocol-specific, default no-op)
    /// </summary>
    public virtual Task<bool> SendCommandAsync(string command)
    {
        // Base implementation - override in derived classes that support commands
        return Task.FromResult(false);
    }

    /// <summary>
    /// Test the connection without starting continuous reading
    /// </summary>
    public virtual async Task<bool> TestConnectionAsync(ConnectionConfig config)
    {
        try
        {
            var success = await ConnectAsync(config);
            if (success)
            {
                await DisconnectAsync();
            }
            return success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Start continuous reading loop
    /// </summary>
    public virtual async Task StartContinuousReadingAsync(CancellationToken cancellationToken = default)
    {
        if (_status == ConnectionStatus.Connected && _readingCts != null)
        {
            throw new InvalidOperationException("Continuous reading already started");
        }

        _readingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _ = Task.Run(async () =>
        {
            while (!_readingCts.Token.IsCancellationRequested)
            {
                try
                {
                    var reading = await ReadWeightAsync(_readingCts.Token);
                    if (reading != null)
                    {
                        _lastReadTime = DateTime.UtcNow;
                        _consecutiveErrors = 0;
                        OnWeightReceived(reading);
                    }

                    // Small delay to prevent CPU spinning
                    await Task.Delay(10, _readingCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _consecutiveErrors++;
                    OnErrorOccurred(ex);

                    if (_consecutiveErrors >= MaxConsecutiveErrors)
                    {
                        OnErrorOccurred(new Exception($"Too many consecutive errors ({_consecutiveErrors}), attempting reconnect"));
                        await AttemptReconnectAsync(_readingCts.Token);
                    }

                    await Task.Delay(100, _readingCts.Token);
                }
            }
        }, _readingCts.Token);
    }

    /// <summary>
    /// Stop continuous reading loop
    /// </summary>
    public virtual async Task StopContinuousReadingAsync()
    {
        if (_readingCts != null)
        {
            _readingCts.Cancel();
            await Task.Delay(100); // Give time for reading task to stop
            _readingCts.Dispose();
            _readingCts = null;
        }
    }

    /// <summary>
    /// Attempt to reconnect to the scale
    /// </summary>
    protected virtual async Task AttemptReconnectAsync(CancellationToken cancellationToken)
    {
        if (_config == null)
            return;

        try
        {
            UpdateStatus(ConnectionStatus.Reconnecting);

            // Use public DisconnectAsync (which acquires its own lock)
            await DisconnectAsync();
            await Task.Delay(_config.ReconnectIntervalSeconds * 1000, cancellationToken);

            var success = await ConnectAsync(_config, cancellationToken);
            if (success)
            {
                _consecutiveErrors = 0;
                OnErrorOccurred(new Exception("Reconnection successful"));
            }
            else
            {
                UpdateStatus(ConnectionStatus.Error);
                OnErrorOccurred(new Exception("Reconnection failed"));
            }
        }
        catch (Exception ex)
        {
            UpdateStatus(ConnectionStatus.Error);
            OnErrorOccurred(ex);
        }
    }

    /// <summary>
    /// Update connection status and raise event
    /// </summary>
    protected void UpdateStatus(ConnectionStatus newStatus)
    {
        if (_status != newStatus)
        {
            _status = newStatus;
            StatusChanged?.Invoke(this, _status);
        }
    }

    /// <summary>
    /// Raise WeightReceived event
    /// </summary>
    protected virtual void OnWeightReceived(WeightReading reading)
    {
        WeightReceived?.Invoke(this, reading);
    }

    /// <summary>
    /// Raise RawDataReceived event
    /// </summary>
    protected virtual void OnRawDataReceived(string rawData)
    {
        RawDataReceived?.Invoke(this, rawData);
    }

    /// <summary>
    /// Raise ErrorOccurred event
    /// </summary>
    protected virtual void OnErrorOccurred(Exception error)
    {
        ErrorOccurred?.Invoke(this, error);
        Debug.WriteLine($"[{Name}] {error.Message}");
    }

    /// <summary>
    /// Raise ErrorOccurred event with message
    /// </summary>
    protected virtual void OnErrorOccurred(string errorMessage)
    {
        OnErrorOccurred(new Exception(errorMessage));
    }

    /// <summary>
    /// Validate connection configuration
    /// </summary>
    protected virtual void ValidateConfig(ConnectionConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
    }

    public virtual void Dispose()
    {
        _readingCts?.Cancel();
        _readingCts?.Dispose();
        _connectionLock?.Dispose();
        GC.SuppressFinalize(this);
    }
}
