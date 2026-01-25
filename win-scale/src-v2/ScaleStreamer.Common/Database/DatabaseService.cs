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

        // Read and execute schema
        var schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "schema.sql");
        if (File.Exists(schemaPath))
        {
            var schema = await File.ReadAllTextAsync(schemaPath);
            using var command = _connection.CreateCommand();
            command.CommandText = schema;
            await command.ExecuteNonQueryAsync();
        }
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

        while (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            var protocol = JsonSerializer.Deserialize<ProtocolDefinition>(json);
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
