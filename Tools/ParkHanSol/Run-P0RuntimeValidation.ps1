param(
    [string]$ExecutablePath = "Builds/PHS0715Validation/LastJumpCrew.exe",
    [int]$HostReadyTimeoutSeconds = 90,
    [int]$ScenarioTimeoutSeconds = 720
)

$ErrorActionPreference = "Stop"
$workspace = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$executable = (Resolve-Path (Join-Path $workspace $ExecutablePath)).Path
$logDirectory = Join-Path $workspace "Builds\PHS0715Validation"
$hostLog = Join-Path $logDirectory "p0-host.log"
$clientLog = Join-Path $logDirectory "p0-client.log"
$hostProcess = $null
$clientProcess = $null

function Wait-ForLogPattern {
    param(
        [string]$Path,
        [string]$Pattern,
        [int]$TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $match = Select-String -LiteralPath $Path -Pattern $Pattern | Select-Object -Last 1
            if ($null -ne $match) {
                return $match.Line
            }
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for pattern '$Pattern' in '$Path'."
}

try {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    Remove-Item -LiteralPath $hostLog, $clientLog -Force -ErrorAction SilentlyContinue

    $hostArguments = @(
        "-batchmode", "-nographics",
        "-phsProfile", "p0h_$([Guid]::NewGuid().ToString('N').Substring(0, 20))",
        "-phsAutoHost", "-phsAutoStartGame",
        "-phsAutoStartClients", "2",
        "-phsAutoStartTimeout", "90",
        "-phsAutoP0Scenario",
        "-logFile", $hostLog
    )
    $hostProcess = Start-Process -FilePath $executable -ArgumentList $hostArguments -PassThru -WindowStyle Hidden

    $readyLine = Wait-ForLogPattern -Path $hostLog -Pattern "PHS_AUTO_HOST_(READY code=|FAILED)" -TimeoutSeconds $HostReadyTimeoutSeconds
    if ($readyLine -match "PHS_AUTO_HOST_FAILED") {
        throw "Host automation failed. See '$hostLog'."
    }
    $joinCode = ($readyLine -split "code=")[-1].Trim()
    if ([string]::IsNullOrWhiteSpace($joinCode)) {
        throw "Host log did not contain a usable join code."
    }

    $clientArguments = @(
        "-batchmode", "-nographics",
        "-phsProfile", "p0c_$([Guid]::NewGuid().ToString('N').Substring(0, 20))",
        "-phsAutoJoin", $joinCode,
        "-phsAutoP0Scenario",
        "-logFile", $clientLog
    )
    $clientProcess = Start-Process -FilePath $executable -ArgumentList $clientArguments -PassThru -WindowStyle Hidden

    $resultLine = Wait-ForLogPattern -Path $hostLog -Pattern "PHS_P0_RESULT (PASS|FAIL)" -TimeoutSeconds $ScenarioTimeoutSeconds
    Write-Output $resultLine

    if ($resultLine -notmatch "PHS_P0_RESULT PASS") {
        Write-Output "Host log: $hostLog"
        Write-Output "Client log: $clientLog"
        exit 1
    }

    exit 0
}
finally {
    foreach ($process in @($hostProcess, $clientProcess)) {
        if ($null -ne $process -and !$process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
