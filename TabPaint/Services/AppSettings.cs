using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
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
    public enum AppTheme
    {
        Light,
        Dark,
        System // 跟随系统
    }
    public class ShortcutItem
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }

        // 修改这个属性的 get 方法
        public string DisplayText
        {
            get
            {
                if (Key == Key.None) return "无";

                StringBuilder sb = new StringBuilder();

                // 1. 处理修饰键 (Ctrl, Alt, Shift)
                if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control) sb.Append("Ctrl + ");
                if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) sb.Append("Shift + ");
                if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) sb.Append("Alt + ");
                if ((Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) sb.Append("Win + ");

                // 2. 处理按键显示的转换逻辑
                sb.Append(GetKeyDisplayName(Key));

                return sb.ToString();
            }
        }

        // === 核心修复逻辑 ===
        private string GetKeyDisplayName(Key key)
        {
            // 处理主键盘区的数字键 D0 - D9
            if (key >= Key.D0 && key <= Key.D9)
            {
                // Key.D0 的枚举值是 34，减去 34 即可得到数字 0
                return ((int)key - (int)Key.D0).ToString();
            }

            // 处理小键盘区的数字键 NumPad0 - NumPad9
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return "Num " + ((int)key - (int)Key.NumPad0).ToString();
            }

            // 处理其他常见特殊字符的友好显示 (可选)
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

            if (_router?.CurrentTool != null)
            {
                settings.LastToolName = _router.CurrentTool.GetType().Name;
            }
            if (_ctx != null)
            {
                settings.LastBrushStyle = _ctx.PenStyle;
            }
            TabPaint.SettingsManager.Instance.Save();
        }

        /// <summary>
        /// 2. 恢复上次的应用状态
        /// </summary>
        private void RestoreAppState()
        {
            try
            {

                var settings = TabPaint.SettingsManager.Instance.Current;

                // 1. 恢复笔刷大小
                if (_ctx != null)
                {
                    _ctx.PenThickness = settings.PenThickness;
                }

                // 2. 恢复工具和样式
                ITool targetTool = null; // 默认
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

                if (_ctx != null)
                {
                    _ctx.PenStyle = targetStyle;
                }

                // 3. 应用工具切换
                // 注意：这里需要确保界面元素(MainToolBar)已经加载完毕，否则高亮更新可能会空引用
                Dispatcher.InvokeAsync(() =>
                {
                    _router.SetTool(targetTool);

                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
            finally
            {

            }
        }
    }
    public class AppSettings : INotifyPropertyChanged
    {
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

        private double _penThickness = 5.0; // 默认值

        [JsonPropertyName("pen_thickness")]
        public double PenThickness
        {
            get => _penThickness;
            set
            {
                // 如果值没变，什么都不做
                if (Math.Abs(_penThickness - value) < 0.01) return;

                _penThickness = value;
                OnPropertyChanged();
            }
        }
        private double _penOpacity = 1.0; // 默认不透明 (0.0 到 1.0)
        [JsonPropertyName("pen_opacity")]
        public double PenOpacity
        {
            get => _penOpacity;
            set
            {
                if (_penOpacity != value)
                {
                    _penOpacity = value;
                    OnPropertyChanged(nameof(PenOpacity));
                }
            }
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
                    // 设置变更时，通知 ThemeManager 应用主题 (在外部监听或这里直接调用)
                    ThemeManager.ApplyTheme(value);
                }
            }
        }
        // 在 AppSettings 类中添加
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
                    //SettingsManager.Instance.Save(); // 自动保存
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

        private double _viewInterpolationThreshold = 70.0; 
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
        private double _paintInterpolationThreshold = 80.0; 
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
        { "Tool.SwitchToPen",    new ShortcutItem { Key = Key.D1, Modifiers = ModifierKeys.Control } }, // 铅笔/画笔
        { "Tool.SwitchToPick",   new ShortcutItem { Key = Key.D2, Modifiers = ModifierKeys.Control } }, // 取色
        { "Tool.SwitchToEraser", new ShortcutItem { Key = Key.D3, Modifiers = ModifierKeys.Control } }, // 橡皮
        { "Tool.SwitchToSelect", new ShortcutItem { Key = Key.D4, Modifiers = ModifierKeys.Control } }, // 选择
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
