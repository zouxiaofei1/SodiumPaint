using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    public class ShortcutItem
    {
        public Key Key { get; set; } = Key.None;
        public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;

        [JsonIgnore] // 不需要保存到文件，仅用于UI显示
        public string DisplayText
        {
            get
            {
                if (Key == Key.None) return "无";
                var str = "";
                if (Modifiers.HasFlag(ModifierKeys.Control)) str += "Ctrl + ";
                if (Modifiers.HasFlag(ModifierKeys.Shift)) str += "Shift + ";
                if (Modifiers.HasFlag(ModifierKeys.Alt)) str += "Alt + ";
                if (Modifiers.HasFlag(ModifierKeys.Windows)) str += "Win + ";
                str += Key.ToString();
                return str;
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
          // === 3. 基础绘图工具 (新增) ===
        { "Tool.SwitchToPen",    new ShortcutItem { Key = Key.D1, Modifiers = ModifierKeys.Control } }, // 铅笔/画笔
        { "Tool.SwitchToPick",   new ShortcutItem { Key = Key.D2, Modifiers = ModifierKeys.Control } }, // 取色
        { "Tool.SwitchToEraser", new ShortcutItem { Key = Key.D3, Modifiers = ModifierKeys.Control } }, // 橡皮
        { "Tool.SwitchToSelect", new ShortcutItem { Key = Key.D4, Modifiers = ModifierKeys.Control } }, // 选择
        { "Tool.SwitchToFill",   new ShortcutItem { Key = Key.D5, Modifiers = ModifierKeys.Control } }, // 填充
        { "Tool.SwitchToText",   new ShortcutItem { Key = Key.D6, Modifiers = ModifierKeys.Control } }, // 文字
        { "Tool.SwitchToBrush",  new ShortcutItem { Key = Key.D7, Modifiers = ModifierKeys.Control } }, // 画刷菜单(通常只切到默认画刷)
        { "Tool.SwitchToShape",  new ShortcutItem { Key = Key.D8, Modifiers = ModifierKeys.Control } }, // 形状菜单(通常切到默认形状)
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
