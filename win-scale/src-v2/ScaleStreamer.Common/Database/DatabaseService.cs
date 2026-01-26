using Microsoft.Data.Sqlite;
using ScaleStreamer.Common.Models;
using System.Text.Json;

namespace ScaleStreamer.Common.Database;

/// <summary>
/// SQLite database service for storing configuration, weight data, and logs
/// </summary>
public class DatabaseService : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public DatabaseService(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
    }

    /// <summary>
    /// Initialize database and create schema if needed
    /// </summary>
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();

        // Execute embedded schema
        using var command = _connection.CreateCommand();
        command.CommandText = GetDatabaseSchema();
        await command.ExecuteNonQueryAsync();
    }

    private static string GetDatabaseSchema()
    {
        return @"
-- Scale Streamer v2.0 Database Schema
-- SQLite database for storing configuration, weight data, and logs

-- ============================================================================
-- CONFIGURATION TABLES
-- ============================================================================

-- Application configuration (key-value store)
CREATE TABLE IF NOT EXISTS config (
    key TEXT PRIMARY KEY,
    value TEXT,
    modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Scale configurations (supports multiple scales)
CREATE TABLE IF NOT EXISTS scales (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    location TEXT,
    market_type TEXT,
    protocol_name TEXT,
    protocol_definition TEXT, -- JSON
    connection_config TEXT,   -- JSON
    enabled INTEGER DEFAULT 1,
    created TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Protocol templates (built-in and custom)
CREATE TABLE IF NOT EXISTS protocol_templates (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    category TEXT, -- manufacturer, generic, industry, custom
    definition TEXT, -- JSON
    is_builtin INTEGER DEFAULT 0,
    created_by TEXT,
    created TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ============================================================================
-- WEIGHT DATA TABLES
-- ============================================================================

-- Weight readings (universal format for all scale types)
CREATE TABLE IF NOT EXISTS weight_readings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    scale_id TEXT REFERENCES scales(id),
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,

    -- Core fields (always present)
    weight_value REAL NOT NULL,
    weight_unit TEXT NOT NULL,

    -- Optional fields (protocol-dependent)
    tare REAL,
    gross REAL,
    net REAL,
    status TEXT,

    -- Extended fields (market-specific, stored as JSON)
    extended_data TEXT,

    -- Metadata
    raw_data TEXT, -- Original data string for debugging
    quality_score REAL DEFAULT 1.0, -- 0-1, confidence in reading
    is_valid INTEGER DEFAULT 1
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_weight_scale_timestamp ON weight_readings(scale_id, timestamp);
CREATE INDEX IF NOT EXISTS idx_weight_timestamp ON weight_readings(timestamp);

-- ============================================================================
-- TRANSACTION TABLES (for markets that need it)
-- ============================================================================

-- Transactions (shipping, receiving, batches, etc.)
CREATE TABLE IF NOT EXISTS transactions (
    id TEXT PRIMARY KEY,
    scale_id TEXT REFERENCES scales(id),
    transaction_type TEXT, -- shipping, receiving, wim, batch, etc.
    start_time DATETIME,
    end_time DATETIME,
    total_weight REAL,
    item_count INTEGER,
    customer_id TEXT,
    vehicle_id TEXT,
    operator TEXT,
    notes TEXT,
    metadata TEXT -- JSON for flexible additional data
);

CREATE INDEX IF NOT EXISTS idx_transaction_scale_time ON transactions(scale_id, start_time);
CREATE INDEX IF NOT EXISTS idx_transaction_type ON transactions(transaction_type);

-- ============================================================================
-- LOGGING & EVENTS TABLES
-- ============================================================================

-- Application events and errors
CREATE TABLE IF NOT EXISTS events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    level TEXT CHECK(level IN ('DEBUG','INFO','WARN','ERROR','CRITICAL')),
    category TEXT, -- ScaleConnection, Service, GUI, FFmpeg, MediaMTX, etc.
    message TEXT,
    details TEXT, -- JSON with additional context
    scale_id TEXT,
    correlation_id TEXT
);

CREATE INDEX IF NOT EXISTS idx_events_timestamp ON events(timestamp);
CREATE INDEX IF NOT EXISTS idx_events_level ON events(level);
CREATE INDEX IF NOT EXISTS idx_events_category ON events(category);
CREATE INDEX IF NOT EXISTS idx_events_correlation ON events(correlation_id);

-- ============================================================================
-- PERFORMANCE METRICS TABLES
-- ============================================================================

-- System performance metrics
CREATE TABLE IF NOT EXISTS metrics (
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    cpu_percent REAL,
    memory_mb REAL,
    data_rate REAL,           -- readings per second
    frame_rate REAL,          -- video frames per second
    stream_bitrate REAL,      -- Kbps
    viewer_count INTEGER,
    ffmpeg_cpu REAL,
    mediamtx_cpu REAL
);

CREATE INDEX IF NOT EXISTS idx_metrics_timestamp ON metrics(timestamp);

-- ============================================================================
-- ALERTS & NOTIFICATIONS
-- ============================================================================

-- Alert rules
CREATE TABLE IF NOT EXISTS alert_rules (
    id TEXT PRIMARY KEY,
    scale_id TEXT,
    rule_name TEXT NOT NULL,
    rule_type TEXT, -- threshold, connection_lost, rate_limit, custom
    condition TEXT, -- JSON expression
    action TEXT,    -- email, sms, webhook, log
    enabled INTEGER DEFAULT 1,
    created TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Alert history
CREATE TABLE IF NOT EXISTS alert_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    rule_id TEXT REFERENCES alert_rules(id),
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    triggered_value REAL,
    message TEXT,
    action_taken TEXT,
    acknowledged INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_alerts_timestamp ON alert_history(timestamp);

-- ============================================================================
-- CLEANUP VIEWS & TRIGGERS
-- ============================================================================

-- Trigger to auto-purge old weight data (keep 30 days by default)
CREATE TRIGGER IF NOT EXISTS purge_old_weight_readings
AFTER INSERT ON weight_readings
BEGIN
    DELETE FROM weight_readings
    WHERE timestamp < datetime('now', '-30 days');
END;

-- Trigger to auto-purge old events (keep 90 days)
CREATE TRIGGER IF NOT EXISTS purge_old_events
AFTER INSERT ON events
BEGIN
    DELETE FROM events
    WHERE timestamp < datetime('now', '-90 days');
END;

-- Trigger to auto-purge old metrics (keep 7 days)
CREATE TRIGGER IF NOT EXISTS purge_old_metrics
AFTER INSERT ON metrics
BEGIN
    DELETE FROM metrics
    WHERE timestamp < datetime('now', '-7 days');
END;

-- ============================================================================
-- INITIAL DATA
-- ============================================================================

-- Insert default configuration
INSERT OR IGNORE INTO config (key, value) VALUES ('version', '2.0.0');
INSERT OR IGNORE INTO config (key, value) VALUES ('db_schema_version', '1');
INSERT OR IGNORE INTO config (key, value) VALUES ('weight_retention_days', '30');
INSERT OR IGNORE INTO config (key, value) VALUES ('event_retention_days', '90');
INSERT OR IGNORE INTO config (key, value) VALUES ('metric_retention_days', '7');
        ";
    }

    #region Weight Readings

    /// <summary>
    /// Insert weight reading into database
    /// </summary>
    public async Task<long> InsertWeightReadingAsync(WeightReading reading)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = @"
            INSERT INTO weight_readings
            (scale_id, timestamp, weight_value, weight_unit, tare, gross, net, status,
             extended_data, raw_data, quality_score, is_valid)
            VALUES
            (@scaleId, @timestamp, @weight, @unit, @tare, @gross, @net, @status,
             @extendedData, @rawData, @qualityScore, @isValid);
            SELECT last_insert_rowid();";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;

        command.Parameters.AddWithValue("@scaleId", reading.ScaleId ?? "");
        command.Parameters.AddWithValue("@timestamp", reading.Timestamp);
        command.Parameters.AddWithValue("@weight", reading.Weight);
        command.Parameters.AddWithValue("@unit", reading.Unit);
        command.Parameters.AddWithValue("@tare", reading.Tare.HasValue ? reading.Tare.Value : DBNull.Value);
        command.Parameters.AddWithValue("@gross", reading.Gross.HasValue ? reading.Gross.Value : DBNull.Value);
        command.Parameters.AddWithValue("@net", reading.Net.HasValue ? reading.Net.Value : DBNull.Value);
        command.Parameters.AddWithValue("@status", reading.Status.ToString());
        command.Parameters.AddWithValue("@extendedData",
            reading.ExtendedData != null ? JsonSerializer.Serialize(reading.ExtendedData) : DBNull.Value);
        command.Parameters.AddWithValue("@rawData", reading.RawData ?? "");
        command.Parameters.AddWithValue("@qualityScore", reading.QualityScore);
        command.Parameters.AddWithValue("@isValid", reading.IsValid ? 1 : 0);

        var result = await command.ExecuteScalarAsync();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    /// <summary>
    /// Get weight readings for a scale within time range
    /// </summary>
    public async Task<List<WeightReading>> GetWeightReadingsAsync(
        string scaleId,
        DateTime startTime,
        DateTime endTime,
        int limit = 1000)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = @"
            SELECT * FROM weight_readings
            WHERE scale_id = @scaleId
              AND timestamp BETWEEN @startTime AND @endTime
            ORDER BY timestamp DESC
            LIMIT @limit";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@scaleId", scaleId);
        command.Parameters.AddWithValue("@startTime", startTime);
        command.Parameters.AddWithValue("@endTime", endTime);
        command.Parameters.AddWithValue("@limit", limit);

        var readings = new List<WeightReading>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            readings.Add(ReadWeightReadingFromDb(reader));
        }

        return readings;
    }

    /// <summary>
    /// Get weight statistics for a scale
    /// </summary>
    public async Task<WeightStatistics?> GetWeightStatisticsAsync(string scaleId, DateTime startTime, DateTime endTime)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = @"
            SELECT
                MIN(weight_value) as min_weight,
                MAX(weight_value) as max_weight,
                AVG(weight_value) as avg_weight,
                COUNT(*) as reading_count
            FROM weight_readings
            WHERE scale_id = @scaleId
              AND timestamp BETWEEN @startTime AND @endTime
              AND is_valid = 1";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@scaleId", scaleId);
        command.Parameters.AddWithValue("@startTime", startTime);
        command.Parameters.AddWithValue("@endTime", endTime);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new WeightStatistics
            {
                MinWeight = reader.IsDBNull(0) ? 0 : reader.GetDouble(0),
                MaxWeight = reader.IsDBNull(1) ? 0 : reader.GetDouble(1),
                AverageWeight = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                ReadingCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
            };
        }

        return null;
    }

    private WeightReading ReadWeightReadingFromDb(SqliteDataReader reader)
    {
        return new WeightReading
        {
            Timestamp = reader.GetDateTime(reader.GetOrdinal("timestamp")),
            ScaleId = reader.GetString(reader.GetOrdinal("scale_id")),
            Weight = reader.GetDouble(reader.GetOrdinal("weight_value")),
            Unit = reader.GetString(reader.GetOrdinal("weight_unit")),
            Tare = reader.IsDBNull(reader.GetOrdinal("tare")) ? null : reader.GetDouble(reader.GetOrdinal("tare")),
            Gross = reader.IsDBNull(reader.GetOrdinal("gross")) ? null : reader.GetDouble(reader.GetOrdinal("gross")),
            Net = reader.IsDBNull(reader.GetOrdinal("net")) ? null : reader.GetDouble(reader.GetOrdinal("net")),
            Status = Enum.Parse<ScaleStatus>(reader.GetString(reader.GetOrdinal("status"))),
            RawData = reader.GetString(reader.GetOrdinal("raw_data")),
            QualityScore = reader.GetDouble(reader.GetOrdinal("quality_score")),
            IsValid = reader.GetInt32(reader.GetOrdinal("is_valid")) == 1
        };
    }

    #endregion

    #region Events Logging

    /// <summary>
    /// Log an event to the database
    /// </summary>
    public async Task LogEventAsync(string level, string category, string message, string? details = null, string? scaleId = null)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = @"
            INSERT INTO events (timestamp, level, category, message, details, scale_id)
            VALUES (@timestamp, @level, @category, @message, @details, @scaleId)";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@timestamp", DateTime.UtcNow);
        command.Parameters.AddWithValue("@level", level);
        command.Parameters.AddWithValue("@category", category);
        command.Parameters.AddWithValue("@message", message);
        command.Parameters.AddWithValue("@details", details ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@scaleId", scaleId ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Get recent events
    /// </summary>
    public async Task<List<EventLog>> GetRecentEventsAsync(int limit = 100, string? level = null, string? category = null)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = "SELECT * FROM events WHERE 1=1";
        if (!string.IsNullOrEmpty(level))
            sql += " AND level = @level";
        if (!string.IsNullOrEmpty(category))
            sql += " AND category = @category";
        sql += " ORDER BY timestamp DESC LIMIT @limit";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        if (!string.IsNullOrEmpty(level))
            command.Parameters.AddWithValue("@level", level);
        if (!string.IsNullOrEmpty(category))
            command.Parameters.AddWithValue("@category", category);
        command.Parameters.AddWithValue("@limit", limit);

        var events = new List<EventLog>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            events.Add(new EventLog
            {
                Timestamp = reader.GetDateTime(reader.GetOrdinal("timestamp")),
                Level = reader.GetString(reader.GetOrdinal("level")),
                Category = reader.GetString(reader.GetOrdinal("category")),
                Message = reader.GetString(reader.GetOrdinal("message")),
                Details = reader.IsDBNull(reader.GetOrdinal("details")) ? null : reader.GetString(reader.GetOrdinal("details"))
            });
        }

        return events;
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Get configuration value
    /// </summary>
    public async Task<string?> GetConfigValueAsync(string key)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = "SELECT value FROM config WHERE key = @key";
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@key", key);

        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    /// <summary>
    /// Set configuration value
    /// </summary>
    public async Task SetConfigValueAsync(string key, string value)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = @"
            INSERT INTO config (key, value, modified)
            VALUES (@key, @value, @modified)
            ON CONFLICT(key) DO UPDATE SET value = @value, modified = @modified";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.Parameters.AddWithValue("@modified", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    #endregion

    #region Scale Registration

    /// <summary>
    /// Register or update a scale in the database
    /// </summary>
    public async Task RegisterScaleAsync(string scaleId, string name, string? location = null, string? protocolName = null)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = @"
            INSERT INTO scales (id, name, location, protocol_name, enabled, created, modified)
            VALUES (@id, @name, @location, @protocolName, 1, @created, @modified)
            ON CONFLICT(id) DO UPDATE SET
                name = @name,
                location = COALESCE(@location, scales.location),
                protocol_name = COALESCE(@protocolName, scales.protocol_name),
                modified = @modified";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", scaleId);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@location", location ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@protocolName", protocolName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
        command.Parameters.AddWithValue("@modified", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Check if a scale is registered
    /// </summary>
    public async Task<bool> IsScaleRegisteredAsync(string scaleId)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = "SELECT COUNT(*) FROM scales WHERE id = @id";
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", scaleId);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    #endregion

    #region Protocol Templates

    /// <summary>
    /// Save protocol template to database
    /// </summary>
    public async Task SaveProtocolTemplateAsync(ProtocolDefinition protocol, bool isBuiltin = false)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = @"
            INSERT INTO protocol_templates (id, name, category, definition, is_builtin, created, modified)
            VALUES (@id, @name, @category, @definition, @isBuiltin, @created, @modified)
            ON CONFLICT(id) DO UPDATE SET
                name = @name,
                category = @category,
                definition = @definition,
                modified = @modified";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", protocol.ProtocolName);
        command.Parameters.AddWithValue("@name", protocol.ProtocolName);
        command.Parameters.AddWithValue("@category", protocol.Manufacturer);
        command.Parameters.AddWithValue("@definition", JsonSerializer.Serialize(protocol));
        command.Parameters.AddWithValue("@isBuiltin", isBuiltin ? 1 : 0);
        command.Parameters.AddWithValue("@created", DateTime.UtcNow);
        command.Parameters.AddWithValue("@modified", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Get all protocol templates
    /// </summary>
    public async Task<List<ProtocolDefinition>> GetProtocolTemplatesAsync()
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var sql = "SELECT definition FROM protocol_templates ORDER BY category, name";
        using var command = _connection.CreateCommand();
        command.CommandText = sql;

        var templates = new List<ProtocolDefinition>();
        using var reader = await command.ExecuteReaderAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
        };

        while (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            var protocol = JsonSerializer.Deserialize<ProtocolDefinition>(json, options);
            if (protocol != null)
                templates.Add(protocol);
        }

        return templates;
    }

    #endregion

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Weight statistics model
/// </summary>
public class WeightStatistics
{
    public double MinWeight { get; set; }
    public double MaxWeight { get; set; }
    public double AverageWeight { get; set; }
    public int ReadingCount { get; set; }
}

/// <summary>
/// Event log model
/// </summary>
public class EventLog
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Details { get; set; }
}
