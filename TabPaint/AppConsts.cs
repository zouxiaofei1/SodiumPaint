using System;
using System.IO;

namespace TabPaint
{
    /// <summary>
    /// 项目全局常量定义，包含硬编码的配置值、Win32 消息、路径及 AI 模型信息等。
    /// </summary>
    public static class AppConsts
    {
        // --- 版本信息 ---
        public const string ProgramVersion = "v0.9.3.2-beta";

        // --- Win32 消息常量 ---
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int WM_MOUSEHWHEEL = 0x020E;

        // --- HitTest 区域常量 ---
        public const int HTCLIENT = 1;
        public const int HTLEFT = 10;
        public const int HTRIGHT = 11;
        public const int HTTOP = 12;
        public const int HTTOPLEFT = 13;
        public const int HTTOPRIGHT = 14;
        public const int HTBOTTOM = 15;
        public const int HTBOTTOMLEFT = 16;
        public const int HTBOTTOMRIGHT = 17;

        // --- 缩放与动画参数 ---
        public const double DefaultZoomStep = 0.1;
        public const double ZoomTimes = 1.1;
        public const double MinZoom = 0.05;
        public const double MaxZoom = 50.0;
        public const double ZoomSnapThreshold = 0.001;
        public const double ZoomLerpFactor = 1.0;

        // --- UI 与交互参数 ---
        public const int ToastDuration = 1500;
        public const int ToastFadeInMs = 200;
        public const int ToastFadeOutMs = 500;
        public const string InternalClipboardFormat = "TabPaint_Internal_Copy_Marker";

        // Timer intervals and delays
        public const int DeleteCommitTimerSeconds = 2;
        public const int DragTempCleanupDelaySeconds = 150;
        public const int NavGapLevel1Ms = 5000;
        public const int NavGapLevel2Ms = 10000;
        public const int ClipboardCooldownMs = 1000;
        public const double DefaultWindowWidth = 850;
        public const double DefaultWindowHeight = 700;
        public const double WindowMinSize = 200;
        public const int MaxThumbnailCacheCount = 300;
        public const int WindowCornerArea = 16;
        public const int WindowSideArea = 8;
        public const double DoubleClickTimeThreshold = 2.0;
        public const int HighPerformanceThreshold = 8;

        // --- 缩略图参数 ---
        public const int DefaultThumbnailWidth = 100;
        public const int DefaultThumbnailHeight = 60;

        // --- 目录与路径 ---
        public static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabPaint");
        public static readonly string CacheDir = Path.Combine(AppDataFolder, "Cache");
        public static readonly string DragTempDir = Path.Combine(CacheDir, "DragTemp");
        public static readonly string SessionPath = Path.Combine(AppDataFolder, "session.json");
        public static readonly string ClipboardCacheDir = Path.Combine(CacheDir, "Clipboard");

        // --- AI 模型配置 (RMBG - 背景移除) ---
        public const string BgRem_ModelUrl_HF = "https://huggingface.co/briaai/RMBG-1.4/resolve/main/onnx/model.onnx";
        public const string BgRem_ModelUrl_MS = "https://modelscope.cn/models/AI-ModelScope/RMBG-1.4/resolve/master/onnx/model.onnx";
        public const string BgRem_ModelName = "rmbg-1.4.onnx";
        public const string BgRem_ExpectedMD5 = "8bb9b16ff49cda31e7784852873cfd0d";

        // --- AI 模型配置 (Real-ESRGAN - 超分辨率) ---
        public const string Sr_ModelUrl_HF = "https://modelscope.cn/models/AXERA-TECH/Real-ESRGAN/resolve/master/onnx/realesrgan-x4-256.onnx";
        public const string Sr_ModelUrl_Mirror = "https://modelscope.cn/models/AXERA-TECH/Real-ESRGAN/resolve/master/onnx/realesrgan-x4-256.onnx";
        public const string Sr_ModelName = "realesrgan-x4plus.onnx";
        public const string Sr_ExpectedMD5 = "25C354305A32B59300A610BCD7846977";
        public const int Sr_TileSize = 256;
        public const int Sr_ScaleFactor = 4;

        // --- AI 模型配置 (LaMa - 智能填补) ---
        public const string Inpaint_ModelUrl = "https://huggingface.co/Carve/LaMa-ONNX/resolve/main/lama_fp32.onnx";
        public const string Inpaint_ModelUrl_Mirror = "https://modelscope.cn/models/codetrend/LaMa_Inpainting_Model_ONNX/resolve/master/lama_fp32.onnx";
        public const string Inpaint_ModelName = "lama_fp32.onnx";
        public const string Inpaint_ExpectedMD5 = "2777748DC5275B27DAFC63C5D4F1F730";

        // --- 绘图工具参数 ---
        public const double DefaultPenThickness = 5.0;
        public const double DefaultPenOpacity = 1.0;
        public const int PenCursorZIndex = 9999;
        public const byte HighlighterAlpha = 50;
        public const byte AiEraserCursorAlpha = 100;
        public const double PenLowOpacityThreshold = 0.3;
        public const byte PenLowOpacityStrokeAlpha = 100;
        public const double PenLowOpacityStrokeThickness = 0.5;
        public const double PenDefaultStrokeThickness = 1.0;
        public const double AiEraserMaskOpacity = 0.6;

        // --- 画布限制 ---
        public const double MaxCanvasSize = 16384.0;
        public const double StandardDpi = 96.0;
        public const int DefaultBlankCanvasWidth = 2400;
        public const int DefaultBlankCanvasHeight = 1800;

