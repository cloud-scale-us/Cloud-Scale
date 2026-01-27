#!/usr/bin/env python3
"""
Scale Streamer - HTTP MJPEG Weight Display Server
Creates a video stream showing the current weight reading from the scale service.

Usage:
    python weight_stream_server.py

View stream:
    VLC: http://localhost:8555/stream
    Browser: http://localhost:8555/
"""

import http.server
import socketserver
import threading
import time
import io
import json
import os
from pathlib import Path

# Try to import PIL for image generation
try:
    from PIL import Image, ImageDraw, ImageFont
    HAS_PIL = True
except ImportError:
    HAS_PIL = False
    print("WARNING: Pillow not installed. Run: pip install pillow")

# Configuration
PORT = 8555
WIDTH = 1280
HEIGHT = 720
FPS = 10
SETTINGS_PATH = r"C:\ProgramData\ScaleStreamer\settings.json"

# Global state
current_weight = "----.--"
current_status = "NO DATA"
current_unit = "lb"
scale_id = "scale-001"
last_update = time.time()

def load_settings():
    """Load scale settings from the service config file"""
    global scale_id
    try:
        if os.path.exists(SETTINGS_PATH):
            with open(SETTINGS_PATH) as f:
                settings = json.load(f)
                scale_id = settings.get('scaleConnection', {}).get('scaleId', 'scale-001')
    except Exception as e:
        print(f"Could not load settings: {e}")

