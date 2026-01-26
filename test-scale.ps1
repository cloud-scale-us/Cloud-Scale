# Test direct connection to scale
$client = New-Object System.Net.Sockets.TcpClient("10.1.10.210", 5001)
$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream)
Write-Host "Connected! Reading 5 lines..."
for ($i = 1; $i -le 5; $i++) {
    $line = $reader.ReadLine()
    Write-Host "[$i]: $line"
}
$client.Close()
Write-Host "Done"
