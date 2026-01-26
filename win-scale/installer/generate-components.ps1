# Generate WiX component definitions for all files in publish directories
param(
    [string]$ServicePublishDir,
    [string]$ConfigPublishDir,
    [string]$LauncherPublishDir,
    [string]$OutputFile = "GeneratedComponents.wxs"
)

$ErrorActionPreference = "Stop"

Write-Host "Generating WiX components..." -ForegroundColor Yellow

$xml = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
    <!-- Auto-generated component definitions for self-contained deployment -->

    <!-- ===================================================================== -->
    <!-- SERVICE RUNTIME COMPONENTS -->
    <!-- ===================================================================== -->
    <Fragment>
        <ComponentGroup Id="ServiceRuntimeComponents" Directory="ServiceFolder">
"@

# Get all Service files except the executable (which is in ServiceInstallComponents)
$serviceFiles = Get-ChildItem $ServicePublishDir -File | Where-Object { $_.Name -ne "ScaleStreamer.Service.exe" }
$componentId = 1000

foreach ($file in $serviceFiles) {
    $safeFileName = $file.Name -replace '[\.-]', '_'
    $guid = [System.Guid]::NewGuid().ToString().ToUpper()

    $xml += @"

            <Component Id="ServiceFile_$safeFileName" Guid="$guid">
                <File Source="`$(var.ServicePublishDir)\$($file.Name)" />
            </Component>
"@
    $componentId++
}

$xml += @"

        </ComponentGroup>
    </Fragment>

    <!-- ===================================================================== -->
    <!-- CONFIG RUNTIME COMPONENTS -->
    <!-- ===================================================================== -->
    <Fragment>
        <ComponentGroup Id="ConfigRuntimeComponents" Directory="ConfigFolder">
"@

# Get all Config files
$configFiles = Get-ChildItem $ConfigPublishDir -File
$componentId = 2000

foreach ($file in $configFiles) {
    $safeFileName = $file.Name -replace '[\.-]', '_'
    $guid = [System.Guid]::NewGuid().ToString().ToUpper()

    # Desktop shortcut is created separately in main WXS file - don't duplicate here
    if ($file.Name -eq "ScaleStreamer.Config.exe") {
        $xml += @"

            <Component Id="ConfigFile_$safeFileName" Guid="$guid">
                <File Id="ConfigExe" Source="`$(var.ConfigPublishDir)\$($file.Name)" />
            </Component>
"@
    } else {
        $xml += @"

            <Component Id="ConfigFile_$safeFileName" Guid="$guid">
                <File Source="`$(var.ConfigPublishDir)\$($file.Name)" />
            </Component>
"@
    }
    $componentId++
}

$xml += @"

        </ComponentGroup>
    </Fragment>

    <!-- ===================================================================== -->
    <!-- LAUNCHER RUNTIME COMPONENTS -->
    <!-- ===================================================================== -->
    <Fragment>
        <ComponentGroup Id="LauncherRuntimeComponents" Directory="LauncherFolder">
"@

# Get all Launcher files
$launcherFiles = Get-ChildItem $LauncherPublishDir -File
$componentId = 3000

foreach ($file in $launcherFiles) {
    $safeFileName = $file.Name -replace '[\.-]', '_'
    $guid = [System.Guid]::NewGuid().ToString().ToUpper()

    $xml += @"

            <Component Id="LauncherFile_$safeFileName" Guid="$guid">
                <File Source="`$(var.LauncherPublishDir)\$($file.Name)" />
            </Component>
"@
    $componentId++
}

$xml += @"

        </ComponentGroup>
    </Fragment>
</Wix>
"@

# Write to file
$outputPath = Join-Path $PSScriptRoot $OutputFile
Set-Content -Path $outputPath -Value $xml -Encoding UTF8

Write-Host "Generated $($serviceFiles.Count) service components" -ForegroundColor Green
Write-Host "Generated $($configFiles.Count) config components" -ForegroundColor Green
Write-Host "Generated $($launcherFiles.Count) launcher components" -ForegroundColor Green
Write-Host "Output: $outputPath" -ForegroundColor Cyan
