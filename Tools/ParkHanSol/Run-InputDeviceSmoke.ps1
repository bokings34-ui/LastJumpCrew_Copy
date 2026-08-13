param(
    [string]$ExecutablePath = "Builds/PHS0717Validation/LastJumpCrew.exe",
    [int]$HostReadyTimeoutSeconds = 90,
    [int]$InputProbeSeconds = 10,
    [switch]$ManualInput
)

$ErrorActionPreference = "Stop"
$workspace = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$executable = if ([System.IO.Path]::IsPathRooted($ExecutablePath)) {
    (Resolve-Path $ExecutablePath).Path
}
else {
    (Resolve-Path (Join-Path $workspace $ExecutablePath)).Path
}
$logDirectory = Split-Path -Parent $executable
$hostLog = Join-Path $logDirectory "input-host.log"
$clientLog = Join-Path $logDirectory "input-client.log"
$hostProcess = $null
$clientProcess = $null

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PHSInputDeviceSmokeNative {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
  [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public HARDWAREINPUT hi; }
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public int time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public int time; public UIntPtr dwExtraInfo; }
  [StructLayout(LayoutKind.Sequential)] public struct HARDWAREINPUT { public int uMsg; public short wParamL; public short wParamH; }
  public static INPUT Keyboard(ushort scanCode, uint flags) {
    return new INPUT { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wScan = scanCode, dwFlags = flags } } };
  }
  public static INPUT Mouse(int dx, int dy, uint flags) {
    return new INPUT { type = 0, U = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = flags } } };
  }
  [DllImport("user32.dll", SetLastError=true)] public static extern uint SendInput(uint cInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
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

function Set-InputWindowForeground {
    param([System.Diagnostics.Process]$Process, [string]$Role)

    $targetWindow = $Process.MainWindowHandle
    $shell = New-Object -ComObject WScript.Shell
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        $shell.AppActivate($Process.Id) | Out-Null
        [PHSInputDeviceSmokeNative]::ShowWindowAsync($targetWindow, 9) | Out-Null

        $foregroundWindow = [PHSInputDeviceSmokeNative]::GetForegroundWindow()
        $currentThread = [PHSInputDeviceSmokeNative]::GetCurrentThreadId()
        $foregroundThread = [PHSInputDeviceSmokeNative]::GetWindowThreadProcessId(
            $foregroundWindow,
            [IntPtr]::Zero)
        $attached = $false
        if ($foregroundThread -ne 0 -and $foregroundThread -ne $currentThread) {
            $attached = [PHSInputDeviceSmokeNative]::AttachThreadInput(
                $currentThread,
                $foregroundThread,
                $true)
        }
        try {
            [PHSInputDeviceSmokeNative]::BringWindowToTop($targetWindow) | Out-Null
            [PHSInputDeviceSmokeNative]::SetForegroundWindow($targetWindow) | Out-Null
        }
        finally {
            if ($attached) {
                [PHSInputDeviceSmokeNative]::AttachThreadInput(
                    $currentThread,
                    $foregroundThread,
                    $false) | Out-Null
            }
        }

        Start-Sleep -Milliseconds 150
        if ([PHSInputDeviceSmokeNative]::GetForegroundWindow() -eq $targetWindow) {
            return $targetWindow
        }
    }

    $actualWindow = [PHSInputDeviceSmokeNative]::GetForegroundWindow()
    throw "PHS_INPUT_SMOKE_FAIL role=$Role reason=foreground_window_not_acquired attempts=4 target=$targetWindow actual=$actualWindow"
}

