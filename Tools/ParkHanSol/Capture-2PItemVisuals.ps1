param(
    [string]$ExecutablePath = "C:\Users\hanso\Desktop\LastJumpCrew_FeedbackCheck\LastJumpCrew.exe",
    [string]$OutputDirectory = "C:\Users\hanso\Desktop\LastJumpCrew_FeedbackCheck\VisualCapture"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PHSVisualCaptureNative {
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint flags);
  [DllImport("user32.dll")] public static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputUnion U; }
  [StructLayout(LayoutKind.Explicit)] public struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
  [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
}
'@

function Wait-ForLogPattern {
    param([string]$Path, [string]$Pattern, [int]$TimeoutSeconds = 90)
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $line = Select-String -LiteralPath $Path -Pattern $Pattern | Select-Object -Last 1
            if ($null -ne $line) { return $line.Line }
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for '$Pattern' in '$Path'."
}

function Capture-Window {
    param([System.Diagnostics.Process]$Process, [string]$Path)
    $Process.Refresh()
    $rect = New-Object PHSVisualCaptureNative+RECT
    if ($Process.MainWindowHandle -eq [IntPtr]::Zero -or ![PHSVisualCaptureNative]::GetWindowRect($Process.MainWindowHandle, [ref]$rect)) {
        throw "Window handle missing for process $($Process.Id)."
    }
    $width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    if (![PHSVisualCaptureNative]::PrintWindow($Process.MainWindowHandle, $hdc, 2)) {
        $graphics.ReleaseHdc($hdc)
        $graphics.Dispose(); $bitmap.Dispose()
        throw "PrintWindow failed for process $($Process.Id)."
    }
    $graphics.ReleaseHdc($hdc)
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
}

function Send-ForwardMove {
    param([System.Diagnostics.Process]$Process)
    $Process.Refresh()
    [PHSVisualCaptureNative]::SetForegroundWindow($Process.MainWindowHandle) | Out-Null
    $down = New-Object PHSVisualCaptureNative+INPUT; $down.type = 1; $down.U.ki.wVk = 0x57
    $up = New-Object PHSVisualCaptureNative+INPUT; $up.type = 1; $up.U.ki.wVk = 0x57; $up.U.ki.dwFlags = 0x0002
    $size = [Runtime.InteropServices.Marshal]::SizeOf([type][PHSVisualCaptureNative+INPUT])
    [PHSVisualCaptureNative]::SendInput(1, @($down), $size) | Out-Null
    Start-Sleep -Milliseconds 700
    [PHSVisualCaptureNative]::SendInput(1, @($up), $size) | Out-Null
}

$hostProcess = $null; $clientProcess = $null
try {
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $hostLog = Join-Path $OutputDirectory "host.log"; $clientLog = Join-Path $OutputDirectory "client.log"
    Remove-Item -LiteralPath $hostLog, $clientLog -Force -ErrorAction SilentlyContinue
    $common = @("-screen-fullscreen", "0", "-screen-width", "960", "-screen-height", "540", "-phsAutoItemScenario", "-phsVisualCapture")
    $hostArgs = @("-phsProfile", "visualh_$([Guid]::NewGuid().ToString('N').Substring(0,20))", "-phsAutoHost", "-phsAutoStartGame", "-phsAutoStartClients", "2", "-phsAutoStartTimeout", "90") + $common + @("-logFile", $hostLog)
    $hostProcess = Start-Process -FilePath $ExecutablePath -ArgumentList $hostArgs -PassThru
    $code = (Wait-ForLogPattern $hostLog "PHS_AUTO_HOST_READY code=") -replace ".*code=", ""
    $clientArgs = @("-phsProfile", "visualc_$([Guid]::NewGuid().ToString('N').Substring(0,20))", "-phsAutoJoin", $code) + $common + @("-logFile", $clientLog)
    $clientProcess = Start-Process -FilePath $ExecutablePath -ArgumentList $clientArgs -PassThru
    Wait-ForLogPattern $hostLog "PHS_ITEM_P0_BEGIN" | Out-Null
    Start-Sleep -Seconds 3
    [PHSVisualCaptureNative]::SetWindowPos($hostProcess.MainWindowHandle, [IntPtr]::Zero, 0, 0, 960, 540, 0x0040) | Out-Null
    [PHSVisualCaptureNative]::SetWindowPos($clientProcess.MainWindowHandle, [IntPtr]::Zero, 960, 0, 960, 540, 0x0040) | Out-Null
    foreach ($item in @("wrench", "fire_extinguisher", "battery_pack")) {
        Wait-ForLogPattern $hostLog "PHS_VISUAL_CAPTURE_ITEM_HELD item=$item" | Out-Null
        Start-Sleep -Milliseconds 500
        Send-ForwardMove $clientProcess
        Capture-Window $hostProcess (Join-Path $OutputDirectory "Host_Remote_${item}_moving.png")
        Capture-Window $clientProcess (Join-Path $OutputDirectory "Client_Local_${item}_moving.png")
        Write-Output "PHS_VISUAL_CAPTURE_SAVED item=$item"
    }
    Wait-ForLogPattern $hostLog "PHS_P0_RESULT PASS" 90 | Out-Null
    Write-Output "PHS_VISUAL_CAPTURE_PASS output=$OutputDirectory"
}
finally {
    foreach ($process in @($hostProcess, $clientProcess)) {
        if ($null -ne $process -and !$process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    }
}
