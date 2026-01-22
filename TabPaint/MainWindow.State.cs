using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TabPaint.MainWindow;

//
//TabPaint定义
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private const int WM_NCHITTEST = 0x0084;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;


        private const double ZoomStep = 0.1; // 每次滚轮缩放步进
        private const double ZoomTimes = 1.1;
        private const double MinZoom = 0.05;
        private const double MaxZoom = 50.0;
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int WM_MOUSEHWHEEL = 0x020E;
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private HwndSource _hwndSource;
        private bool _isMonitoringClipboard = false;
        private string _fileSize = "0 KB";
        public string FileSize
        {
            get => _fileSize;
            set
            {
                if (_fileSize != value)
                {
                    _fileSize = value;
                    OnPropertyChanged(nameof(FileSize));
                }
            }
        }
        private CanvasSurface _surface;
        public UndoRedoManager _undo;
        public ToolContext _ctx;
        public InputRouter _router;
        public ToolRegistry _tools;
        public double zoomscale = 1;
        private byte[]? _preDrawSnapshot = null;

        private WriteableBitmap _bitmap;
        private int _bmpWidth, _bmpHeight;
        private Color _penColor = Colors.Black;
        private bool _isDrawing = false;
        public List<string> _imageFiles = new List<string>();
        private int _currentImageIndex = -1;
        private bool _isEdited = false; // 标记当前画布是否被修改
        private string _currentFileName = LocalizationManager.GetString("L_Common_Untitled");
        public string ProgramVersion { get; set; } = "v0.9.3-beta";

        private bool _isFileSaved = true; // 是否有未保存修改

        private string _mousePosition = "0,0" + LocalizationManager.GetString("L_Main_Unit_Pixel");
        public string MousePosition
        {
            get => _mousePosition;
            set { _mousePosition = value; OnPropertyChanged(); }
        }
        private bool _isPanning = false;
        private Point _lastMousePosition;
        private string _imageSize = "0×0" + LocalizationManager.GetString("L_Main_Unit_Pixel");
        public string ImageSize
        {
            get => _imageSize;
            set { _imageSize = value; OnPropertyChanged(); }
        }

        private string _selectionSize = "0×0" + LocalizationManager.GetString("L_Main_Unit_Pixel");
        public string SelectionSize
        {
            get => _selectionSize;
            set { _selectionSize = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private double _penThickness = 5;
        public double PenThickness
        {
            get => _penThickness;
            set
            {
                if (_penThickness != value)
                {
                    _penThickness = value;
                    OnPropertyChanged(nameof(PenThickness));
                    if (_ctx != null) _ctx.PenThickness = value;
                }
            }
        }
 
        public enum BrushStyle { Round, Square, Brush, Spray, Pencil, Eraser, Watercolor, Crayon, Highlighter, Mosaic }
        public enum UndoActionType
        {
            Draw,         // 普通绘图
            Transform,    // 旋转/翻转
            Selection,
            FileDelete
        }
        private UndoAction _pendingDeleteUndo = null;
        public SelectTool Select;
        public SolidColorBrush ForegroundBrush { get; set; } = new SolidColorBrush(Colors.Black);
        public SolidColorBrush BackgroundBrush { get; set; } = new SolidColorBrush(Colors.White);
        // 当前画笔颜色属性，可供工具使用
        public Color BackgroundColor= Colors.White;
        public Color ForegroundColor= Colors.Black;
        public SolidColorBrush SelectedBrush { get; set; } = new SolidColorBrush(Colors.Black);


        private double _zoomScale = 1.0;
        private bool _isInternalZoomUpdate =false;
        private string _zoomLevel = "100%";
        public string ZoomLevel
        {
            get => _zoomLevel;
            set { _zoomLevel = value; OnPropertyChanged(); }
        }
        private System.Windows.Controls.TextBox? _activeTextBox;
        private Stack<UndoAction> _undoStack = new Stack<UndoAction>();
        private List<Int32Rect> _currentDrawRegions = new List<Int32Rect>(); // 当前笔的区域记录
        private Stack<UndoAction> _redoStack = new Stack<UndoAction>();
        string PicFilterString => string.Format("{0}|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.avif*.ico;*.heic;*.jfif|{1} (*.png)|*.png|{2} (*.jpg;*.jpeg)|*.jpg;*.jpeg|{3} (*.webp)|*.webp|{4} (*.bmp)|*.bmp|{5} (*.gif)|*.gif|{6} (*.tif;*.tiff)|*.tif;*.tiff|{7} (*.ico)|*.ico",
            LocalizationManager.GetString("L_Main_Filter_AllImages"),
            LocalizationManager.GetString("L_Main_Filter_PNG"),
            LocalizationManager.GetString("L_Main_Filter_JPEG"),
            LocalizationManager.GetString("L_Main_Filter_WebP"),
            LocalizationManager.GetString("L_Main_Filter_BMP"),
            LocalizationManager.GetString("L_Main_Filter_GIF"),
            LocalizationManager.GetString("L_Main_Filter_TIFF"),
            LocalizationManager.GetString("L_Main_Filter_ICO"));
        ITool LastTool;
        public bool useSecondColor = false;//是否使用备用颜色
        private bool _maximized = false;
        private Rect _restoreBounds;
        private string _currentFilePath = string.Empty;
        private Point _dragStartPoint;
        private bool _draggingFromMaximized = false;
        public class PaintSession
        {
            public string LastViewedFile { get; set; } // 上次正在看的文件
            public List<SessionTabInfo> Tabs { get; set; } = new List<SessionTabInfo>();
        }
        public bool _startupFinished = false;

        public class SessionTabInfo
        {
            public string Id { get; set; }
            public string OriginalPath { get; set; }
            public string BackupPath { get; set; }
            public bool IsDirty { get; set; }
            public bool IsNew { get; set; }
            public int UntitledNumber { get; set; }
            // [新增] 记录该标签页所属的工作目录
            public string WorkDirectory { get; set; }
        }

        private string _sessionPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TabPaint", "session.json");
        private readonly string _cacheDir = System.IO.Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "TabPaint", "Cache");
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        public CanvasResizeManager _canvasResizer;
        private int _savedUndoPoint = 0;
        private string _currentImageFullInfo;
        public string CurrentImageFullInfo
        {
            get => _currentImageFullInfo;
            set { _currentImageFullInfo = value; OnPropertyChanged(nameof(CurrentImageFullInfo)); }
        }
        private double _originalDpiX = 96.0;
        private double _originalDpiY = 96.0;
        private bool _isFixedZoom = false;
        public bool IsFixedZoom
        {
            get => _isFixedZoom;
            set
            {
                if (_isFixedZoom != value)
                {
                    _isFixedZoom = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _isPaintingMode = true;//画图模式
        public bool MicaEnabled = false;
        private bool _isLoadingImage = true;//是否正在加载图像,false时不能画图
        private bool _programClosed = false;
        private string _workingPath;
        public const string InternalClipboardFormat = "TabPaint_Internal_Copy_Marker";
        public bool _firstFittoWindowdone = false;
        public int PerformanceScore;
        public static readonly DependencyProperty IsViewModeProperty =
     DependencyProperty.Register("IsViewMode", typeof(bool), typeof(MainWindow),
         new PropertyMetadata(false, OnIsViewModeChanged));
        private bool _showRulers = false; // 默认为 false 或从配置读取
        public bool ShowRulers
        {
            get => _showRulers;
            set
            {
                if (_showRulers != value)
                {
                    _showRulers = value;
                    OnPropertyChanged(nameof(ShowRulers));
                    // 切换显示时强制刷新一次位置
                    if (value) UpdateRulerPositions();
                }
            }
        }
        public bool IsViewMode
        {
            get { return (bool)GetValue(IsViewModeProperty); }
            set { SetValue(IsViewModeProperty, value); }
        }
        public TabPaint.Controls.MenuBarControl MainMenu;
        public TabPaint.Controls.ToolBarControl MainToolBar;
        public TabPaint.Controls.ImageBarControl MainImageBar;
        public TabPaint.Controls.StatusBarControl MyStatusBar;

        private DispatcherTimer _toastTimer;
        private const int ToastDuration = 1500;
        public bool BlanketMode = false;
        private bool _isCurrentFileGif = false; // 标记当前文件是否为GIF
        private System.Windows.Threading.DispatcherTimer _deleteCommitTimer;
        private List<FileTabItem> _pendingDeletionTabs = new List<FileTabItem>(); // 待删除列表
        private FileTabItem _lastDeletedTabForUndo = null; // 专门用于 Ctrl+Z 的引用
        private int _lastDeletedTabIndex = -1;
        private bool _isDraggingBirdEye = false;
        private Brush _originalGridBrush; // 用于存储启动时 XAML 里定义的那个格子画刷
        private readonly SolidColorBrush _darkBackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333"));
        public bool _currentFileExists = true; // 标记当前文件是否存在于磁盘
        private bool _hasUserManuallyZoomed = false;
        public static ThumbnailCache GlobalThumbnailCache = new ThumbnailCache(10000); // 存300张
        private bool _isUpdatingToolSettings = false;
        public static SemaphoreSlim _thumbnailSemaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }
}