function Send-PhysicalInputSmoke {
    param([System.Diagnostics.Process]$Process, [string]$Role)
    $Process.Refresh()
    if ($Process.MainWindowHandle -eq [IntPtr]::Zero) { throw "PHS_INPUT_SMOKE_FAIL role=$Role reason=window_handle_missing" }

    $targetWindow = Set-InputWindowForeground -Process $Process -Role $Role
    Start-Sleep -Milliseconds 250

    $windowRect = New-Object PHSInputDeviceSmokeNative+RECT
    if (![PHSInputDeviceSmokeNative]::GetWindowRect($targetWindow, [ref]$windowRect)) {
        throw "PHS_INPUT_SMOKE_FAIL role=$Role reason=window_rect_unavailable"
    }
    [PHSInputDeviceSmokeNative]::SetCursorPos(
        [int](($windowRect.Left + $windowRect.Right) / 2),
        [int](($windowRect.Top + $windowRect.Bottom) / 2)) | Out-Null

    $inputSize = [Runtime.InteropServices.Marshal]::SizeOf([type][PHSInputDeviceSmokeNative+INPUT])
    if ($inputSize -ne 40) {
        throw "PHS_INPUT_SMOKE_FAIL role=$Role reason=input_struct_size_invalid actual=$inputSize expected=40"
    }
    # Match Unity Input System's WinUserInput test fixture: hardware scan code,
    # not a translated virtual-key message. W uses set-1 scan code 0x11.
    $down = [PHSInputDeviceSmokeNative]::Keyboard(0x11, 0x0008)
    $up = [PHSInputDeviceSmokeNative]::Keyboard(0x11, 0x000A)
    # Complete the surface-focus click before pressing W. Clicking after key-down
    # makes the Input System resync keyboard state and can discard that first press.
    foreach ($clickFlag in @(0x0002, 0x0004)) {
        $click = [PHSInputDeviceSmokeNative]::Mouse(0, 0, $clickFlag)
        $sent = [PHSInputDeviceSmokeNative]::SendInput(1, @($click), $inputSize)
        if ($sent -ne 1) {
            throw "PHS_INPUT_SMOKE_FAIL role=$Role reason=mouse_focus_click_failed flag=$clickFlag"
        }
    }
    Start-Sleep -Milliseconds 250
    if ([PHSInputDeviceSmokeNative]::GetForegroundWindow() -ne $targetWindow) {
        $targetWindow = Set-InputWindowForeground -Process $Process -Role $Role
        Start-Sleep -Milliseconds 250
    }
    $sent = [PHSInputDeviceSmokeNative]::SendInput(1, @($down), $inputSize)
    if ($sent -ne 1) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "PHS_INPUT_SMOKE_FAIL role=$Role reason=key_down_send_failed win32=$errorCode"
    }
    for ($index = 0; $index -lt 20; $index++) {
        $mouse = [PHSInputDeviceSmokeNative]::Mouse(12, 3, 0x2001)
        $sent = [PHSInputDeviceSmokeNative]::SendInput(1, @($mouse), $inputSize)
        if ($sent -ne 1) {
            $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
            throw "PHS_INPUT_SMOKE_FAIL role=$Role reason=mouse_send_failed index=$index win32=$errorCode"
        }
        Start-Sleep -Milliseconds 75
    }
    $sent = [PHSInputDeviceSmokeNative]::SendInput(1, @($up), $inputSize)
    if ($sent -ne 1) {
        $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "PHS_INPUT_SMOKE_FAIL role=$Role reason=key_up_send_failed win32=$errorCode"
    }
    Write-Output "PHS_INPUT_SMOKE_SENT role=$Role focusVerified=True sendCount=24"
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
    Wait-ForLogPattern -Path $hostLog -Pattern "PHS_INPUT_DEVICE_PROBE_ARMED ownerClientId=0" -TimeoutSeconds 90 | Out-Null
    Wait-ForLogPattern -Path $clientLog -Pattern "PHS_PLAYER_SCENE_STATE scene=PHS_Map_ver1 owner=True input=True" -TimeoutSeconds 90 | Out-Null
    Wait-ForLogPattern -Path $clientLog -Pattern "PHS_INPUT_DEVICE_PROBE_ARMED ownerClientId=1" -TimeoutSeconds 10 | Out-Null
    if ($ManualInput) {
        Write-Output "PHS_INPUT_MANUAL_READY role=host_and_client action=focus_each_window_then_press_W_and_move_mouse"
    }
    else {
        Send-PhysicalInputSmoke -Process $hostProcess -Role "host"
        $hostProbe = Wait-ForLogPattern -Path $hostLog -Pattern "PHS_INPUT_DEVICE_PROBE.*pass=True" -TimeoutSeconds 45
    }
    if (!$ManualInput) {
        Send-PhysicalInputSmoke -Process $clientProcess -Role "client"
    }
    $probeTimeoutSeconds = if ($ManualInput) { 180 } else { 45 }
    if ($ManualInput) {
        $hostProbe = Wait-ForLogPattern -Path $hostLog -Pattern "PHS_INPUT_DEVICE_PROBE.*pass=True" -TimeoutSeconds $probeTimeoutSeconds
    }
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
