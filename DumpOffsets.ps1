# DumpOffsets.ps1
# Run this AFTER Il2CppDumper has produced dump.cs and script.json in .\dump\
# It extracts the offsets needed for GameOffsets.cs and prints them ready to paste.

$dumpCs    = ".\dump\dump.cs"
$scriptJson = ".\dump\script.json"

if (-not (Test-Path $dumpCs)) {
    Write-Error "dump\dump.cs not found. Run Il2CppDumper first (see step 2 in README)."
    exit 1
}

Write-Host "`n=== Parsing dump.cs ===" -ForegroundColor Cyan

# Classes we care about
$targets = @("GridManager", "Room", "RoomAddressableData", "WallType")

$lines    = Get-Content $dumpCs
$inClass  = $null
$pending  = $null  # offset comment seen, waiting for the field line

foreach ($line in $lines) {
    $trimmed = $line.Trim()

    # Detect class start
    foreach ($t in $targets) {
        if ($trimmed -match "^\s*(public|private|internal).*class\s+$t(\s|:|$)") {
            $inClass = $t
            Write-Host "`n--- $t ---" -ForegroundColor Yellow
        }
    }
    # Detect class end (closing brace at class level)
    if ($inClass -and $trimmed -eq "}" ) { $inClass = $null }

    if ($inClass) {
        # Capture offset comment
        if ($trimmed -match "//\s*Offset:\s*(0x[0-9A-Fa-f]+)") {
            $pending = $matches[1]
        }
        # Field line following offset
        elseif ($pending -and $trimmed -match "(public|private|protected|internal)\s+\S+\s+(\w+)\s*;") {
            Write-Host ("  {0,-35} // Offset: {1}" -f $matches[2], $pending)
            $pending = $null
        }
        # Enum value
        elseif ($inClass -eq "WallType" -and $trimmed -match "(\w+)\s*=\s*(\d+)") {
            Write-Host ("  {0} = {1}" -f $matches[1], $matches[2])
        }
    }
}

Write-Host "`n=== Parsing script.json (static fields) ===" -ForegroundColor Cyan

if (-not (Test-Path $scriptJson)) {
    Write-Warning "dump\script.json not found — skipping static field addresses."
} else {
    $json = Get-Content $scriptJson -Raw | ConvertFrom-Json
    $keyNames = @("GridManager", "StatsLogger", "PlayMakerFSM")
    foreach ($entry in $json) {
        foreach ($kn in $keyNames) {
            if ($entry.Name -like "*$kn*") {
                Write-Host ("  0x{0:X}  {1}" -f $entry.Address, $entry.Name)
            }
        }
    }
}

Write-Host "`nDone. Copy the Offset values into ExternalReader\GameOffsets.cs" -ForegroundColor Green
