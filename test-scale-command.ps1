# Test direct connection to scale with command
$client = New-Object System.Net.Sockets.TcpClient("10.1.10.210", 5001)
$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream)
$writer = New-Object System.IO.StreamWriter($stream)
$writer.AutoFlush = $true

Write-Host "Connected!"

# Try sending common scale commands to request weight
$commands = @("W", "P", "S", "?", "IP", "G", "SI", "READ")
foreach ($cmd in $commands) {
    Write-Host "Sending: $cmd"
    $writer.WriteLine($cmd)
    Start-Sleep -Milliseconds 500

    # Check for any data available
    if ($stream.DataAvailable) {
        $response = $reader.ReadLine()
        Write-Host "Response: $response"
    } else {
        Write-Host "No response"
    }
}

# Also try just waiting for data
Write-Host "`nWaiting 3 seconds for data..."
Start-Sleep 3

if ($stream.DataAvailable) {
    $response = $reader.ReadLine()
    Write-Host "Data: $response"
} else {
    Write-Host "Still no data"
}

$client.Close()
Write-Host "Done"
