//
//UserControls.cs
//负责在主窗口中延迟初始化并注入各个自定义控件（ImageBar、MenuBar、ToolBar等），并绑定相关事件。
//
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
        private void InitializeLazyControls()
        {
            // 第一帧：加载 ImageBar
            InitializeImageBar();

            // 使用异步分帧加载重型控件，避免主线程阻塞
            Dispatcher.BeginInvoke(new Action(() =>
            {
                InitializeMenuBar();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    InitializeToolBar();

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        InitializeStatusBar();
                        InitializeDragWatchdog();
                    }), DispatcherPriority.Background);
                }), DispatcherPriority.Background);
            }), DispatcherPriority.Background);
        }

        private void InitializeImageBar()
        {
            MainImageBar = new ImageBarControl();
            var viewModeBinding = new Binding("IsViewMode") { Source = this, Mode = BindingMode.OneWay };
            BindingOperations.SetBinding(MainImageBar, ImageBarControl.IsViewModeProperty, viewModeBinding);
            MainImageBar.SaveAllClick += OnSaveAllClick;
            MainImageBar.SaveAllDoubleClick += OnSaveAllDoubleClick;
            MainImageBar.ClearUneditedClick += OnClearUneditedClick;
            MainImageBar.DiscardAllClick += OnDiscardAllClick;
            MainImageBar.PrependTabClick += OnPrependTabClick;
            MainImageBar.NewTabClick += OnNewTabClick;
            MainImageBar.TabStickImageClick += OnTabStickImageClick;
            MainImageBar.FileTabClick += OnFileTabClick;
            MainImageBar.FileTabCloseClick += OnFileTabCloseClick;
            MainImageBar.FileTabPreviewMouseDown += OnFileTabPreviewMouseDown;
            MainImageBar.FileTabPreviewMouseMove += OnFileTabPreviewMouseMove;
            MainImageBar.FileTabDrop += OnFileTabDrop;
            MainImageBar.FileTabLeave += OnFileTabLeave;
            MainImageBar.FileTabReorderDragOver += OnFileTabReorderDragOver;
            MainImageBar.FileTabsWheelScroll += OnFileTabsWheelScroll;
            MainImageBar.FileTabsScrollChanged += OnFileTabsScrollChanged;
            MainImageBar.PreviewSliderValueChanged += PreviewSlider_ValueChanged;
            MainImageBar.SliderPreviewMouseWheel += Slider_PreviewMouseWheel;
            MainImageBar.TabCopyClick += OnTabCopyClick;
            MainImageBar.TabCutClick += OnTabCutClick;
            MainImageBar.TabPasteClick += OnTabPasteClick;
            MainImageBar.TabOpenFolderClick += OnTabOpenFolderClick;
            MainImageBar.TabDeleteClick += OnTabDeleteClick;
            MainImageBar.TabFileDeleteClick += OnTabFileDeleteClick;
            ImageBarHolder.Content = MainImageBar;
            if (MainImageBar != null)
                MainImageBar.IsCompactMode = SettingsManager.Instance.Current.IsImageBarCompact;
        }

        private void InitializeMenuBar()
        {
            MainMenu = new MenuBarControl();
            MainMenu.NewClick += OnNewClick;
            MainMenu.OpenClick += OnOpenClick;
            MainMenu.OpenWorkspaceClick += OnOpenWorkspaceClick;
            MainMenu.SaveClick += OnSaveClick;
            MainMenu.SaveAsClick += OnSaveAsClick;
            MainMenu.NewWindowClick += OnNewWindowClick;
            MainMenu.ExitClick += OnExitClick;
            MainMenu.RecentFileClick += OnRecentFileClick;
            MainMenu.ClearRecentFilesClick += OnClearRecentFilesClick;
            MainMenu.WatermarkClick += OnWatermarkClick;
            MainMenu.CopyClick += OnCopyClick;
            MainMenu.CutClick += OnCutClick;
            MainMenu.PasteClick += OnPasteClick;
            MainMenu.ResizeCanvasClick += OnResizeCanvasClick;
            MainMenu.BCEClick += OnBrightnessContrastExposureClick;
            MainMenu.TTSClick += OnColorTempTintSaturationClick;
            MainMenu.BlackWhiteClick += OnConvertToBlackAndWhiteClick;
            MainMenu.InvertClick += OnInvertColorsClick;
            MainMenu.AutoLevelsClick += OnAutoLevelsClick;
            MainMenu.UndoClick += OnUndoClick;
            MainMenu.RedoClick += OnRedoClick;
            MainMenu.SettingsClick += OnSettingsClick;
            MainMenu.NewTabClick += OnNewTabClick;
            MainMenu.DiscardAllClick += OnDiscardAllClick;
            MainMenu.SepiaClick += OnSepiaClick;
            MainMenu.OilPaintingClick += OnOilPaintingClick;
            MainMenu.VignetteClick += OnVignetteClick;
            MainMenu.GlowClick += OnGlowClick;
            MainMenu.SharpenClick += OnSharpenClick;
            MainMenu.BrownClick += OnBrownClick;
            MainMenu.MosaicClick += OnMosaicClick;
            MainMenu.GaussianBlurClick += OnGaussianBlurClick;
            MenuBarHolder.Content = MainMenu;

            SetUndoRedoButtonState();
        }

        private void InitializeToolBar()
        {
            MainToolBar = new ToolBarControl();
            MainToolBar.PenClick += OnPenClick;
            MainToolBar.PickColorClick += OnPickColorClick;
            MainToolBar.EraserClick += OnEraserClick;
            MainToolBar.SelectClick += OnSelectClick;
            MainToolBar.FillClick += OnFillClick;
            MainToolBar.TextClick += OnTextClick;
            MainToolBar.BrushStyleClick += OnBrushStyleClick;
            MainToolBar.ShapeStyleClick += OnShapeStyleClick;
            MainToolBar.BrushMainClick += OnBrushMainClick;
            MainToolBar.ShapeMainClick += OnShapeMainClick;
            MainToolBar.CropClick += CropMenuItem_Click;
            MainToolBar.RotateLeftClick += OnRotateLeftClick;
            MainToolBar.RotateRightClick += OnRotateRightClick;
            MainToolBar.Rotate180Click += OnRotate180Click;
            MainToolBar.FlipVerticalClick += OnFlipVerticalClick;
            MainToolBar.FlipHorizontalClick += OnFlipHorizontalClick;
            MainToolBar.CustomColorClick += OnCustomColorClick;
            MainToolBar.ColorOneClick += OnColorOneClick;
            MainToolBar.ColorTwoClick += OnColorTwoClick;
            MainToolBar.ColorButtonClick += OnColorButtonClick;
            MainToolBar.SelectMainClick += OnSelectMainClick;
            MainToolBar.SelectStyleClick += OnSelectStyleClick;
            ToolBarHolder.Content = MainToolBar;

            // 确保工具栏加载后更新高亮状态
            UpdateToolSelectionHighlight();
            UpdateColorHighlight();
            SetCropButtonState();
        }

        private void InitializeStatusBar()
        {
            MyStatusBar = new StatusBarControl();
            MyStatusBar.ClipboardMonitorClick += ClipboardMonitorToggle_Click;
            MyStatusBar.FitToWindowClick += FitToWindow_Click;
            MyStatusBar.ZoomOutClick += ZoomOut_Click;
            MyStatusBar.ZoomInClick += ZoomIn_Click;
            MyStatusBar.ZoomChanged += OnStatusBarZoomChanged;
            AppTitleBar.NewClick += OnNewClick;
            AppTitleBar.OpenClick += OnOpenClick;
            AppTitleBar.OpenWorkspaceClick += OnOpenWorkspaceClick;
            AppTitleBar.SaveClick += OnSaveClick;
            AppTitleBar.SaveAsClick += OnSaveAsClick;
            AppTitleBar.ExitClick += OnExitClick;
            AppTitleBar.IconDragRequest += OnAppTitleBarIconDragRequest;
            AppTitleBar.LogoMiddleClick += OnAppTitleBarLogoMiddleClick;
            AppTitleBar.HelpClick += OnHelpClick;
            StatusBarHolder.Content = MyStatusBar;

            UpdateUIStatus(zoomscale);
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