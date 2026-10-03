# 构建火星文转换器：生成数据 -> 生成图标 -> 用 .NET Framework 自带 csc 编译单文件 exe
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
$build = Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $build | Out-Null

# 1. 数据资源
if (-not (Test-Path (Join-Path $build 'dict.tsv')) -or -not (Test-Path (Join-Path $build 'smart.gz'))) {
    Write-Host '生成数据资源 ...'
    node (Join-Path $PSScriptRoot 'gen_data.mjs')
}

# 2. 图标
$ico = Join-Path $src 'app.ico'
if (-not (Test-Path $ico)) {
    Write-Host '生成图标 ...'
    $py = (Get-Command python -ErrorAction SilentlyContinue).Source
    if (-not $py) { $py = "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe" }
    & $py (Join-Path $PSScriptRoot 'make_icon.py')
}

# 3. 编译器
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw '找不到 .NET Framework 编译器 csc.exe' }
Write-Host "编译器: $csc"

$common = @(
    '/nologo', '/optimize+', '/platform:anycpu', '/warn:4',
    '/reference:System.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
    "/resource:$build\dict.tsv,dict.tsv",
    "/resource:$build\smart.gz,smart.gz"
)

$gui = Join-Path $build '火星文转换器.exe'
Write-Host "编译界面版: $gui"
& $csc @common /target:winexe /out:$gui /win32manifest:"$src\app.manifest" /win32icon:"$ico" `
    "$src\Conv.cs" "$src\MainForm.cs" "$src\Program.cs" "$src\TestCli.cs"
if ($LASTEXITCODE -ne 0) { throw "编译失败: $LASTEXITCODE" }

$cli = Join-Path $build 'hxw-cli.exe'
Write-Host "编译命令行版: $cli"
& $csc @common /target:exe /define:CLI /out:$cli "$src\Conv.cs" "$src\TestCli.cs"
if ($LASTEXITCODE -ne 0) { throw "编译失败: $LASTEXITCODE" }

Get-ChildItem $build -Filter *.exe | Select-Object Name, @{n = '大小KB'; e = { [math]::Round($_.Length / 1KB, 1) } }, LastWriteTime
