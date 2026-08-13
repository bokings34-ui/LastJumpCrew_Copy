param(
    [string]$ExecutablePath = "Builds/PHS0717Validation/LastJumpCrew.exe",
    [ValidateRange(2, 8)]
    [int]$TotalPeers = 8,
    [int]$HostReadyTimeoutSeconds = 120,
    [int]$SessionReadyTimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
$workspace = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$executable = if ([IO.Path]::IsPathRooted($ExecutablePath)) {
    (Resolve-Path $ExecutablePath).Path
}
else {
    (Resolve-Path (Join-Path $workspace $ExecutablePath)).Path
}
$logDirectory = Split-Path -Parent $executable
$hostLog = Join-Path $logDirectory "session8-host.log"
$clientLogs = @()
$processes = @()

function Wait-ForLogPattern {
    param([string]$Path, [string]$Pattern, [int]$TimeoutSeconds)

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $match = Select-String -LiteralPath $Path -Pattern $Pattern | Select-Object -Last 1
            if ($null -ne $match) {
                return $match.Line
            }
        }
        Start-Sleep -Milliseconds 250
    }

    throw "PHS_8P_SMOKE_FAIL reason=log_timeout pattern='$Pattern' log='$Path'"
}

try {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    Remove-Item -LiteralPath $hostLog -Force -ErrorAction SilentlyContinue
    for ($index = 1; $index -lt $TotalPeers; $index++) {
        $clientLog = Join-Path $logDirectory ("session8-client-{0:D2}.log" -f $index)
        Remove-Item -LiteralPath $clientLog -Force -ErrorAction SilentlyContinue
        $clientLogs += $clientLog
    }

    $hostArguments = @(
        "-batchmode", "-nographics",
        "-phsProfile", "s8h_$([Guid]::NewGuid().ToString('N').Substring(0, 20))",
        "-phsAutoHost", "-phsAutoStartGame",
        "-phsAutoStartClients", $TotalPeers,
        "-phsAutoStartTimeout", $SessionReadyTimeoutSeconds,
        "-logFile", $hostLog
    )
    $hostProcess = Start-Process -FilePath $executable -ArgumentList $hostArguments -PassThru -WindowStyle Hidden
    $processes += $hostProcess

    $readyLine = Wait-ForLogPattern -Path $hostLog -Pattern "PHS_AUTO_HOST_(READY code=|FAILED)" -TimeoutSeconds $HostReadyTimeoutSeconds
    if ($readyLine -match "PHS_AUTO_HOST_FAILED") {
        throw "PHS_8P_SMOKE_FAIL reason=host_create_failed log='$hostLog'"
    }
    $joinCode = ($readyLine -split "code=")[-1].Trim()
    if ([string]::IsNullOrWhiteSpace($joinCode)) {
        throw "PHS_8P_SMOKE_FAIL reason=join_code_missing log='$hostLog'"
    }

    for ($index = 0; $index -lt $clientLogs.Count; $index++) {
        $clientArguments = @(
            "-batchmode", "-nographics",
            "-phsProfile", "s8c$($index + 1)_$([Guid]::NewGuid().ToString('N').Substring(0, 16))",
            "-phsAutoJoin", $joinCode,
            "-logFile", $clientLogs[$index]
        )
        $clientProcess = Start-Process -FilePath $executable -ArgumentList $clientArguments -PassThru -WindowStyle Hidden
        $processes += $clientProcess
        Start-Sleep -Milliseconds 250
    }

    Wait-ForLogPattern -Path $hostLog -Pattern "PHS_AUTO_ROOM_COUNT clients=$TotalPeers/$TotalPeers" -TimeoutSeconds $SessionReadyTimeoutSeconds | Out-Null
    Wait-ForLogPattern -Path $hostLog -Pattern "PHS_PLAYER_SCENE_STATE scene=PHS_Map_ver1 owner=True input=True" -TimeoutSeconds $SessionReadyTimeoutSeconds | Out-Null
    foreach ($clientLog in $clientLogs) {
        Wait-ForLogPattern -Path $clientLog -Pattern "PHS_ROOM_JOIN_OK .*players=\d+/8" -TimeoutSeconds $SessionReadyTimeoutSeconds | Out-Null
        Wait-ForLogPattern -Path $clientLog -Pattern "PHS_PLAYER_SCENE_STATE scene=PHS_Map_ver1 owner=True input=True" -TimeoutSeconds $SessionReadyTimeoutSeconds | Out-Null
    }

    $escapedChannel = [Regex]::Escape($joinCode)
    foreach ($peerLog in @($hostLog) + $clientLogs) {
        Wait-ForLogPattern -Path $peerLog -Pattern "PHS_ROOM_VOICE_CHANNEL_JOINED channel=$escapedChannel " -TimeoutSeconds $SessionReadyTimeoutSeconds | Out-Null
        $voiceFailure = Select-String -LiteralPath $peerLog -Pattern "PHS_ROOM_VOICE_CHANNEL_FAILED|PHS_VOICE_CHANNEL_JOIN_FAILED" | Select-Object -First 1
        if ($null -ne $voiceFailure) {
            throw "PHS_8P_SMOKE_FAIL reason=voice_channel_failed log='$peerLog' evidence='$($voiceFailure.Line.Trim())'"
        }
    }

    $runtimeFailure = Select-String -LiteralPath (@($hostLog) + $clientLogs) -Pattern @(
        'NullReferenceException',
        'MissingReferenceException',
        'PHS_[A-Z0-9_]+_(FAILED|FAIL)',
        'Send error on connection.*send queue full'
    ) | Select-Object -First 1
    if ($null -ne $runtimeFailure) {
        throw "PHS_8P_SMOKE_FAIL reason=log_health path='$($runtimeFailure.Path)' line=$($runtimeFailure.LineNumber) evidence='$($runtimeFailure.Line.Trim())'"
    }

    Write-Output "PHS_8P_SESSION_PASS peers=$TotalPeers roomCapacity=8 gameplayOwners=$TotalPeers"
    Write-Output "PHS_8P_VOICE_CHANNEL_CONTRACT_OK peers=$TotalPeers channel=$joinCode actualAudio=NOT_TESTED"
    Write-Output "PHS_8P_LOGS host=$hostLog clients=$($clientLogs -join ',')"
}
finally {
    foreach ($process in $processes) {
        if ($null -ne $process -and !$process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
