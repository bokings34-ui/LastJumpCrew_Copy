$sourcePath = Join-Path $PSScriptRoot "Run-TeamEventValidation.ps1"
$temporaryPath = Join-Path $PSScriptRoot "Run-SpecialItemValidation.generated.ps1"
$source = Get-Content -LiteralPath $sourcePath -Raw
$source = $source.Replace("-phsTeamEventScenario", "-phsSpecialItemScenario")
$source = $source.Replace("team-events-host.log", "special-items-host.log")
$source = $source.Replace("team-events-client.log", "special-items-client.log")
$source = $source.Replace("PHS_TEAM_EVENT_RESULT", "PHS_SPECIAL_ITEM_RESULT")
$source = $source.Replace("PHS_TEAM_EVENT_LOG_HEALTH_OK", "PHS_SPECIAL_ITEM_LOG_HEALTH_OK")
Set-Content -LiteralPath $temporaryPath -Value $source -Encoding utf8
$scenarioExitCode = 0
try {
    & $temporaryPath
    if ($null -ne $LASTEXITCODE) {
        $scenarioExitCode = $LASTEXITCODE
    }
}
catch {
    Write-Error $_
    $scenarioExitCode = 1
}
finally {
    Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
}

exit $scenarioExitCode