def read_weight_from_logs():
    """Read current weight from service logs"""
    global current_weight, current_status, current_unit, last_update

    log_dir = Path(r"C:\ProgramData\ScaleStreamer\logs")
    today = time.strftime("%Y%m%d")
    log_file = log_dir / f"service-{today}.log"

    if not log_file.exists():
        return

    try:
        # Read last 50 lines of log
        with open(log_file, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()[-50:]

        # Find most recent weight reading
        for line in reversed(lines):
            if "Weight received" in line:
                # Parse: "Weight received from scale-001: 840 lb"
                parts = line.split("Weight received from")
                if len(parts) > 1:
                    weight_part = parts[1].split(":")[-1].strip()
                    tokens = weight_part.split()
                    if len(tokens) >= 2:
                        current_weight = tokens[0]
                        current_unit = tokens[1] if len(tokens) > 1 else "lb"
                        current_status = "STABLE"
                        last_update = time.time()
                        return
    except Exception as e:
        print(f"Error reading logs: {e}")

def generate_frame():
    """Generate a single JPEG frame with weight display"""
    if not HAS_PIL:
        # Return a simple 1x1 black pixel if PIL not available
        return b'\xff\xd8\xff\xe0\x00\x10JFIF\x00\x01\x01\x00\x00\x01\x00\x01\x00\x00\xff\xdb\x00C\x00\x08\x06\x06\x07\x06\x05\x08\x07\x07\x07\t\t\x08\n\x0c\x14\r\x0c\x0b\x0b\x0c\x19\x12\x13\x0f\x14\x1d\x1a\x1f\x1e\x1d\x1a\x1c\x1c $.\' ",#\x1c\x1c(7),01444\x1f\'9televiion9telecast.;#-\'<>\'---\'\'--\'----\'\'-----\'-----\'---\'-----.\'------\xff\xc0\x00\x0b\x08\x00\x01\x00\x01\x01\x01\x11\x00\xff\xc4\x00\x1f\x00\x00\x01\x05\x01\x01\x01\x01\x01\x01\x00\x00\x00\x00\x00\x00\x00\x00\x01\x02\x03\x04\x05\x06\x07\x08\t\n\x0b\xff\xc4\x00\xb5\x10\x00\x02\x01\x03\x03\x02\x04\x03\x05\x05\x04\x04\x00\x00\x01}\x01\x02\x03\x00\x04\x11\x05\x12!1A\x06\x13Qa\x07"q\x142\x81\x91\xa1\x08#B\xb1\xc1\x15R\xd1\xf0$3br\x82\t\n\x16\x17\x18\x19\x1a%&\'()*456789:CDEFGHIJSTUVWXYZcdefghijstuvwxyz\x83\x84\x85\x86\x87\x88\x89\x8a\x92\x93\x94\x95\x96\x97\x98\x99\x9a\xa2\xa3\xa4\xa5\xa6\xa7\xa8\xa9\xaa\xb2\xb3\xb4\xb5\xb6\xb7\xb8\xb9\xba\xc2\xc3\xc4\xc5\xc6\xc7\xc8\xc9\xca\xd2\xd3\xd4\xd5\xd6\xd7\xd8\xd9\xda\xe1\xe2\xe3\xe4\xe5\xe6\xe7\xe8\xe9\xea\xf1\xf2\xf3\xf4\xf5\xf6\xf7\xf8\xf9\xfa\xff\xda\x00\x08\x01\x01\x00\x00?\x00\xfb\xd5\xff\xd9'

    # Create image
    img = Image.new('RGB', (WIDTH, HEIGHT), color=(25, 35, 50))
    draw = ImageDraw.Draw(img)

    # Try to load fonts
    try:
        weight_font = ImageFont.truetype("consola.ttf", 96)
        unit_font = ImageFont.truetype("arial.ttf", 48)
        status_font = ImageFont.truetype("arialbd.ttf", 32)
        label_font = ImageFont.truetype("arial.ttf", 24)
    except:
        weight_font = ImageFont.load_default()
        unit_font = weight_font
        status_font = weight_font
        label_font = weight_font

    # Draw weight
    weight_text = current_weight
    bbox = draw.textbbox((0, 0), weight_text, font=weight_font)
    text_width = bbox[2] - bbox[0]
    text_height = bbox[3] - bbox[1]
    x = (WIDTH - text_width) // 2
    y = (HEIGHT - text_height) // 2 - 50

    # Shadow
    draw.text((x + 3, y + 3), weight_text, fill=(0, 0, 0), font=weight_font)
    # Main text
    draw.text((x, y), weight_text, fill=(255, 255, 255), font=weight_font)

    # Unit
    draw.text((x + text_width + 20, y + text_height // 2), current_unit, fill=(180, 180, 180), font=unit_font)

    # Status
    status_color = {
        'STABLE': (50, 255, 50),
        'MOTION': (255, 255, 0),
        'ERROR': (255, 50, 50),
        'NO DATA': (128, 128, 128)
    }.get(current_status, (255, 255, 255))

    status_bbox = draw.textbbox((0, 0), current_status, font=status_font)
    status_width = status_bbox[2] - status_bbox[0]
    status_x = (WIDTH - status_width) // 2
    status_y = y + text_height + 40

    # Status background
    draw.rectangle([status_x - 15, status_y - 5, status_x + status_width + 15, status_y + 35], fill=(50, 55, 65))
    draw.text((status_x, status_y), current_status, fill=status_color, font=status_font)

    # Scale ID
    draw.text((15, 15), f"Scale: {scale_id}", fill=(128, 128, 128), font=label_font)

    # Time
    time_str = time.strftime("%H:%M:%S")
    time_bbox = draw.textbbox((0, 0), time_str, font=label_font)
    draw.text((WIDTH - time_bbox[2] - 15, 15), time_str, fill=(128, 128, 128), font=label_font)

    # Bottom bar
    draw.rectangle([0, HEIGHT - 45, WIDTH, HEIGHT], fill=(35, 45, 60))
    draw.text((15, HEIGHT - 38), "Cloud-Scale Weight Stream", fill=(100, 100, 100), font=label_font)

    # Live indicator
    draw.ellipse([WIDTH - 85, HEIGHT - 33, WIDTH - 73, HEIGHT - 21], fill=(255, 0, 0))
    draw.text((WIDTH - 65, HEIGHT - 38), "LIVE", fill=(255, 255, 255), font=label_font)

    # Convert to JPEG
    buffer = io.BytesIO()
    img.save(buffer, format='JPEG', quality=85)
    return buffer.getvalue()

class MJPEGHandler(http.server.BaseHTTPRequestHandler):
    """HTTP handler for MJPEG stream"""

    def log_message(self, format, *args):
        print(f"[{time.strftime('%H:%M:%S')}] {args[0]}")

    def do_GET(self):
        if self.path == '/stream' or self.path == '/mjpeg':
            self.send_mjpeg_stream()
        elif self.path == '/snapshot' or self.path == '/snapshot.jpg':
            self.send_snapshot()
        elif self.path == '/':
            self.send_html_page()
        else:
            self.send_error(404)

    def send_html_page(self):
        """Send HTML page with embedded stream"""
        html = f'''<!DOCTYPE html>
<html>
<head>
    <title>Scale Streamer</title>
    <style>
        body {{ background: #1a1a2e; color: white; font-family: Arial; text-align: center; margin: 20px; }}
        h1 {{ color: #4CAF50; }}
        img {{ border: 2px solid #333; border-radius: 8px; max-width: 100%; }}
        .info {{ margin: 20px; color: #888; }}
    </style>
</head>
<body>
    <h1>Cloud-Scale Weight Stream</h1>
    <img src="/stream" alt="Weight Stream">
    <div class="info">
        <p>Stream URL: http://localhost:{PORT}/stream</p>
        <p>Snapshot URL: http://localhost:{PORT}/snapshot</p>
    </div>
</body>
</html>'''
        self.send_response(200)
        self.send_header('Content-Type', 'text/html')
        self.end_headers()
        self.wfile.write(html.encode())

    def send_snapshot(self):
        """Send single JPEG frame"""
        frame = generate_frame()
        self.send_response(200)
        self.send_header('Content-Type', 'image/jpeg')
        self.send_header('Content-Length', len(frame))
        self.end_headers()
        self.wfile.write(frame)

    def send_mjpeg_stream(self):
        """Send MJPEG stream"""
        self.send_response(200)
        self.send_header('Content-Type', 'multipart/x-mixed-replace; boundary=frame')
        self.send_header('Cache-Control', 'no-cache')
        self.end_headers()

        frame_delay = 1.0 / FPS

        try:
            while True:
                frame = generate_frame()

                self.wfile.write(b'--frame\r\n')
                self.wfile.write(b'Content-Type: image/jpeg\r\n')
                self.wfile.write(f'Content-Length: {len(frame)}\r\n'.encode())
                self.wfile.write(b'\r\n')
                self.wfile.write(frame)
                self.wfile.write(b'\r\n')

                time.sleep(frame_delay)
        except (BrokenPipeError, ConnectionResetError):
            print("Client disconnected")

def weight_update_thread():
    """Background thread to update weight from logs"""
    while True:
        read_weight_from_logs()
        time.sleep(0.2)  # Update 5 times per second

def main():
    load_settings()

    # Start weight update thread
    update_thread = threading.Thread(target=weight_update_thread, daemon=True)
    update_thread.start()

    print(f"=" * 50)
    print(f"Cloud-Scale Weight Stream Server")
    print(f"=" * 50)
    print(f"")
    print(f"Stream URL: http://localhost:{PORT}/stream")
    print(f"Web View:   http://localhost:{PORT}/")
    print(f"Snapshot:   http://localhost:{PORT}/snapshot")
    print(f"")
    print(f"VLC: Media -> Open Network Stream -> http://localhost:{PORT}/stream")
    print(f"")

    with socketserver.TCPServer(("", PORT), MJPEGHandler) as httpd:
        httpd.serve_forever()

if __name__ == "__main__":
    main()
