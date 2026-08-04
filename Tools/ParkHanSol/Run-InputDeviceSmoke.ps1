param(
    [string]$ExecutablePath = "Builds/PHS0717Validation/LastJumpCrew.exe",
    [int]$HostReadyTimeoutSeconds = 90,
    [int]$InputProbeSeconds = 10,
    [switch]$ManualInput
)

$ErrorActionPreference = "Stop"
$workspace = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$executable = (Resolve-Path (Join-Path $workspace $ExecutablePath)).Path
$logDirectory = Split-Path -Parent $executable
$hostLog = Join-Path $logDirectory "input-host.log"
$clientLog = Join-Path $logDirectory "input-client.log"
$hostProcess = $null
$clientProcess = $null

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PHSInputDeviceSmokeNative {
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
  [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
  [DllImport("user32.dll")] public static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
}
'@

function Wait-ForLogPattern {
    param([string]$Path, [string]$Pattern, [int]$TimeoutSeconds)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $match = Select-String -LiteralPath $Path -Pattern $Pattern | Select-Object -Last 1
            if ($null -ne $match) { return $match.Line }
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for '$Pattern' in '$Path'."
}

function Send-PhysicalInputSmoke {
    param([System.Diagnostics.Process]$Process, [string]$Role)
    $Process.Refresh()
    if ($Process.MainWindowHandle -eq [IntPtr]::Zero) { throw "PHS_INPUT_SMOKE_FAIL role=$Role reason=window_handle_missing" }
    [PHSInputDeviceSmokeNative]::ShowWindowAsync($Process.MainWindowHandle, 9) | Out-Null
    [PHSInputDeviceSmokeNative]::SetForegroundWindow($Process.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
    $down = New-Object PHSInputDeviceSmokeNative+INPUT
    $down.type = 1; $down.U.ki.wVk = 0x57
    $up = New-Object PHSInputDeviceSmokeNative+INPUT
    $up.type = 1; $up.U.ki.wVk = 0x57; $up.U.ki.dwFlags = 0x0002
    [PHSInputDeviceSmokeNative]::SendInput(1, @($down), [Runtime.InteropServices.Marshal]::SizeOf([type][PHSInputDeviceSmokeNative+INPUT])) | Out-Null
    for ($index = 0; $index -lt 20; $index++) {
        $mouse = New-Object PHSInputDeviceSmokeNative+INPUT
        $mouse.type = 0; $mouse.U.mi.dx = 12; $mouse.U.mi.dy = 3; $mouse.U.mi.dwFlags = 0x0001
        [PHSInputDeviceSmokeNative]::SendInput(1, @($mouse), [Runtime.InteropServices.Marshal]::SizeOf([type][PHSInputDeviceSmokeNative+INPUT])) | Out-Null
        Start-Sleep -Milliseconds 75
    }
    [PHSInputDeviceSmokeNative]::SendInput(1, @($up), [Runtime.InteropServices.Marshal]::SizeOf([type][PHSInputDeviceSmokeNative+INPUT])) | Out-Null
    Write-Output "PHS_INPUT_SMOKE_SENT role=$Role"
}

try {
    Remove-Item -LiteralPath $hostLog, $clientLog -Force -ErrorAction SilentlyContinue
    $common = @("-screen-fullscreen", "0", "-screen-width", "960", "-screen-height", "540", "-phsInputOnlyScenario", "-phsInputDeviceProbeSeconds", $InputProbeSeconds)
    $hostArguments = @("-phsProfile", "inputh_$([Guid]::NewGuid().ToString('N').Substring(0,20))", "-phsAutoHost", "-phsAutoStartGame", "-phsAutoStartClients", "2", "-phsAutoStartTimeout", "90") + $common + @("-logFile", $hostLog)
    $hostProcess = Start-Process -FilePath $executable -ArgumentList $hostArguments -PassThru
    $readyLine = Wait-ForLogPattern -Path $hostLog -Pattern "PHS_AUTO_HOST_(READY code=|FAILED)" -TimeoutSeconds $HostReadyTimeoutSeconds
    if ($readyLine -match "PHS_AUTO_HOST_FAILED") { throw "Host automation failed." }
    $joinCode = ($readyLine -split "code=")[-1].Trim()
    $clientArguments = @("-phsProfile", "inputc_$([Guid]::NewGuid().ToString('N').Substring(0,20))", "-phsAutoJoin", $joinCode) + $common + @("-logFile", $clientLog)
    $clientProcess = Start-Process -FilePath $executable -ArgumentList $clientArguments -PassThru
    Wait-ForLogPattern -Path $hostLog -Pattern "PHS_INPUT_SCENE_READY peers=2 scene=PHS_Map_ver1" -TimeoutSeconds 90 | Out-Null
    Wait-ForLogPattern -Path $clientLog -Pattern "PHS_PLAYER_SCENE_STATE scene=PHS_Map_ver1 owner=True input=True" -TimeoutSeconds 90 | Out-Null
    if ($ManualInput) {
        Write-Output "PHS_INPUT_MANUAL_READY role=host_and_client action=focus_each_window_then_press_W_and_move_mouse"
    }
    else {
        Send-PhysicalInputSmoke -Process $hostProcess -Role "host"
        Send-PhysicalInputSmoke -Process $clientProcess -Role "client"
    }
    $probeTimeoutSeconds = if ($ManualInput) { 180 } else { 45 }
    $hostProbe = Wait-ForLogPattern -Path $hostLog -Pattern "PHS_INPUT_DEVICE_PROBE.*pass=True" -TimeoutSeconds $probeTimeoutSeconds
    $clientProbe = Wait-ForLogPattern -Path $clientLog -Pattern "PHS_INPUT_DEVICE_PROBE.*pass=True" -TimeoutSeconds $probeTimeoutSeconds
    $remoteProbe = Wait-ForLogPattern -Path $hostLog -Pattern "PHS_INPUT_REMOTE_SYNC.*pass=True" -TimeoutSeconds $probeTimeoutSeconds
    Write-Output $hostProbe
    Write-Output $clientProbe
    Write-Output $remoteProbe
    Write-Output "PHS_INPUT_DEVICE_SMOKE_PASS"
}
finally {
    foreach ($process in @($hostProcess, $clientProcess)) {
        if ($null -ne $process -and !$process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    }
}
