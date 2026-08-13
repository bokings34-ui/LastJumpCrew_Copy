param([string]$ExecutablePath = "Builds/PHS0717Validation/LastJumpCrew.exe")

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$exe = (Resolve-Path (Join-Path $root $ExecutablePath)).Path
$hostLog = Join-Path (Split-Path $exe) "restart-host.log"
$clientLog = Join-Path (Split-Path $exe) "restart-client.log"
$hostProcess = $null
$clientProcess = $null

function Wait-Line([string]$Path, [string]$Pattern, [int]$Seconds) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path $Path) {
            $line = Select-String $Path -Pattern $Pattern | Select-Object -Last 1
            if ($line) { return $line.Line }
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out: $Pattern"
}

try {
    Remove-Item $hostLog, $clientLog -Force -ErrorAction SilentlyContinue
    $hostProcess = Start-Process $exe -ArgumentList @(
        "-phsProfile", "restarth_$([guid]::NewGuid().ToString('N').Substring(0,16))",
        "-phsAutoHost", "-phsAutoStartGame", "-phsAutoStartClients", "2",
        "-phsAutoStartTimeout", "90", "-phsNetworkRunRestartValidation", "success",
        "-logFile", $hostLog) -PassThru -WindowStyle Hidden
    $ready = Wait-Line $hostLog "PHS_AUTO_HOST_READY code=" 90
    $code = ($ready -split "code=")[-1].Trim()
    $clientProcess = Start-Process $exe -ArgumentList @(
        "-phsProfile", "restartc_$([guid]::NewGuid().ToString('N').Substring(0,16))",
        "-phsAutoJoin", $code, "-phsNetworkRunRestartValidation", "success",
        "-logFile", $clientLog) -PassThru -WindowStyle Hidden
    $hostResult = Wait-Line $hostLog "PHS_NETWORK_RUN_RESTART_VALIDATION (COMPLETE result=PASS|FAIL)" 180
    $clientResult = Wait-Line $clientLog "PHS_NETWORK_RUN_RESTART_VALIDATION (COMPLETE result=PASS|FAIL)" 180
    $hostResult
    $clientResult
    $validationFailed = $hostResult -match "FAIL" -or $clientResult -match "FAIL"

    $healthPatterns = @(
        @{ Name = "NullReferenceException"; Pattern = "NullReferenceException" },
        @{ Name = "MissingReferenceException"; Pattern = "MissingReferenceException" },
        @{ Name = "InvalidOperationException"; Pattern = "InvalidOperationException" },
        @{ Name = "NetworkPrefabHashMissing"; Pattern = "NetworkPrefab hash was not found" },
        @{ Name = "DuplicateSpawn"; Pattern = "Cannot process spawn.*already spawned" },
        @{ Name = "ScenePlacedObjectDuplicate"; Pattern = "ScenePlacedObjects which already contains" },
        @{ Name = "RuntimeContractFailed"; Pattern = "PHS_[A-Z0-9_]+FAILED" }
    )
    $healthFailures = @()
    foreach ($log in @(
        @{ Name = "host"; Path = $hostLog },
        @{ Name = "client"; Path = $clientLog }
    )) {
        foreach ($healthPattern in $healthPatterns) {
            $match = Select-String -LiteralPath $log.Path -Pattern $healthPattern.Pattern |
                Select-Object -First 1
            if ($null -ne $match) {
                $healthFailures += [PSCustomObject]@{
                    Log = $log.Name
                    Pattern = $healthPattern.Name
                    LineNumber = $match.LineNumber
                    Evidence = $match.Line.Trim()
                }
            }
        }
    }

    if ($healthFailures.Count -gt 0) {
        foreach ($failure in $healthFailures) {
            "PHS_RESTART_LOG_HEALTH_FAIL log=$($failure.Log) pattern=$($failure.Pattern) line=$($failure.LineNumber) evidence=$($failure.Evidence)"
        }
        exit 1
    }

    "PHS_RESTART_LOG_HEALTH_OK"
    if ($validationFailed) { exit 1 }
    "PHS_RESTART_VALIDATION_PASS peers=2"
}
finally {
    foreach ($process in @($hostProcess, $clientProcess)) {
        if ($process -and !$process.HasExited) { Stop-Process $process.Id -Force -ErrorAction SilentlyContinue }
    }
}
