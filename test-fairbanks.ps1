# Test Fairbanks FB6000 scale connection
# Try various Fairbanks protocol commands

$host_ip = "10.1.10.210"
$port = 5001

Write-Host "Testing Fairbanks FB6000 at ${host_ip}:${port}"

$client = New-Object System.Net.Sockets.TcpClient($host_ip, $port)
$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream)
$writer = New-Object System.IO.StreamWriter($stream)
$writer.AutoFlush = $true

Write-Host "Connected!"

# Fairbanks FB6000 uses specific commands
# Common Fairbanks commands with different line endings
$commands = @(
    @{cmd="W"; desc="Weight request"},
    @{cmd="`r"; desc="Carriage return only"},
    @{cmd="`n"; desc="Line feed only"},
    @{cmd="W`r"; desc="Weight with CR"},
    @{cmd="W`r`n"; desc="Weight with CRLF"},
    @{cmd="S"; desc="Stable weight"},
    @{cmd="P"; desc="Print"},
    @{cmd="Z"; desc="Zero"},
    @{cmd="T"; desc="Tare"},
    @{cmd="U"; desc="Units"},
    @{cmd="G"; desc="Gross"},
    @{cmd="N"; desc="Net"},
    @{cmd="?W"; desc="Query weight"},
    @{cmd="?S"; desc="Query status"}
)

foreach ($item in $commands) {
    Write-Host "`nSending: '$($item.cmd.Replace("`r", "\r").Replace("`n", "\n"))' ($($item.desc))"
    $writer.Write($item.cmd)
    $writer.Flush()
    Start-Sleep -Milliseconds 300

    if ($stream.DataAvailable) {
        $response = ""
        while ($stream.DataAvailable) {
            $byte = $stream.ReadByte()
            if ($byte -ge 32 -and $byte -le 126) {
                $response += [char]$byte
            } else {
                $response += "[0x$($byte.ToString('X2'))]"
            }
        }
        Write-Host "Response: $response"
    } else {
        Write-Host "No response"
    }
}

# Wait and check for streaming data
Write-Host "`n--- Waiting 5 seconds for continuous data ---"
$stream.ReadTimeout = 5000
try {
    for ($i = 0; $i -lt 5; $i++) {
        Start-Sleep -Seconds 1
        if ($stream.DataAvailable) {
            $response = $reader.ReadLine()
            Write-Host "Data[$i]: $response"
        } else {
            Write-Host "No data at second $i"
        }
    }
} catch {
    Write-Host "Read error: $_"
}

$client.Close()
Write-Host "`nDone"
