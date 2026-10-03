param(
    [string]$Exe = "C:\Users\W\Downloads\火星文转换\build\火星文转换器.exe",
    [string]$Cli = "C:\Users\W\Downloads\火星文转换\build\hxw-cli.exe",
    [string]$OutDir = "C:\Users\W\Downloads\火星文转换\build\shots"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WinApi {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hWnd, EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
"@

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Get-Rect($h) {
    $r = New-Object WinApi+RECT
    [void][WinApi]::GetWindowRect($h, [ref]$r)
    return $r
}

function Save-Shot($h, $file) {
    $r = Get-Rect $h
    $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
    if ($w -le 0 -or $ht -le 0) { throw "窗口尺寸异常" }
    $bmp = New-Object System.Drawing.Bitmap($w, $ht)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [void][WinApi]::PrintWindow($h, $hdc, 2)
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "saved $file ($w x $ht)"
}

function Get-Edits($h) {
    $list = New-Object System.Collections.ArrayList
    $cb = [WinApi+EnumProc]{
        param($hWnd, $lParam)
        $sb = New-Object System.Text.StringBuilder 256
        [void][WinApi]::GetClassName($hWnd, $sb, 256)
        if ($sb.ToString() -like '*EDIT*') { [void]$list.Add($hWnd) }
        return $true
    }
    [void][WinApi]::EnumChildWindows($h, $cb, [IntPtr]::Zero)
    return $list
}

function Set-EditText($hEdit, $text) {
    [void][WinApi]::SendMessage($hEdit, 0x00B1, [IntPtr]::Zero, [IntPtr](-1))   # EM_SETSEL 全选
    [void][WinApi]::SendMessage($hEdit, 0x0303, [IntPtr]::Zero, [IntPtr]::Zero) # WM_CLEAR 删除
    foreach ($ch in $text.ToCharArray()) {
        [void][WinApi]::SendMessage($hEdit, 0x0102, [IntPtr][int]$ch, [IntPtr]::Zero)  # WM_CHAR
    }
    Start-Sleep -Milliseconds 900
}

$proc = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Milliseconds 3000
$proc.Refresh()
if ($proc.HasExited) { throw "程序已退出，退出码 $($proc.ExitCode)" }
$h = $proc.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { throw "未取到窗口句柄" }
[void][WinApi]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 600

$r = Get-Rect $h
Write-Host "窗口: $($r.Right - $r.Left) x $($r.Bottom - $r.Top)"

$edits = Get-Edits $h
Write-Host "找到文本框数量: $($edits.Count)"
if ($edits.Count -lt 2) { throw "未找到两个文本框" }
$leftEdit = $edits[0]; $rightEdit = $edits[1]

Save-Shot $h (Join-Path $OutDir '1-启动.png')

$cn = "我爱你，你是我心中最美的风景。今天的天气真不错，我们一起去公园散步吧！`r无论走到哪里，都要记得最初的梦想，愿你每天都开心快乐。"
Set-EditText $leftEdit $cn
Save-Shot $h (Join-Path $OutDir '2-中文转火星文.png')

# 生成对应火星文，再注入右侧文本框，验证反向还原
$tmpIn = Join-Path $env:TEMP 'hxw_in.txt'
$tmpOut = Join-Path $env:TEMP 'hxw_out.txt'
[System.IO.File]::WriteAllText($tmpIn, $cn, (New-Object System.Text.UTF8Encoding($false)))
& $Cli -cli hx $tmpIn $tmpOut
$hxText = [System.IO.File]::ReadAllText($tmpOut, [System.Text.Encoding]::UTF8)
Write-Host "火星文: $hxText"
Set-EditText $rightEdit $hxText
Save-Shot $h (Join-Path $OutDir '3-火星文转中文.png')

$null = $proc.CloseMainWindow()
Start-Sleep -Milliseconds 600
if (-not $proc.HasExited) { $proc.Kill() }
Write-Host "完成: $OutDir"
