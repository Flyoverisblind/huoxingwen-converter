# 构建安卓 APK（不依赖 Gradle，直接用 SDK 里的 aapt2 / javac / d8 / zipalign / apksigner）
# 注意：aapt2 对中文路径支持不好，所以先在纯英文的临时目录里构建，最后把 APK 拷回 build\
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$sdk = "$env:LOCALAPPDATA\Android\Sdk"
if (-not (Test-Path $sdk)) { $sdk = $env:ANDROID_HOME }
$bt = Get-ChildItem "$sdk\build-tools" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$platform = Get-ChildItem "$sdk\platforms" -Directory | Sort-Object Name -Descending | Select-Object -First 1
if (-not $bt -or -not $platform) { throw "未找到 Android SDK 的 build-tools / platforms，请先安装" }

$androidJar = Join-Path $platform.FullName 'android.jar'
$aapt2 = Join-Path $bt.FullName 'aapt2.exe'
$d8 = Join-Path $bt.FullName 'd8.bat'
$zipalign = Join-Path $bt.FullName 'zipalign.exe'
$apksigner = Join-Path $bt.FullName 'apksigner.bat'
$javac = (Get-Command javac).Source
$jar = Join-Path (Split-Path $javac) 'jar.exe'
Write-Host "build-tools: $($bt.Name)   platform: $($platform.Name)"

# 纯英文工作目录
$work = Join-Path $env:TEMP 'hxw_apk_build'
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $work, "$work\assets", "$work\classes", "$work\gen" | Out-Null
Copy-Item "$root\android\app\res" "$work\res" -Recurse -Force
Copy-Item "$root\android\app\AndroidManifest.xml" "$work\AndroidManifest.xml" -Force
Copy-Item "$root\android\app\java" "$work\java" -Recurse -Force

# 网页版页面作为 assets 打包（三个平台共用同一份转换逻辑）
$html = Join-Path $root 'build\火星文转换器.html'
if (-not (Test-Path $html)) { throw "缺少 build\火星文转换器.html，请先运行 node tools/gen_html.mjs" }
Copy-Item $html "$work\assets\index.html" -Force
Write-Host "assets/index.html: $([math]::Round((Get-Item "$work\assets\index.html").Length/1KB,1)) KB"

Write-Host '编译资源 ...'
& $aapt2 compile --dir "$work\res" -o "$work\res.zip"
if ($LASTEXITCODE -ne 0) { throw 'aapt2 compile 失败' }

Write-Host '链接资源 ...'
& $aapt2 link -o "$work\app-unsigned.apk" -I $androidJar --manifest "$work\AndroidManifest.xml" `
    "$work\res.zip" --java "$work\gen" -A "$work\assets" `
    --min-sdk-version 21 --target-sdk-version 34 --version-code 1 --version-name 1.0
if ($LASTEXITCODE -ne 0) { throw 'aapt2 link 失败' }

Write-Host '编译 Java ...'
$srcs = @()
$srcs += (Get-ChildItem "$work\java" -Recurse -Filter *.java | ForEach-Object { $_.FullName })
$srcs += (Get-ChildItem "$work\gen" -Recurse -Filter *.java | ForEach-Object { $_.FullName })
& $javac -encoding UTF-8 -nowarn -source 8 -target 8 -bootclasspath $androidJar -classpath $androidJar -d "$work\classes" $srcs
if ($LASTEXITCODE -ne 0) { throw 'javac 失败' }

Write-Host '生成 classes.dex ...'
New-Item -ItemType Directory -Force -Path "$work\dex" | Out-Null
& $jar cf "$work\classes.jar" -C "$work\classes" .
& $d8 --release --min-api 21 --lib $androidJar --output "$work\dex" "$work\classes.jar"
if ($LASTEXITCODE -ne 0) { throw 'd8 失败' }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$dexBytes = [System.IO.File]::ReadAllBytes("$work\dex\classes.dex")
$zip = [System.IO.Compression.ZipFile]::Open("$work\app-unsigned.apk", [System.IO.Compression.ZipArchiveMode]::Update)
try {
    $old = $zip.GetEntry('classes.dex')
    if ($old) { $old.Delete() }
    $entry = $zip.CreateEntry('classes.dex', [System.IO.Compression.CompressionLevel]::Optimal)
    $es = $entry.Open()
    $es.Write($dexBytes, 0, $dexBytes.Length)
    $es.Close()
} finally { $zip.Dispose() }
Write-Host "classes.dex: $([math]::Round($dexBytes.Length/1KB,1)) KB"

Write-Host '对齐并签名 ...'
& $zipalign -f -p 4 "$work\app-unsigned.apk" "$work\app-aligned.apk"
if ($LASTEXITCODE -ne 0) { throw 'zipalign 失败' }

$ksDir = Join-Path $root 'android\keystore'
New-Item -ItemType Directory -Force -Path $ksDir | Out-Null
$ks = Join-Path $ksDir 'hxw-release.jks'
$pass = 'huoxingwen'
if (-not (Test-Path $ks)) {
    Write-Host '生成签名证书（自签名，测试用）...'
    & (Get-Command keytool).Source -genkeypair -keystore $ks -alias hxw -keyalg RSA -keysize 2048 `
        -validity 10950 -storepass $pass -keypass $pass `
        -dname "CN=HuoXingWen Converter, OU=Dev, O=HuoXingWen, L=Beijing, ST=Beijing, C=CN"
    if ($LASTEXITCODE -ne 0) { throw 'keytool 失败' }
}

$apk = Join-Path $root 'build\火星文转换器-v1.0-android.apk'
Remove-Item $apk -Force -ErrorAction SilentlyContinue
& $apksigner sign --ks $ks --ks-key-alias hxw --ks-pass "pass:$pass" --key-pass "pass:$pass" --out "$work\signed.apk" "$work\app-aligned.apk"
if ($LASTEXITCODE -ne 0) { throw 'apksigner 失败' }
Copy-Item "$work\signed.apk" $apk -Force

Write-Host ''
Write-Host '=== 签名校验 ==='
& $apksigner verify --print-certs $apk | Select-Object -First 5
Write-Host '=== 安装包信息 ==='
& $aapt2 dump badging $apk | Select-String -Pattern "package:|sdkVersion|targetSdkVersion|application-label|launchable-activity|uses-permission|application-icon-320"
Get-Item $apk | Select-Object Name, @{n = '大小KB'; e = { [math]::Round($_.Length / 1KB, 1) } }
