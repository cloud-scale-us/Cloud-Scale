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
    -- Example: {"vehicle_id": "ABC123", "axle_weights": [1000, 2000]}
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

-- ============================================================================
-- USEFUL QUERIES (comments for reference)
-- ============================================================================

-- Get weight statistics for last 24 hours:
-- SELECT
--   MIN(weight_value) as min,
--   MAX(weight_value) as max,
--   AVG(weight_value) as avg,
--   COUNT(*) as count
-- FROM weight_readings
-- WHERE timestamp > datetime('now', '-1 day');

-- Get recent errors:
-- SELECT * FROM events
-- WHERE level IN ('ERROR', 'CRITICAL')
-- ORDER BY timestamp DESC
-- LIMIT 100;

-- Get connection uptime:
-- SELECT
--   category,
--   COUNT(CASE WHEN message LIKE '%connected%' THEN 1 END) as connects,
--   COUNT(CASE WHEN message LIKE '%disconnected%' THEN 1 END) as disconnects
-- FROM events
-- WHERE category = 'ScaleConnection'
-- GROUP BY category;
