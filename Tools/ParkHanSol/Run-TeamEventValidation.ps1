param(
    [string]$ExecutablePath = "Builds/PHS0717Validation/LastJumpCrew.exe",
    [int]$HostReadyTimeoutSeconds = 90,
    [int]$ScenarioTimeoutSeconds = 240
)

$ErrorActionPreference = "Stop"
$workspace = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$executable = (Resolve-Path (Join-Path $workspace $ExecutablePath)).Path
$logDirectory = Split-Path -Parent $executable
$hostLog = Join-Path $logDirectory "team-events-host.log"
$clientLog = Join-Path $logDirectory "team-events-client.log"
$hostProcess = $null
$clientProcess = $null

function Wait-ForLogPattern {
    param([string]$Path, [string]$Pattern, [int]$TimeoutSeconds)

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $match = Select-String -LiteralPath $Path -Pattern $Pattern | Select-Object -Last 1
            if ($null -ne $match) { return $match.Line }
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for pattern '$Pattern' in '$Path'."
}

try {
    Remove-Item -LiteralPath $hostLog, $clientLog -Force -ErrorAction SilentlyContinue
    $common = @("-batchmode", "-nographics", "-phsAutoStartClients", "2", "-phsTeamEventScenario")
    $hostArgs = @(
        "-phsProfile", "teamh_$([Guid]::NewGuid().ToString('N').Substring(0, 20))",
        "-phsAutoHost", "-phsAutoStartGame", "-phsAutoStartTimeout", "90"
    ) + $common + @("-logFile", $hostLog)
    $hostProcess = Start-Process -FilePath $executable -ArgumentList $hostArgs -PassThru -WindowStyle Hidden

    $readyLine = Wait-ForLogPattern $hostLog "PHS_AUTO_HOST_(READY code=|FAILED)" $HostReadyTimeoutSeconds
    if ($readyLine -match "PHS_AUTO_HOST_FAILED") {
        throw "Host automation failed. See '$hostLog'."
    }

    $joinCode = ($readyLine -split "code=")[-1].Trim()
    if ([string]::IsNullOrWhiteSpace($joinCode)) {
        throw "Host log did not contain a usable join code."
    }

    $clientArgs = @(
        "-phsProfile", "teamc_$([Guid]::NewGuid().ToString('N').Substring(0, 20))",
        "-phsAutoJoin", $joinCode
    ) + $common + @("-logFile", $clientLog)
    $clientProcess = Start-Process -FilePath $executable -ArgumentList $clientArgs -PassThru -WindowStyle Hidden

    $resultLine = Wait-ForLogPattern $hostLog "PHS_TEAM_EVENT_RESULT (PASS|FAIL)" $ScenarioTimeoutSeconds
    Write-Output $resultLine
    if ($resultLine -notmatch "PHS_TEAM_EVENT_RESULT PASS") {
        exit 1
    }

    $failurePattern = "NullReferenceException|MissingReferenceException|PHS_EVENT_TERMINAL_IMPACT_FAILED|PHS_EVENT_MINIGAME_RESULT_REJECTED|PHS_TOOL_BOX_NETWORK_SYNC_FAILED|PHS_MAP_RUNTIME_BIND_FAILED|Send error on connection.*send queue full"
    $failure = Select-String -LiteralPath @($hostLog, $clientLog) -Pattern $failurePattern |
        Select-Object -First 1
    if ($null -ne $failure) {
        Write-Output "PHS_TEAM_EVENT_LOG_HEALTH_FAIL $($failure.Path):$($failure.LineNumber) $($failure.Line.Trim())"
        exit 1
    }

    Write-Output "PHS_TEAM_EVENT_LOG_HEALTH_OK"
}
finally {
    foreach ($process in @($hostProcess, $clientProcess)) {
        if ($null -ne $process -and !$process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
