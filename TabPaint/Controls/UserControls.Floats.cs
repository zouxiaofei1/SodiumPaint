//浮动面板控件延迟加载
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TabPaint.Controls;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private DownloadProgressFloat _downloadProgressPopup;
        private SelectionToolBar _selectionToolBar;
        public DownloadProgressFloat DownloadProgressPopup
        {
            get
            {
                if (_downloadProgressPopup == null)
                {
                    _downloadProgressPopup = new DownloadProgressFloat();
                    // 设置初始属性
                    _downloadProgressPopup.HorizontalAlignment = HorizontalAlignment.Center;
                    _downloadProgressPopup.VerticalAlignment = VerticalAlignment.Bottom;

                    // 绑定事件（如果有在 XAML 中绑定的事件，在这里用代码绑定）
                    _downloadProgressPopup.CancelRequested += OnDownloadCancelRequested;

                    // 放入占位符
                    DownloadProgressHolder.Content = _downloadProgressPopup;
                }
                return _downloadProgressPopup;
            }
        }
        public SelectionToolBar SelectionToolBar
        {
            get
            {
                if (_selectionToolBar == null)
                {
                    _selectionToolBar = new SelectionToolBar();
                    // 设置初始属性
                    _selectionToolBar.HorizontalAlignment = HorizontalAlignment.Left;
                    _selectionToolBar.VerticalAlignment = VerticalAlignment.Top;

                    // 绑定事件
                    _selectionToolBar.CopyClick += SelectionToolBar_CopyClick;
                    _selectionToolBar.AiRemoveBgClick += SelectionToolBar_AiRemoveBgClick;
                    _selectionToolBar.OcrClick += SelectionToolBar_OcrClick;

                    // 放入占位符
                    SelectionToolHolder.Content = _selectionToolBar;
                }
                return _selectionToolBar;
            }
        }

        public TextToolControl TextMenu { get; private set; }

        // 新增：延迟初始化文本工具栏
        private void EnsureTextToolLoaded()
        {

            if (TextMenu != null) return;
            TextMenu = new TextToolControl();

            TextMenu.FontFamilyCombo.SelectionChanged += FontSettingChanged;
            TextMenu.FontFamilyCombo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(FontSettingChanged));

            TextMenu.FontSizeCombo.SelectionChanged += FontSettingChanged;
            TextMenu.FontSizeCombo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(FontSettingChanged));
            TextMenu.FontFamilyBox.SelectionChanged += FontSettingChanged;
            TextMenu.FontSizeBox.SelectionChanged += FontSettingChanged;
            TextMenu.TextEditBar.MouseDown += TextEditBar_MouseDown; // 绑定回 MainWindow 的旧方法
            TextMenu.TextEditBar.MouseMove += TextEditBar_MouseMove;
            TextMenu.TextEditBar.MouseUp += TextEditBar_MouseUp;

            // 样式按钮
            TextMenu.BoldButton.Click += FontSettingChanged;
            TextMenu.ItalicButton.Click += FontSettingChanged;
            TextMenu.UnderlineButton.Click += FontSettingChanged;
            TextMenu.StrikeButton.Click += FontSettingChanged;

            TextMenu.TextBackgroundButton.Click += FontSettingChanged;
            TextMenu.SubscriptButton.Click += FontSettingChanged;
            TextMenu.SuperscriptButton.Click += FontSettingChanged;
            TextMenu.HighlightButton.Click += FontSettingChanged;
            TextMenu.ShadowButton.Click += FontSettingChanged;

            // 对齐
            TextMenu.AlignLeftButton.Click += TextAlign_Click;
            TextMenu.AlignCenterButton.Click += TextAlign_Click;
            TextMenu.AlignRightButton.Click += TextAlign_Click;

            // 表格
            TextMenu.InsertTableButton.Click += InsertTable_Click;

            // 注入界面
            TextToolHolder.Content = TextMenu;
        }
        private void InitializeDragWatchdog()
        {
            _dragWatchdog = new DispatcherTimer();
            _dragWatchdog.Interval = TimeSpan.FromMilliseconds(200);
            _dragWatchdog.Tick += DragWatchdog_Tick;
        }
        private void OnAppTitleBarIconDragRequest(object sender, MouseButtonEventArgs e)
        {
            // 1. 基础检查：单图模式且当前有 Tab
            if (MainImageBar.IsSingleTabMode && _currentTabItem != null)
            {
                try
                {
                    string dragFilePath = PrepareDragFilePath(_currentTabItem);

                    if (!string.IsNullOrEmpty(dragFilePath) && File.Exists(dragFilePath))
                    {
                        // 3. 构建拖拽数据
                        var dataObject = new DataObject(DataFormats.FileDrop, new string[] { dragFilePath });

                        // 4. 执行拖拽
                        // 使用 Copy | Move，允许复制到桌面或其他文件夹
                        DragDrop.DoDragDrop(AppTitleBar, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
                    }
                }
                catch (Exception ex)
                {
                    ShowToast(string.Format(LocalizationManager.GetString("L_Toast_DragFailed"), ex.Message));
                }
            }
        }
    }
}