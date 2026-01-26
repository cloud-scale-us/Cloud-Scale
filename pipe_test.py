"""
Test Named Pipe connection using ctypes (no pywin32 required)
"""
import ctypes
from ctypes import wintypes
import json
import time

# Windows API constants
GENERIC_READ = 0x80000000
GENERIC_WRITE = 0x40000000
OPEN_EXISTING = 3
INVALID_HANDLE_VALUE = -1
FILE_FLAG_OVERLAPPED = 0x40000000

kernel32 = ctypes.windll.kernel32

def connect_to_pipe(pipe_name, timeout_ms=5000):
    """Connect to a named pipe"""
    full_pipe_name = f"\\\\.\\pipe\\{pipe_name}"

    print(f"Connecting to: {full_pipe_name}")

    # Wait for pipe to be available
    result = kernel32.WaitNamedPipeW(full_pipe_name, timeout_ms)
    if not result:
        error = ctypes.get_last_error()
        print(f"WaitNamedPipe failed with error: {error}")
        # Try anyway

    # Open the pipe
    handle = kernel32.CreateFileW(
        full_pipe_name,
        GENERIC_READ | GENERIC_WRITE,
        0,  # No sharing
        None,  # Default security
        OPEN_EXISTING,
        0,  # Normal attributes
        None  # No template
    )

    if handle == INVALID_HANDLE_VALUE:
        error = ctypes.get_last_error()
        raise OSError(f"CreateFile failed with error: {error}")

    print(f"Connected! Handle: {handle}")
    return handle

def read_from_pipe(handle, max_bytes=4096):
    """Read data from pipe"""
    buffer = ctypes.create_string_buffer(max_bytes)
    bytes_read = wintypes.DWORD()

    result = kernel32.ReadFile(
        handle,
        buffer,
        max_bytes,
        ctypes.byref(bytes_read),
        None  # No overlapped
    )

    if not result:
        error = ctypes.get_last_error()
        raise OSError(f"ReadFile failed with error: {error}")

    return buffer.raw[:bytes_read.value]

def write_to_pipe(handle, data):
    """Write data to pipe"""
    if isinstance(data, str):
        data = data.encode('utf-8')

    bytes_written = wintypes.DWORD()

    result = kernel32.WriteFile(
        handle,
        data,
        len(data),
        ctypes.byref(bytes_written),
        None
    )

    if not result:
        error = ctypes.get_last_error()
        raise OSError(f"WriteFile failed with error: {error}")

    return bytes_written.value

def close_pipe(handle):
    """Close pipe handle"""
    kernel32.CloseHandle(handle)

def main():
    pipe_name = "ScaleStreamerPipe"

    try:
        # Connect
        handle = connect_to_pipe(pipe_name)

        # Read welcome message
        print("\nReading welcome message...")
        data = read_from_pipe(handle)
        print(f"Received {len(data)} bytes")
        print(f"Raw hex: {data[:50].hex()}")

        # Try to decode as UTF-8
        try:
            text = data.decode('utf-8')
            print(f"Text: {text}")

            # Check for JSON
            if text.strip():
                try:
                    obj = json.loads(text.strip())
                    print(f"JSON: {json.dumps(obj, indent=2)}")
                except json.JSONDecodeError:
                    print("(Not valid JSON)")
        except UnicodeDecodeError as e:
            print(f"UTF-8 decode error: {e}")
            # Try with BOM handling
            if data.startswith(b'\xef\xbb\xbf'):
                text = data[3:].decode('utf-8')
                print(f"Text (after BOM): {text}")

        # Send a ping command
        print("\nSending ping command...")
        ping_cmd = json.dumps({"MessageType": "Ping", "Timestamp": time.time()}) + "\n"
        written = write_to_pipe(handle, ping_cmd)
        print(f"Wrote {written} bytes")

        # Read response
        print("\nReading response...")
        time.sleep(0.5)
        try:
            data = read_from_pipe(handle)
            print(f"Response {len(data)} bytes: {data}")
            if data:
                text = data.decode('utf-8', errors='replace')
                print(f"Text: {text}")
        except Exception as e:
            print(f"Read error: {e}")

        # Close
        close_pipe(handle)
        print("\nPipe closed successfully!")

    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()
