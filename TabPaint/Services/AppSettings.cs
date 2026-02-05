//
//AppSettings.cs
//应用程序设置模型，包含语言、主题、快捷键、画笔参数及各种UI配置项的持久化结构。
//
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public enum AppResamplingMode
    {
        Auto,
        Bilinear,    // 双线性
        Fant,        // 高质量幻像 (WPF HighQualityBicubic 类似)
        HighQuality  // 通用高质量
    }
    public enum SelectionClearMode
    {
        Transparent,    // 透明底 (RGBA: 0,0,0,0)
        White,          // 白底 (RGBA: 255,255,255,255)
        PreserveAlpha   // 不改变Alpha (只修改RGB，保留原透明度)
    }
    public enum MouseWheelMode
    {
        Zoom,          // 缩放 (默认)
        SwitchImage    // 切图
    }
    public enum AppLanguage
    {
        ChineseSimplified,
        English
    }
    public enum AppTheme
    {
        Light,
        Dark,
        System // 跟随系统
    }
    public class ToolSettingsModel
    {
        public double Thickness { get; set; } = AppConsts.DefaultPenThickness;
        public double Opacity { get; set; } = AppConsts.DefaultPenOpacity;
    }

    public class ShortcutItem
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public string DisplayText
        {
            get
            {
                if (Key == Key.None) return LocalizationManager.GetString("L_Key_None");

                StringBuilder sb = new StringBuilder();
                if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl + "); // 1. 处理修饰键 (Ctrl, Alt, Shift)
                if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift + ");
                if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt + ");
                if ((Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win + ");
                sb.Append(GetKeyDisplayName(Key));// 2. 处理按键显示的转换逻辑
                return sb.ToString();
            }
        }

        private string GetKeyDisplayName(Key key)
        {
            // 处理主键盘区的数字键 D0 - D9
            if (key >= Key.D0 && key <= Key.D9) return ((int)key - (int)Key.D0).ToString();

            // 处理小键盘区的数字键 NumPad0 - NumPad9
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num " + ((int)key - (int)Key.NumPad0).ToString();
            switch (key)
            {
                case Key.OemPlus: return "+";
                case Key.OemMinus: return "-";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "?";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemPipe: return "|";
                case Key.OemTilde: return "~";
                case Key.Return: return "Enter";
                case Key.Next: return "PageDown"; // WPF里 PageDown叫 Next
                case Key.Capital: return "CapsLock";
                case Key.Back: return "Backspace";
                default:
                    return key.ToString(); // 其他键保持默认
            }
        }
    }
        public partial class MainWindow
    {
        private void SaveAppState()
        {
            var settings = TabPaint.SettingsManager.Instance.Current;
            if (this.WindowState == System.Windows.WindowState.Maximized)
            {
                // 最大化时，保存还原后的坐标和尺寸
                settings.WindowTop = this.RestoreBounds.Top;
                settings.WindowLeft = this.RestoreBounds.Left;
                settings.WindowHeight = this.RestoreBounds.Height;
                settings.WindowWidth = this.RestoreBounds.Width;
                settings.WindowState = (int)System.Windows.WindowState.Maximized;
            }
            else if (this.WindowState == System.Windows.WindowState.Normal)
            {
                // 正常模式，直接保存
                settings.WindowTop = this.Top;
                settings.WindowLeft = this.Left;
                settings.WindowHeight = this.Height;
                settings.WindowWidth = this.Width;
                settings.WindowState = (int)System.Windows.WindowState.Normal;
            }
            if (_router?.CurrentTool != null)    settings.LastToolName = _router.CurrentTool.GetType().Name;
            if (_ctx != null)  settings.LastBrushStyle = _ctx.PenStyle;
            if (MainImageBar != null)
            {
                settings.IsImageBarCompact = MainImageBar.IsCompactMode;
            }
            TabPaint.SettingsManager.Instance.Save();
        }

        private void RestoreWindowBounds()
        {
            var settings = TabPaint.SettingsManager.Instance.Current;

            if (settings.WindowLeft == AppConsts.UninitializedWindowPosition || settings.WindowTop == AppConsts.UninitializedWindowPosition) return; // Note: -10000 is used as a magic number for uninitialized position
            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualWidth = SystemParameters.VirtualScreenWidth;
            double virtualHeight = SystemParameters.VirtualScreenHeight;

            // 只要窗口的左上角在虚拟屏幕范围内，就认为是可见的
            bool isVisible = (settings.WindowLeft >= virtualLeft &&
                              settings.WindowLeft < (virtualLeft + virtualWidth) &&
                              settings.WindowTop >= virtualTop &&
                              settings.WindowTop < (virtualTop + virtualHeight));

            if (isVisible)
            {
                this.Left = settings.WindowLeft;
                this.Top = settings.WindowTop;
            }
            else// 如果不在屏幕范围内（比如上次在副屏，现在副屏拔了），居中显示
            {
                
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            this.Width = Math.Max(settings.WindowWidth, AppConsts.WindowMinSize);
            this.Height = Math.Max(settings.WindowHeight, AppConsts.WindowMinSize);
            if (settings.WindowState == (int)WindowState.Maximized)
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void RestoreAppState()
        {
            try
            {
                var settings = TabPaint.SettingsManager.Instance.Current;

                // 1. 恢复笔刷大小
                if (_ctx != null)_ctx.PenThickness = settings.PenThickness;
                // 2. 准备目标工具
                ITool targetTool = null;
                BrushStyle targetStyle = settings.LastBrushStyle;

                switch (settings.LastToolName)
                {
                    case "EyedropperTool": targetTool = _tools.Eyedropper; break;
                    case "FillTool": targetTool = _tools.Fill; break;
                    case "SelectTool": targetTool = _tools.Select; break;
                    case "TextTool": targetTool = _tools.Text; break;
                    case "ShapeTool": targetTool = _tools.Shape; break;
                    case "PenTool":
                    default:
                        targetTool = _tools.Pen;
                        break;
                }
                if (MainImageBar != null)
                {
                    // 这里直接赋值，ImageBarControl 内部的 OnCompactModeChanged 会处理高度变化动画
                    MainImageBar.IsCompactMode = settings.IsImageBarCompact;
                }
                // 3. 设置上下文样式
                if (_ctx != null)  _ctx.PenStyle = targetStyle;
                if (_router != null)
                {

                    if (settings.LastToolName == "PenTool")
                    {
                        SetBrushStyle(_ctx.PenStyle); UpdateBrushSplitButtonIcon(_ctx.PenStyle);
                        UpdateGlobalToolSettingsKey(); 
                    }
                    else _router.SetTool(targetTool);
                }
             
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreAppState Error: {ex.Message}");
            }
        }

    }
    public class AppSettings : INotifyPropertyChanged
    {
        private AppLanguage _language = AppLanguage.ChineseSimplified;

        [JsonPropertyName("language")]
        public AppLanguage Language
        {
            get => _language;
            set
            {
                if (_language != value)
                {
                    _language = value;
                    OnPropertyChanged();
                }
            }
        }
        private SelectionClearMode _selectionClearMode = SelectionClearMode.White; // 默认白底
        [JsonPropertyName("selection_clear_mode")]
        public SelectionClearMode SelectionClearMode
        {
            get => _selectionClearMode;
            set
            {
                if (_selectionClearMode != value)
                {
                    _selectionClearMode = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _isFirstRun = true; // 默认为 true
        public bool IsFirstRun
        {
            get => _isFirstRun;
            set
            {
                if (_isFirstRun != value)
                {
                    _isFirstRun = value;
                    OnPropertyChanged(nameof(IsFirstRun));
                }
            }
        }
        private bool _isImageBarCompact = false; // 默认为 false (展开状态)

        [JsonPropertyName("is_image_bar_compact")]
        public bool IsImageBarCompact
        {
            get => _isImageBarCompact;
            set
            {
                if (_isImageBarCompact != value)
                {
                    _isImageBarCompact = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _startInViewMode = false; // 默认关闭，即启动为画图模式

        [JsonPropertyName("start_in_view_mode")]
        public bool StartInViewMode
        {
            get => _startInViewMode;
            set
            {
                if (_startInViewMode != value)
                {
                    _startInViewMode = value;
                    OnPropertyChanged();
                }
            }
        }
        private MouseWheelMode _viewMouseWheelMode = MouseWheelMode.Zoom;

        [JsonPropertyName("view_mouse_wheel_mode")]
        public MouseWheelMode ViewMouseWheelMode
        {
            get => _viewMouseWheelMode;
            set
            {
                if (_viewMouseWheelMode != value)
                {
                    _viewMouseWheelMode = value;
                    OnPropertyChanged();
                }
            }
        }
        private Dictionary<string, ToolSettingsModel> _perToolSettings;

        [JsonPropertyName("per_tool_settings")]
        public Dictionary<string, ToolSettingsModel> PerToolSettings
        {
            get
            {
                if (_perToolSettings == null) _perToolSettings = GetDefaultToolSettings();
                return _perToolSettings;
            }
            set
            {
                _perToolSettings = value;
                OnPropertyChanged();
            }
        }
        private bool _isTextToolbarExpanded = false; 

        [JsonPropertyName("is_text_toolbar_expanded")]
        public bool IsTextToolbarExpanded
        {
            get => _isTextToolbarExpanded;
            set
            {
                if (_isTextToolbarExpanded != value)
                {
                    _isTextToolbarExpanded = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _enableIccColorCorrection = false;

        [JsonPropertyName("enable_icc_color_correction")]
        public bool EnableIccColorCorrection
        {
            get => _enableIccColorCorrection;
            set
            {
                if (_enableIccColorCorrection != value)
                {
                    _enableIccColorCorrection = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _currentToolKey = "Pen_Pencil";
        [JsonIgnore]
        public string CurrentToolKey
        {
            get => _currentToolKey;
            set
            {
                if (_currentToolKey != value)
                {
                    _currentToolKey = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PenThickness));
                    OnPropertyChanged(nameof(PenOpacity));
                }
            }
        }
        [JsonIgnore] // 不再直接序列化这个值，而是序列化字典
        public double PenThickness
        {
            get
            {
                if (_currentToolKey == "Pen_Pencil") return 1.0;

                if (PerToolSettings.TryGetValue(_currentToolKey, out var settings))
                {
                    return settings.Thickness;
                }
                return 5.0; // Fallback
            }
            set
            {
                // 特殊处理：铅笔不允许修改粗细
                if (_currentToolKey == "Pen_Pencil") return;

                if (!PerToolSettings.ContainsKey(_currentToolKey))
                {
                    PerToolSettings[_currentToolKey] = new ToolSettingsModel();
                }

                if (Math.Abs(PerToolSettings[_currentToolKey].Thickness - value) > 0.01)
                {
                    PerToolSettings[_currentToolKey].Thickness = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        public double PenOpacity
        {
            get
            {
                if (PerToolSettings.TryGetValue(_currentToolKey, out var settings))
                {
                    return settings.Opacity;
                }
                return 1.0;
            }
            set
            {
                if (!PerToolSettings.ContainsKey(_currentToolKey))
                {
                    PerToolSettings[_currentToolKey] = new ToolSettingsModel();
                }

                if (Math.Abs(PerToolSettings[_currentToolKey].Opacity - value) > 0.001)
                {
                    PerToolSettings[_currentToolKey].Opacity = value;
                    OnPropertyChanged();
                }
            }
        }
        private Dictionary<string, ToolSettingsModel> GetDefaultToolSettings()
        {
            var dict = new Dictionary<string, ToolSettingsModel>();

            // 为每种笔刷样式预设值
            dict["Pen_Pencil"] = new ToolSettingsModel { Thickness = 1.0, Opacity = 1.0 };
            dict["Pen_Round"] = new ToolSettingsModel { Thickness = 5.0, Opacity = 1.0 };
            dict["Pen_Square"] = new ToolSettingsModel { Thickness = 10.0, Opacity = 1.0 };
            dict["Pen_Highlighter"] = new ToolSettingsModel { Thickness = 20.0, Opacity = 0.5 }; // 荧光笔默认半透明
            dict["Pen_Eraser"] = new ToolSettingsModel { Thickness = 20.0, Opacity = 1.0 };
            dict["Pen_Watercolor"] = new ToolSettingsModel { Thickness = 30.0, Opacity = 0.8 };
            dict["Pen_Crayon"] = new ToolSettingsModel { Thickness = 15.0, Opacity = 1.0 };
            dict["Pen_Spray"] = new ToolSettingsModel { Thickness = 40.0, Opacity = 0.8 };
            dict["Pen_Mosaic"] = new ToolSettingsModel { Thickness = 20.0, Opacity = 1.0 };
            dict["Pen_Brush"] = new ToolSettingsModel { Thickness = 8.0, Opacity = 1.0 };

            // 形状工具预留
            dict["Shape"] = new ToolSettingsModel { Thickness = 3.0, Opacity = 1.0 };
            return dict;
        }
        private AppTheme _themeMode = AppTheme.System; // 默认跟随系统

        [JsonPropertyName("theme_mode")]
        public AppTheme ThemeMode
        {
            get => _themeMode;
            set
            {
                if (_themeMode != value)
                {
                    _themeMode = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _isFixedZoom = false;

        [JsonPropertyName("is_fixed_zoom")]
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
        [JsonPropertyName("enable_clipboard_monitor")]
        public bool EnableClipboardMonitor
        {
            get => _enableClipboardMonitor;
            set
            {
                if (_enableClipboardMonitor != value)
                {
                    _enableClipboardMonitor = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _enableClipboardMonitor = false; // 默认关闭
        [JsonPropertyName("last_tool_name")]
        public string LastToolName { get; set; } = "PenTool"; // 默认为笔

        [JsonPropertyName("last_brush_style")]
        public BrushStyle LastBrushStyle { get; set; } = BrushStyle.Pencil; // 默认为铅笔

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private List<string> _recentFiles = new List<string>();

        [JsonPropertyName("recent_files")]
        public List<string> RecentFiles
        {
            get => _recentFiles;
            set
            {
                if (_recentFiles != value)
                {
                    _recentFiles = value;
                    OnPropertyChanged();
                }
            }
        }
        private Dictionary<string, ShortcutItem> _shortcuts;

        [JsonPropertyName("shortcuts")]
        public Dictionary<string, ShortcutItem> Shortcuts
        {
            get
            {
                if (_shortcuts == null) _shortcuts = GetDefaultShortcuts();
                return _shortcuts;
            }
            set
            {
                _shortcuts = value;
                OnPropertyChanged();
            }
        }
        private bool _showRulers = false; // 默认不显示

        [JsonPropertyName("show_rulers")]
        public bool ShowRulers
        {
            get => _showRulers;
            set
            {
                if (_showRulers != value)
                {
                    _showRulers = value;
                    OnPropertyChanged();
                }
            }
        }
        private AppResamplingMode _resamplingMode = AppResamplingMode.Auto;
        [JsonPropertyName("resampling_mode")]
        public AppResamplingMode ResamplingMode
        {
            get => _resamplingMode;
            set
            {
                if (_resamplingMode != value)
                {
                    _resamplingMode = value;
                    OnPropertyChanged();
                }
            }
        }
        private double _viewInterpolationThreshold = AppConsts.DefaultViewInterpolationThreshold;
        [JsonPropertyName("view_interpolation_threshold")]
        public double ViewInterpolationThreshold
        {
            get => _viewInterpolationThreshold;
            set
            {
                if (Math.Abs(_viewInterpolationThreshold - value) > 0.1)
                {
                    _viewInterpolationThreshold = value;
                    OnPropertyChanged();
                }
            }
        }
        private List<string> _customColors = new List<string>();

        [JsonPropertyName("custom_colors")]
        public List<string> CustomColors
        {
            get => _customColors;
            set
            {
                if (_customColors != value)
                {
                    _customColors = value;
                    OnPropertyChanged();
                }
            }
        }
        private double _paintInterpolationThreshold = AppConsts.DefaultPaintInterpolationThreshold;
        [JsonPropertyName("paint_interpolation_threshold")]
        public double PaintInterpolationThreshold
        {
            get => _paintInterpolationThreshold;
            set
            {
                if (Math.Abs(_paintInterpolationThreshold - value) > 0.1)
                {
                    _paintInterpolationThreshold = value;
                    OnPropertyChanged();
                }
            }
        }
        [JsonPropertyName("window_width")]
        public double WindowWidth { get; set; } = AppConsts.DefaultWindowWidth; // 默认宽

        [JsonPropertyName("window_height")]
        public double WindowHeight { get; set; } = AppConsts.DefaultWindowHeight; // 默认高

        [JsonPropertyName("window_left")]
        public double WindowLeft { get; set; } = AppConsts.UninitializedWindowPosition; // 默认无效值，用于判断是否是首次启动

        [JsonPropertyName("window_top")]
        public double WindowTop { get; set; } = AppConsts.UninitializedWindowPosition;
        [JsonPropertyName("window_state")] // 0: Normal, 1: Minimized, 2: Maximized
        public int WindowState { get; set; } = 0;

        private bool _autoLoadFolderImages = true; // 默认值为 true，保持原有行为

        [JsonPropertyName("auto_load_folder_images")]
        public bool AutoLoadFolderImages
        {
            get => _autoLoadFolderImages;
            set
            {
                if (_autoLoadFolderImages != value)
                {
                    _autoLoadFolderImages = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _viewShowTransparentGrid = false; // 默认隐藏透明灰白格子(即显示白底)

        [JsonPropertyName("view_show_transparent_grid")]
        public bool ViewShowTransparentGrid
        {
            get => _viewShowTransparentGrid;
            set
            {
                if (_viewShowTransparentGrid != value)
                {
                    _viewShowTransparentGrid = value;
                    OnPropertyChanged();
                }
            }
        }
        // 在 AppSettings 类中添加以下代码（建议放在其他 bool 属性附近）

        private bool _skipResetConfirmation = false; // 默认需要确认

        [JsonPropertyName("skip_reset_confirmation")]
        public bool SkipResetConfirmation
        {
            get => _skipResetConfirmation;
            set
            {
                if (_skipResetConfirmation != value)
                {
                    _skipResetConfirmation = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _autoPopupOnClipboardImage = false; // 默认不弹出

        [JsonPropertyName("auto_popup_on_clipboard_image")]
        public bool AutoPopupOnClipboardImage
        {
            get => _autoPopupOnClipboardImage;
            set
            {
                if (_autoPopupOnClipboardImage != value)
                {
                    _autoPopupOnClipboardImage = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _enableFileDeleteInPaintMode = false; // 默认关闭

        [JsonPropertyName("enable_file_delete_in_paint_mode")]
        public bool EnableFileDeleteInPaintMode
        {
            get => _enableFileDeleteInPaintMode;
            set
            {
                if (_enableFileDeleteInPaintMode != value)
                {
                    _enableFileDeleteInPaintMode = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _viewUseDarkCanvasBackground = true; // 默认开启深灰色背景(#1A1A1A)

        [JsonPropertyName("view_use_dark_canvas_background")]
        public bool ViewUseDarkCanvasBackground
        {
            get => _viewUseDarkCanvasBackground;
            set
            {
                if (_viewUseDarkCanvasBackground != value)
                {
                    _viewUseDarkCanvasBackground = value;
                    OnPropertyChanged();
                }
            }
        }
        private string _themeAccentColor = AppConsts.DefaultThemeAccentColor; // 默认蓝色

        [JsonPropertyName("theme_accent_color")]
        public string ThemeAccentColor
        {
            get => _themeAccentColor;
            set
            {
                if (_themeAccentColor != value)
                {
                    _themeAccentColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _performanceScore = -1;
        [JsonPropertyName("performance_score")]
        public int PerformanceScore
        {
            get => _performanceScore;
            set
            {
                if (_performanceScore != value)
                {
                    _performanceScore = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime _lastBenchmarkDate = DateTime.MinValue;
        [JsonPropertyName("last_benchmark_date")]
        public DateTime LastBenchmarkDate
        {
            get => _lastBenchmarkDate;
            set
            {
                if (_lastBenchmarkDate != value)
                {
                    _lastBenchmarkDate = value;
                    OnPropertyChanged();
                }
            }
        }
        private Dictionary<string, ShortcutItem> GetDefaultShortcuts()
        {
            var defaults = new Dictionary<string, ShortcutItem>
    {
        // 1. 全局/视图功能
        { "View.PrevImage",      new ShortcutItem { Key = Key.Left, Modifiers = ModifierKeys.None } },
        { "View.NextImage",      new ShortcutItem { Key = Key.Right, Modifiers = ModifierKeys.None } },
        { "View.RotateLeft",     new ShortcutItem { Key = Key.L, Modifiers = ModifierKeys.Control } },
        { "View.RotateRight",    new ShortcutItem { Key = Key.R, Modifiers = ModifierKeys.Control } },
        { "View.ToggleMode",     new ShortcutItem { Key = Key.Tab, Modifiers = ModifierKeys.None } }, // 切换模式
        { "View.FullScreen",     new ShortcutItem { Key = Key.F11, Modifiers = ModifierKeys.None } },
        { "View.VerticalFlip",   new ShortcutItem { Key = Key.V, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 自动色阶
        { "View.HorizontalFlip",       new ShortcutItem { Key = Key.H, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 调整大小
        { "View.ToggleMinimize", new ShortcutItem { Key = Key.P, Modifiers = ModifierKeys.Control } }, 

        // 2. 高级工具 (Ctrl + Alt 系列)
        { "Tool.ClipMonitor",    new ShortcutItem { Key = Key.P, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 剪贴板监听开关
        { "Tool.RemoveBg",       new ShortcutItem { Key = Key.D1, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 抠图
        { "Tool.ChromaKey",      new ShortcutItem { Key = Key.D2, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.OCR",            new ShortcutItem { Key = Key.D3, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.ScreenPicker",   new ShortcutItem { Key = Key.D4, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 屏幕取色
        { "Tool.CopyColorCode",  new ShortcutItem { Key = Key.D5, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AutoCrop",       new ShortcutItem { Key = Key.D6, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AddBorder",      new ShortcutItem { Key = Key.D7, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        // 3. 特殊操作 (Ctrl + Shift 系列)
        { "File.OpenWorkspace",  new ShortcutItem { Key = Key.O, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } }, // 打开工作区
        { "File.PasteNewTab",    new ShortcutItem { Key = Key.V, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } }, // 粘贴为新标签
          // === 3. 基础绘图工具  ===
        { "Tool.SwitchToSelect", new ShortcutItem { Key = Key.D1, Modifiers = ModifierKeys.Control } }, // 选择
        { "Tool.SwitchToPen",    new ShortcutItem { Key = Key.D2, Modifiers = ModifierKeys.Control } }, // 铅笔/画笔
        { "Tool.SwitchToPick",   new ShortcutItem { Key = Key.D3, Modifiers = ModifierKeys.Control } }, // 取色
        { "Tool.SwitchToEraser", new ShortcutItem { Key = Key.D4, Modifiers = ModifierKeys.Control } }, // 橡皮
        { "Tool.SwitchToFill",   new ShortcutItem { Key = Key.D5, Modifiers = ModifierKeys.Control } }, // 填充
        { "Tool.SwitchToText",   new ShortcutItem { Key = Key.D6, Modifiers = ModifierKeys.Control } }, // 文字
        { "Tool.SwitchToBrush",  new ShortcutItem { Key = Key.D7, Modifiers = ModifierKeys.Control } }, // 画刷菜单(通常只切到默认画刷)
        { "Tool.SwitchToShape",  new ShortcutItem { Key = Key.D8, Modifiers = ModifierKeys.Control } }, // 形状菜单(通常切到默认形状)
          // === 4. 效果菜单 (Effect) ===
        { "Effect.Brightness",   new ShortcutItem { Key = Key.R, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 亮度/对比度
        { "Effect.Temperature",  new ShortcutItem { Key = Key.T, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 色温/色调
        { "Effect.Grayscale",    new ShortcutItem { Key = Key.Y, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 黑白
        { "Effect.Invert",       new ShortcutItem { Key = Key.U, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 反色
        { "Effect.AutoLevels",   new ShortcutItem { Key = Key.I, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 自动色阶
        { "Effect.Resize",       new ShortcutItem { Key = Key.O, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 调整大小
    };
            return defaults;
        }
        public void ResetShortcutsToDefault()
        {
            var defaults = GetDefaultShortcuts();
            Shortcuts = defaults;
        }
    }
}
