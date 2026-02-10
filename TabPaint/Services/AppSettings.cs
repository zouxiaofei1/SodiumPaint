//
//AppSettings.cs
//应用程序设置模型，包含语言、主题、快捷键、画笔参数及各种UI配置项的持久化结构。
//
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Globalization;
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

        public ToolSettingsModel Clone()
        {
            return new ToolSettingsModel
            {
                Thickness = this.Thickness,
                Opacity = this.Opacity
            };
        }
    }

    public class ShortcutItem : INotifyPropertyChanged
    {
        private Key _key;
        private ModifierKeys _modifiers;

        public Key Key
        {
            get => _key;
            set
            {
                if (_key != value)
                {
                    _key = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public ModifierKeys Modifiers
        {
            get => _modifiers;
            set
            {
                if (_modifiers != value)
                {
                    _modifiers = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // 当快捷键的 Key 或 Modifiers 改变时，通知全局 Provider
            if (propertyName == nameof(Key) || propertyName == nameof(Modifiers))
            {
                TabPaint.Services.ShortcutProvider.Instance.NotifyChanged();
            }
        }

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

            int mainWindowCount = Application.Current.Windows.OfType<MainWindow>().Count();
            // 如果是最后一个窗口，或者程序正在退出过程中，则保存画笔参数到全局设置
            if (mainWindowCount <= 1 || _programClosed)
            {
                // 只有最后一个窗口关闭时才保存画笔参数到设置
                if (this.LocalPerToolSettings != null)
                {
                    settings.PerToolSettings = this.LocalPerToolSettings.ToDictionary(k => k.Key, v => v.Value.Clone());
                }
                settings.CurrentToolKey = this.CurrentToolKey;
            }

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
            if (_router?.CurrentTool != null) settings.LastToolName = _router.CurrentTool.GetType().Name;
            if (_ctx != null) settings.LastBrushStyle = _ctx.PenStyle;
            if (MainImageBar != null)
            {
                settings.IsImageBarCompact = MainImageBar.IsCompactMode;
            }
            TabPaint.SettingsManager.Instance.Save();
        }

        private void RestoreWindowBounds()
        {
            var settings = TabPaint.SettingsManager.Instance.Current;
            var workArea = SystemParameters.WorkArea;

            // 1. 确定初始尺寸
            double winWidth = Math.Max(settings.WindowWidth, AppConsts.WindowMinSize);
            double winHeight = Math.Max(settings.WindowHeight, AppConsts.WindowMinSize);
            if (winWidth > workArea.Width) winWidth = workArea.Width;
            if (winHeight > workArea.Height) winHeight = workArea.Height;

            this.Width = winWidth;
            this.Height = winHeight;

            // 2. 查找参照窗口
            var existingWindows = Application.Current.Windows.OfType<MainWindow>()
                .Where(w => w != this && w.IsVisible).ToList();

            if (existingWindows.Count > 0)
            {
                // 层叠创建逻辑
                var refWindow = existingWindows.Last();
                double offset = 30;
                double newLeft, newTop;

                if (refWindow.WindowState == WindowState.Maximized)
                {
                    newLeft = refWindow.RestoreBounds.Left + offset;
                    newTop = refWindow.RestoreBounds.Top + offset;
                }
                else
                {
                    newLeft = refWindow.Left + offset;
                    newTop = refWindow.Top + offset;
                }

                // 回绕处理：超出屏幕边界则返回左上角区域
                if (newLeft + winWidth > workArea.Right || newTop + winHeight > workArea.Bottom) { newLeft = workArea.Left + offset; newTop = workArea.Top + offset; }
                if (newLeft < workArea.Left) newLeft = workArea.Left;
                if (newTop < workArea.Top) newTop = workArea.Top;

                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = newLeft;
                this.Top = newTop;
                this.WindowState = WindowState.Normal;
            }
            else
            {
                // 首个窗口逻辑：恢复上次位置或居中
                if (settings.WindowLeft != AppConsts.UninitializedWindowPosition &&
                    settings.WindowTop != AppConsts.UninitializedWindowPosition)
                {
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = settings.WindowLeft;
                    this.Top = settings.WindowTop;
                    
                    // 额外检查：确保恢复的位置在当前可见区域内（防止多显示器断开后的问题）
                    if (this.Left + 100 > workArea.Right || this.Top + 100 > workArea.Bottom ||
                        this.Left + this.Width < workArea.Left || this.Top < workArea.Top)
                    {
                        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                }
                else
                {
                    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                if (settings.WindowState == (int)WindowState.Maximized)
                {
                    this.WindowState = WindowState.Maximized;
                }
            }
        }

        private void RestoreAppState()
        {
            try
            {
                var settings = TabPaint.SettingsManager.Instance.Current;

                // 初始化本地画笔参数
                var existingWindows = Application.Current.Windows.OfType<MainWindow>()
                    .Where(w => w != this && w.IsVisible).ToList();

                if (existingWindows.Count > 0)
                {
                    // 继承旧窗口参数（克隆最后获得焦点的窗口）
                    var refWindow = _lastFocusedInstance ?? existingWindows.FirstOrDefault(w => w != this);
                    if (refWindow != null && refWindow.LocalPerToolSettings != null)
                    {
                        this.LocalPerToolSettings = refWindow.LocalPerToolSettings.ToDictionary(
                            k => k.Key, v => v.Value.Clone());
                        this.CurrentToolKey = refWindow.CurrentToolKey;
                    }
                }
                else
                {
                    // 首个窗口：从设置读取
                    if (settings.PerToolSettings != null)
                    {
                        this.LocalPerToolSettings = settings.PerToolSettings.ToDictionary(
                            k => k.Key, v => v.Value.Clone());
                    }
                    this.CurrentToolKey = settings.CurrentToolKey;
                }

                // 显式同步到 Context 及其关联的 PenTool
                if (_ctx != null)
                {
                    _ctx.PenThickness = this.PenThickness;
                    _ctx.PenOpacity = this.PenOpacity;
                }
                
                // 强制触发通知，确保 Slider 绑定刷新
                OnPropertyChanged(nameof(PenThickness));
                OnPropertyChanged(nameof(PenOpacity));

                // 1. 恢复笔刷大小 (二次确认同步)
                if (_ctx != null) _ctx.PenThickness = this.PenThickness;
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
                if (_ctx != null) _ctx.PenStyle = targetStyle;
                if (_router != null)
                {
                    if (settings.LastToolName == "PenTool")
                    {
                        SetBrushStyle(_ctx.PenStyle);
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
        private AppLanguage _language = GetDefaultLanguage();

        private static AppLanguage GetDefaultLanguage()
        {
            try
            {
                string cultureName = CultureInfo.CurrentUICulture.Name;
                if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    return AppLanguage.ChineseSimplified;
                }
            }
            catch
            {
                // 忽略异常，默认返回英文或中文
            }
            return AppLanguage.English;
        }

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
        [JsonPropertyName("is_first_run")]
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
        private bool _isImageBarCompact = true; // 默认为 false (展开状态)

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
        private bool _alwaysShowTabCloseButton = false;

        [JsonPropertyName("always_show_tab_close_button")]
        public bool AlwaysShowTabCloseButton
        {
            get => _alwaysShowTabCloseButton;
            set
            {
                if (_alwaysShowTabCloseButton != value)
                {
                    _alwaysShowTabCloseButton = value;
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
        private string _currentToolKey = "Pen_Round";
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
                return 25.0; // Fallback
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
            dict["Pen_Round"] = new ToolSettingsModel { Thickness = 25.0, Opacity = 1.0 };
            dict["Pen_Square"] = new ToolSettingsModel { Thickness = 25.0, Opacity = 1.0 };
            dict["Pen_Highlighter"] = new ToolSettingsModel { Thickness = 20.0, Opacity = 0.5 }; // 荧光笔默认半透明
            dict["Pen_Eraser"] = new ToolSettingsModel { Thickness = 25.0, Opacity = 1.0 };
            dict["Pen_Watercolor"] = new ToolSettingsModel { Thickness = 30.0, Opacity = 0.8 };
            dict["Pen_Crayon"] = new ToolSettingsModel { Thickness = 25.0, Opacity = 1.0 };
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
        public BrushStyle LastBrushStyle { get; set; } = BrushStyle.Round;

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
                TabPaint.Services.ShortcutProvider.Instance.NotifyChanged();
            }
        }
        private bool _showRulers = true; // 默认不显示

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

        private bool _autoLoadFolderImages = false;

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
        private int _maxGlobalUndoSteps = 1000;
        [JsonPropertyName("max_global_undo_steps")]
        public int MaxGlobalUndoSteps
        {
            get => _maxGlobalUndoSteps;
            set
            {
                if (_maxGlobalUndoSteps != value)
                {
                    _maxGlobalUndoSteps = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _maxUndoMemoryMB = 2048;
        [JsonPropertyName("max_undo_memory_mb")]
        public int MaxUndoMemoryMB
        {
            get => _maxUndoMemoryMB;
            set
            {
                if (_maxUndoMemoryMB != value)
                {
                    _maxUndoMemoryMB = value;
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
        // 0. 基础文件
        { "File.New",            new ShortcutItem { Key = Key.N, Modifiers = ModifierKeys.Control } },
        { "File.Open",           new ShortcutItem { Key = Key.O, Modifiers = ModifierKeys.Control } },
        { "File.Save",           new ShortcutItem { Key = Key.S, Modifiers = ModifierKeys.Control } },
        { "File.SaveAs",         new ShortcutItem { Key = Key.S, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } },

        // 1. 全局/视图功能
        { "View.PrevImage",      new ShortcutItem { Key = Key.Left, Modifiers = ModifierKeys.None } },
        { "View.NextImage",      new ShortcutItem { Key = Key.Right, Modifiers = ModifierKeys.None } },
        { "View.RotateLeft",     new ShortcutItem { Key = Key.L, Modifiers = ModifierKeys.Control } },
        { "View.RotateRight",    new ShortcutItem { Key = Key.R, Modifiers = ModifierKeys.Control } },
        { "View.FullScreen",     new ShortcutItem { Key = Key.F11, Modifiers = ModifierKeys.None } },
        { "View.VerticalFlip",   new ShortcutItem { Key = Key.V, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 自动色阶
        { "View.HorizontalFlip",       new ShortcutItem { Key = Key.H, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 调整大小
        { "View.ToggleMinimize", new ShortcutItem { Key = Key.P, Modifiers = ModifierKeys.Control } },
        { "View.ZoomIn",         new ShortcutItem { Key = Key.OemPlus, Modifiers = ModifierKeys.Control } },
        { "View.ZoomOut",        new ShortcutItem { Key = Key.OemMinus, Modifiers = ModifierKeys.Control } },

        // 2. 高级工具 (Ctrl + Alt 系列)
        { "Tool.ClipMonitor",    new ShortcutItem { Key = Key.P, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 剪贴板监听开关
        { "Tool.RemoveBg",       new ShortcutItem { Key = Key.D1, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 抠图
        { "Tool.ChromaKey",      new ShortcutItem { Key = Key.D2, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.OCR",            new ShortcutItem { Key = Key.D3, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.ScreenPicker",   new ShortcutItem { Key = Key.D4, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } }, // 屏幕取色
        { "Tool.CopyColorCode",  new ShortcutItem { Key = Key.D5, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AutoCrop",       new ShortcutItem { Key = Key.D6, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AddBorder",      new ShortcutItem { Key = Key.D7, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AiUpscale",      new ShortcutItem { Key = Key.D8, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
        { "Tool.AiOcr",          new ShortcutItem { Key = Key.D9, Modifiers = ModifierKeys.Control | ModifierKeys.Alt } },
    

        // 3. 特殊操作 (Ctrl + Shift 系列)
        { "File.OpenWorkspace",  new ShortcutItem { Key = Key.O, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } }, // 打开工作区
        { "File.PasteNewTab",    new ShortcutItem { Key = Key.V, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } }, // 粘贴为新标签
        
        // 4. 编辑
        { "Edit.Undo",           new ShortcutItem { Key = Key.Z, Modifiers = ModifierKeys.Control } },
        { "Edit.Redo",           new ShortcutItem { Key = Key.Y, Modifiers = ModifierKeys.Control } },
        { "Edit.Copy",           new ShortcutItem { Key = Key.C, Modifiers = ModifierKeys.Control } },
        { "Edit.Cut",            new ShortcutItem { Key = Key.X, Modifiers = ModifierKeys.Control } },
        { "Edit.Paste",          new ShortcutItem { Key = Key.V, Modifiers = ModifierKeys.Control } },
        { "Edit.Bold",           new ShortcutItem { Key = Key.B, Modifiers = ModifierKeys.Control } },
        { "Edit.Italic",         new ShortcutItem { Key = Key.I, Modifiers = ModifierKeys.Control } },
        { "Edit.Underline",      new ShortcutItem { Key = Key.U, Modifiers = ModifierKeys.Control } },
        { "Edit.Strikethrough",  new ShortcutItem { Key = Key.T, Modifiers = ModifierKeys.Control | ModifierKeys.Shift } },

          // === 5. 基础绘图工具  ===
        { "Tool.SwitchToSelect", new ShortcutItem { Key = Key.D1, Modifiers = ModifierKeys.Control } }, // 选择
        { "Tool.SwitchToPen",    new ShortcutItem { Key = Key.D2, Modifiers = ModifierKeys.Control } }, // 铅笔/画笔
        { "Tool.SwitchToPick",   new ShortcutItem { Key = Key.D3, Modifiers = ModifierKeys.Control } }, // 取色
        { "Tool.SwitchToEraser", new ShortcutItem { Key = Key.D4, Modifiers = ModifierKeys.Control } }, // 橡皮
        { "Tool.SwitchToFill",   new ShortcutItem { Key = Key.D5, Modifiers = ModifierKeys.Control } }, // 填充
        { "Tool.SwitchToText",   new ShortcutItem { Key = Key.D6, Modifiers = ModifierKeys.Control } }, // 文字
        { "Tool.SwitchToBrush",  new ShortcutItem { Key = Key.D7, Modifiers = ModifierKeys.Control } }, // 画刷菜单(通常只切到默认画刷)
        { "Tool.SwitchToShape",  new ShortcutItem { Key = Key.D8, Modifiers = ModifierKeys.Control } }, // 形状菜单(通常切到默认形状)
          // === 6. 效果菜单 (Effect) ===
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
            OnPropertyChanged(nameof(Shortcuts));
        }
    }
}