        // --- 文本工具参数 ---
        public const double DefaultFontSize = 24.0;
        public const double DefaultTextBoxWidth = 500.0;
        public const double MinTextBoxHeight = 50.0;
        public const double MaxTextBoxWidth = 1000.0;
        public const int EditorOverlayZIndex = 999;
        public const double TextToolOutlineThickness = 1.5;
        public const double TextToolHandleHitTestSize = 12.0;
        public const double TextToolBorderThicknessMin = 5.0;
        public const double TextToolBorderThicknessMax = 10.0;
        public const double TextToolPadding = 5.0;

        // --- 形状工具参数 ---
        public const double ShapeToolRoundedRectRadius = 20.0;
        public const double ShapeToolStarInnerRadiusRatio = 0.4;
        public const double ShapeToolBubbleRadiusRatio = 0.15;
        public const double ShapeToolBubbleTailHeightRatio = 0.2;
        public const double ShapeToolBubbleTailStartRatio = 0.7;
        public const double ShapeToolBubbleTailEndRatio = 0.5;

        // --- 选择工具参数 ---
        public const double SelectToolHandleSize = 6.0;
        public const double SelectToolOutlineThickness = 1.5;
        public const double SelectToolDashLength = 4.0;
        public const double SelectToolAnimationDurationSeconds = 1.0;
        public const double SelectToolAnimationTo = 8.0;

        // --- 图片栏参数 ---
        public const double ImageBarItemWidth = 124.0;
        public const double ImageBarAddButtonWidthFallback = 46.0;

        // --- 选择与拖拽定时 (ms) ---
        public const int TabSwitchCheckIntervalMs = 50;
        public const int HoverStartTimeThresholdMs = 200;
        public const int QuickDragDelayMs = 500;
        public const int SlowDragDelayMs = 1000;
        public const int TempFileCleanupDelayMs = 5000;

        // --- 拖拽与布局阈值 ---
        public const double DragTitleBarThresholdY = 100.0;
        public const double DragImageBarThresholdY = 210.0;
        public const double DragGlobalPadding = 5.0;

        // --- AI 修复参数 ---
        public const int AiInpaintSize = 512;
        public const int AiInferenceSizeDefault = 1024;
        public const int AiSrTileOverlap = 16;
        public const int AiDownloadTimeoutMinutes = 20;
        public const int AiDownloadBufferSize = 8192;

        // --- 布局参数 ---
        public const double FitToWindowMarginFactor = 0.9;

        // --- 加载与性能参数 ---
        public const int PreviewDecodeWidth = 480;
        public const long HugeImagePixelThreshold = 10_000_000;
        public const long PerformanceScorePixelThreshold = 2_000_000;
        public const long MemoryLimitForAggressiveRelease = 1024L * 1024 * 1024;
        public const int ImageLoadDelayHugeMs = 100;
        public const int ImageLoadDelayLazyMs = 50;

        // --- 滤镜参数 ---
        public const double SepiaR1 = 0.393;
        public const double SepiaR2 = 0.769;
        public const double SepiaR3 = 0.189;
        public const double SepiaG1 = 0.349;
        public const double SepiaG2 = 0.686;
        public const double SepiaG3 = 0.168;
        public const double SepiaB1 = 0.272;
        public const double SepiaB2 = 0.534;
        public const double SepiaB3 = 0.131;

        // --- EXIF Tags ---
        public const string ExifTagExposureTime = "/app1/ifd/exif/{uint=33434}";
        public const string ExifTagFNumber = "/app1/ifd/exif/{uint=33437}";
        public const string ExifTagIsoSpeed = "/app1/ifd/exif/{uint=34855}";
        public const string ExifTagExposureBias = "/app1/ifd/exif/{uint=37380}";
        public const string ExifTagFocalLength = "/app1/ifd/exif/{uint=37386}";
        public const string ExifTagFocalLength35mm = "/app1/ifd/exif/{uint=41989}";
        public const string ExifTagMeteringMode = "/app1/ifd/exif/{uint=37383}";
        public const string ExifTagFlash = "/app1/ifd/exif/{uint=37385}";
        public const string ExifTagLensModel = "/app1/ifd/exif/{uint=42036}";

        // --- 灰度转换权重 ---
        public const double GrayWeightR = 0.2126;
        public const double GrayWeightG = 0.7152;
        public const double GrayWeightB = 0.0722;

        // --- 样式与颜色 ---
        public const string DarkBackgroundHex = "#333";
        public const string DefaultThemeAccentColor = "#0078D4";

        // --- 系统与环境 ---
        public const int Windows11BuildThreshold = 22000;
        public const int DwmCornerPreferenceRounded = 2;
        public const double UninitializedWindowPosition = -10000.0;

        // --- 渲染阈值 ---
        public const double DefaultViewInterpolationThreshold = 160.0;
        public const double DefaultPaintInterpolationThreshold = 200.0;

        // --- 文件过滤器与扩展名 ---
        public const string ImageFilterFormat = "{0}|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.avif;*.ico;*.heic;*.jfif;*.exif;*.jpe;*.jxl;*.heif;*.hif;*.dib;*.wdp;*.wmp;*.jxr|{1} (*.png)|*.png|{2} (*.jpg;*.jpeg)|*.jpg;*.jpeg|{3} (*.webp)|*.webp|{4} (*.bmp)|*.bmp|{5} (*.gif)|*.gif|{6} (*.tif;*.tiff)|*.tif;*.tiff|{7} (*.ico)|*.ico";
        public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".avif", ".ico", ".tiff", ".heic", ".tif", ".jfif", ".exif", ".jpe", ".jxl", ".heif", ".hif", ".dib", ".wdp", ".wmp", ".jxr" };
    }
}
