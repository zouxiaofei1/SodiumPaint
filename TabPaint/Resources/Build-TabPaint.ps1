<#
.SYNOPSIS
    TabPaint 自动化发布脚本 (最终优化版)
    功能：指定文件覆盖 -> 生成 Zip 到输出目录 -> 编译 Setup 到输出目录
#>

# ==============================================================================
# 1. 配置区域
# ==============================================================================

# C# 项目编译后的输出目录
$SourceBuildDir = "E:\dev\TabPaint-mains\TabPaint\bin\Release\net8.0-windows10.0.19041.0"

# 缓存/暂存目录 (仅用于中转文件，ISS 读取这里)
$StagingDir     = "E:\dev\TabPaint_Release"

# 【新增】最终输出目录 (生成的 Zip 和 Setup.exe 都会放在这里)
$FinalOutputDir = "E:\dev"

# Inno Setup 脚本文件的完整路径
$IssFilePath    = "E:\dev\TabPaint-mains\TabPaint\Resources\TabPaint_Setup.iss"

# Inno Setup 编译器路径
$IsccPath       = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

# ==============================================================================
# 2. 脚本逻辑开始
# ==============================================================================

if (-not (Test-Path $IsccPath)) {
    Write-Host "错误: 找不到 ISCC.exe。请检查路径。" -ForegroundColor Red; Pause; exit
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "      TabPaint 自动化发布工具" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$Version = Read-Host "请输入本次发布的版本号 (例如 0.8.6)"
if ([string]::IsNullOrWhiteSpace($Version)) { Write-Host "版本号不能为空！" -ForegroundColor Red; Pause; exit }

# 2.1 准备目录
Write-Host "`n[1/5] 检查目录结构..." -ForegroundColor Yellow

# 确保暂存目录存在
if (-not (Test-Path $StagingDir)) { New-Item -ItemType Directory -Path $StagingDir | Out-Null }

# 确保输出目录存在
if (-not (Test-Path $FinalOutputDir)) { New-Item -ItemType Directory -Path $FinalOutputDir | Out-Null }

Write-Host "   -> 暂存区: $StagingDir"
Write-Host "   -> 输出区: $FinalOutputDir"


# 2.2 复制核心文件到暂存区
Write-Host "[2/5] 正在从 Build 目录复制文件到暂存区..." -ForegroundColor Yellow

$TargetFiles = @(
    "TabPaint.exe", 
    "TabPaint.dll",
    "TabPaint.deps.json", 
    "TabPaint.runtimeconfig.json" 
)

foreach ($fileName in $TargetFiles) {
    $sourceFile = Join-Path $SourceBuildDir $fileName
    $destFile   = Join-Path $StagingDir $fileName

    if (Test-Path $sourceFile) {
        Copy-Item -Path $sourceFile -Destination $destFile -Force
        Write-Host "   已复制: $fileName" -ForegroundColor Gray
    } else {
        Write-Host "   警告: 源目录中找不到文件: $fileName" -ForegroundColor Red
    }
}


# 2.3 生成绿色版 ZIP (直接生成到输出目录，避免被打包进 EXE)
Write-Host "[3/5] 正在生成绿色版 ZIP 包..." -ForegroundColor Yellow

$ZipFileName = "TabPaint_v$($Version)_Portable.zip"
# 【关键修改】Zip 直接保存到 $FinalOutputDir
$ZipOutputPath = Join-Path $FinalOutputDir "$ZipFileName"

# 如果输出目录已有同名 Zip，删除它
if (Test-Path $ZipOutputPath) { Remove-Item $ZipOutputPath -Force }

# 压缩暂存目录的内容 (因为 Zip 在暂存目录外面，所以不需要 Exclude *.zip 了)
Compress-Archive -Path "$StagingDir\*" -DestinationPath $ZipOutputPath -Force

Write-Host "   -> Zip 已生成: $ZipOutputPath" -ForegroundColor Green


# 2.4 修改 ISS 脚本版本号
Write-Host "[4/5] 正在更新 ISS 脚本并编译安装包..." -ForegroundColor Yellow

$IssContent = Get-Content $IssFilePath -Encoding UTF8
# 替换版本号
$NewIssContent = $IssContent -replace '(?<=#define MyAppVersion ").*?(?=")', $Version
# 替换源路径 (确保指向 StagingDir)
$NewIssContent = $NewIssContent -replace '(?<=#define MySourcePath ").*?(?=")', "$StagingDir\*"

$NewIssContent | Set-Content $IssFilePath -Encoding UTF8


# 2.5 编译安装包 (使用 /O 参数强制指定输出路径)
# /O"路径" 可以覆盖 ISS 文件里的 OutputDir 设置
# /F"文件名" 可以覆盖 OutputBaseFilename (可选，这里没加)

$IsccArgs = "/O`"$FinalOutputDir`" `"$IssFilePath`""

Write-Host "   -> 正在调用编译器..." -ForegroundColor Gray
$CompileProcess = Start-Process -FilePath $IsccPath -ArgumentList $IsccArgs -Wait -NoNewWindow -PassThru

if ($CompileProcess.ExitCode -eq 0) {
    Write-Host "[5/5] 安装包编译成功！" -ForegroundColor Green
} else {
    Write-Host "错误: 安装包编译失败，退出代码: $($CompileProcess.ExitCode)" -ForegroundColor Red
}

# ==============================================================================
# 结束
# ==============================================================================
Write-Host "`n全部完成！文件位于: $FinalOutputDir" -ForegroundColor Cyan
Invoke-Item $FinalOutputDir
Pause
