<#
.SYNOPSIS
    TabPaint 自动化发布脚本 (修复版：彻底分离运行库与程序文件)
#>


$SourceBuildDir = "E:\dev\TabPaint\TabPaint-main\TabPaint\bin\x64\Release\net8.0-windows10.0.19041.0"
$FinalOutputDir = "E:\dev\TabPaint\TabPaint_Resources"
$IssFilePath    = "E:\dev\TabPaint\TabPaint-main\TabPaint\Resources\TabPaint_Setup.iss"
$IsccPath       = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

# [关键修改] 程序纯净文件暂存区 (里面只放 TabPaint 的文件)
$StagingDir     = "E:\dev\TabPaint\TabPaint_Release" 

# [关键修改] 外部资源缓存区 (专门放下载的运行库，避免被打包进 Zip)
$ResourceCacheDir = "E:\dev\TabPaint\TabPaint_Resources"

# .NET 8 Desktop Runtime 下载配置
$RuntimeUrl      = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
$RuntimeFileName = "windowsdesktop-runtime-8.0-win-x64.exe"
$RuntimeLocalPath = Join-Path $ResourceCacheDir $RuntimeFileName



Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "      TabPaint 自动化发布 (目录分离版)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$Version = Read-Host "请输入本次发布的版本号 (例如 0.8.6)"
if ([string]::IsNullOrWhiteSpace($Version)) { exit }

# 1. 清理并重建暂存区 (确保里面没有旧的垃圾文件)
Write-Host "[1/6] 准备纯净的程序文件..." -ForegroundColor Yellow

New-Item -ItemType Directory -Path $StagingDir | Out-Null

# 2. 确保资源缓存区存在
if (-not (Test-Path $ResourceCacheDir)) { New-Item -ItemType Directory -Path $ResourceCacheDir | Out-Null }

# 3. 复制核心文件到暂存区
$TargetFiles = @("TabPaint.exe", "TabPaint.dll", "TabPaint.deps.json", "TabPaint.runtimeconfig.json")
foreach ($fileName in $TargetFiles) {
    Copy-Item -Path (Join-Path $SourceBuildDir $fileName) -Destination (Join-Path $StagingDir $fileName) -Force
}


Write-Host "[2/6] 生成纯净版 ZIP..." -ForegroundColor Yellow
$ZipOutputPath = Join-Path $FinalOutputDir "TabPaint_v$($Version)_Portable.zip"
if (Test-Path $ZipOutputPath) { Remove-Item $ZipOutputPath -Force }

# 只压缩 StagingDir，而运行库在 ResourceCacheDir，所以不会被压进去
Compress-Archive -Path "$StagingDir\*" -DestinationPath $ZipOutputPath -Force
Write-Host "   -> Zip 已生成 (体积应很小)" -ForegroundColor Green


Write-Host "[3/6] 检查运行库缓存..." -ForegroundColor Yellow

if (-not (Test-Path $RuntimeLocalPath)) {
    Write-Host "   -> 正在下载 .NET 8 运行库到缓存目录..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $RuntimeUrl -OutFile $RuntimeLocalPath
        Write-Host "   -> 下载完成。" -ForegroundColor Green
    }
    catch {
        Write-Host "   -> 下载失败，将跳过 Full 版生成。" -ForegroundColor Red
        $RuntimeLocalPath = $null # 标记失败
    }
} else {
    Write-Host "   -> 运行库已存在于缓存目录。" -ForegroundColor Green
}


Write-Host "[4/6] 编译 Lite 版本 (不含运行库)..." -ForegroundColor Yellow

$LiteArgs = @(
    "/O`"$FinalOutputDir`"",
    "/F`"TabPaint_Setup_v$($Version)_Lite`"",
    "/DMyAppVersion=`"$Version`"",
    "/DMySourcePath=`"$StagingDir\*`"",  # 指向纯净目录
    "`"$IssFilePath`""
)

Start-Process -FilePath $IsccPath -ArgumentList $LiteArgs -Wait -NoNewWindow
Write-Host "   -> Lite 版编译完成。" -ForegroundColor Green


if ($RuntimeLocalPath) {
    Write-Host "[5/6] 编译 Full 版本 (内嵌运行库)..." -ForegroundColor Yellow

    $FullArgs = @(
        "/O`"$FinalOutputDir`"",
        "/F`"TabPaint_Setup_v$($Version)_Full`"", 
        "/DMyAppVersion=`"$Version`"",
        "/DMySourcePath=`"$StagingDir\*`"",             # 指向纯净目录(程序本体)
        "/DUseBundledRuntime",                          # 开启集成模式
        "/DRuntimeInstallerPath=`"$RuntimeLocalPath`"", # 指向外部缓存目录(运行库)
        "`"$IssFilePath`""
    )

    Start-Process -FilePath $IsccPath -ArgumentList $FullArgs -Wait -NoNewWindow
    Write-Host "   -> Full 版编译完成。" -ForegroundColor Green
}

Write-Host "`n全部完成！" -ForegroundColor Cyan
Invoke-Item $FinalOutputDir
Pause
