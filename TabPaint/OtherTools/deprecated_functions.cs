

//private async void OnCanvasDrop(object sender, System.Windows.DragEventArgs e)
//{
//    HideDragOverlay();
//    if (e.Data.GetDataPresent("TabPaintInternalDrag"))
//    {
//        e.Handled = true;
//        return;
//    }

//    if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
//    {
//        string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
//        if (files != null && files.Length > 0)
//        {
//            // --- 核心修改：逻辑分流 ---
//            if (files.Length > 1)
//            {
//                // 如果是多文件，走新建标签页逻辑
//                await OpenFilesAsNewTabs(files);
//            }
//            else
//            {
//                // 如果是单文件，走原有的“插入当前画布”逻辑
//                string filePath = files[0];
//                try
//                {
//                    BitmapImage bitmap = new BitmapImage();
//                    bitmap.BeginInit();
//                    bitmap.UriSource = new Uri(filePath);
//                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
//                    bitmap.EndInit();
//                    bitmap.Freeze(); // 保持你的建议，加上 Freeze

//                    _router.SetTool(_tools.Select);

//                    if (_tools.Select is SelectTool st)
//                    {
//                        st.InsertImageAsSelection(_ctx, bitmap);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    System.Windows.MessageBox.Show("无法识别的图片格式: " + ex.Message);
//                }
//            }
//            e.Handled = true;
//        }
//    }
//}

//using System.Diagnostics;

//private async Task LoadAndDisplayImageInternalAsync(string filePath)
//{
//    try
//    {
//        OpenImageAndTabs(filePath);
//        //int newIndex = _imageFiles.IndexOf(filePath);
//        //if (newIndex < 0) return;
//        //_currentImageIndex = newIndex;

//        //foreach (var tab in FileTabs)
//        //    tab.IsSelected = false;

//        //FileTabItem current = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
//        //current.IsSelected=true;
//        //// 3. 加载主图片
//        //await LoadImage(filePath); // 假设这是您加载大图的方法

//        //await RefreshTabPageAsync(_currentImageIndex);
//        //_currentTabItem = current;
//        ////a.s(_currentTabItem.FilePath);
//        //SetPreviewSlider(); 
//        //UpdateWindowTitle();
//    }
//    catch (Exception ex)
//    {
//        // 最好有异常处理
//        Debug.WriteLine($"Error loading image {filePath}: {ex.Message}");
//    }
//}



//using System.Globalization;
//using System.Windows.Data;

//namespace TabPaint.Converters
//{
//    // 将 Slider 的线性刻度转换为指数级增长的实际缩放值
//    public class LogarithmicScaleConverter : IValueConverter
//    {
//        // 目标真实倍率范围
//        private const double RealMin = 0.1;  // 10%
//        private const double RealMax = 16.0; // 1600%

//        // Slider 控件的逻辑范围 (XAML里要对应设置 Minimum=0 Maximum=100)
//        private const double SliderMin = 0.0;
//        private const double SliderMax = 100.0;

//        // 计算常数
//        private static readonly double LogRealMin = Math.Log(RealMin);
//        private static readonly double LogRealRange = Math.Log(RealMax) - Math.Log(RealMin);
//        private static readonly double SliderRange = SliderMax - SliderMin;

//        /// <summary>
//        /// ViewModel (真实倍率) -> View (Slider位置 0-100)
//        /// </summary>
//        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
//        {
//            if (value is double realZoom)
//            {
//                // 边界保护
//                if (realZoom <= RealMin) return SliderMin;
//                if (realZoom >= RealMax) return SliderMax;

//                // 公式: slider = (log(zoom) - log(min)) / (log(max) - log(min)) * 100
//                double sliderVal = ((Math.Log(realZoom) - LogRealMin) / LogRealRange) * SliderRange + SliderMin;
//                return sliderVal;
//            }
//            return SliderMin;
//        }

//        /// <summary>
//        /// View (Slider位置 0-100) -> ViewModel (真实倍率)
//        /// </summary>
//        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
//        {
//            if (value is double sliderVal)
//            {
//                // 边界保护
//                if (sliderVal <= SliderMin) return RealMin;
//                if (sliderVal >= SliderMax) return RealMax;

//                // 公式: zoom = exp( (slider/100) * (log(max)-log(min)) + log(min) )
//                double relativePos = (sliderVal - SliderMin) / SliderRange;
//                double realZoom = Math.Exp(relativePos * LogRealRange + LogRealMin);

//                return Math.Round(realZoom, 2); // 保留两位小数
//            }
//            return RealMin;
//        }
//    }
//}

//if (!tab.IsNew &&
//    !string.IsNullOrEmpty(_currentFilePath) &&
//    tab.FilePath == _currentFilePath)
//{
//    return;
//}
// s(1);



// 1. 清理底层画布
//Clean_bitmap(1200, 900);

//// 2. 重置窗口标题
//_currentFilePath = string.Empty;
//_currentFileName = "未命名";
//UpdateWindowTitle();

//var newTab = CreateNewUntitledTab();
//newTab.IsSelected = true; // 设为选中态
//FileTabs.Add(newTab);
//_currentTabItem = newTab;

//// 5. 重置撤销栈和脏状态追踪
//ResetDirtyTracker();

//// 6. 滚动视图归位
//MainImageBar.Scroller.ScrollToHorizontalOffset(0);
//< DrawingImage x: Key = "rotate-left-svgrepo-com" >
//    < DrawingImage.Drawing >
//        < GeometryDrawing Brush = "#FF000000" >
//            < GeometryDrawing.Geometry >
//                < PathGeometry FillRule = "Nonzero" Figures = "M213.3330078125,746.6669921875C154.42300415039062,746.6669921875,106.66699981689453,794.4229736328125,106.66699981689453,853.3330078125L106.66699981689453,1706.6700439453125C106.66699981689453,1765.5799560546875,154.42300415039062,1813.3299560546875,213.3330078125,1813.3299560546875L1066.6700439453125,1813.3299560546875C1125.5799560546875,1813.3299560546875,1173.3299560546875,1765.5799560546875,1173.3299560546875,1706.6700439453125L1173.3299560546875,853.3330078125C1173.3299560546875,794.4229736328125,1125.5799560546875,746.6669921875,1066.6700439453125,746.6669921875L213.3330078125,746.6669921875z M213.3330078125,640L1066.6700439453125,640C1184.489990234375,640,1280,735.5130004882812,1280,853.3330078125L1280,1706.6700439453125C1280,1824.489990234375,1184.489990234375,1920,1066.6700439453125,1920L213.3330078125,1920C95.51300048828125,1920,0,1824.489990234375,-5.7220458984375E-06,1706.6700439453125L-5.7220458984375E-06,853.3330078125C0,735.5130004882812,95.51300048828125,640,213.3330078125,640z M1178.93994140625,0L1254.3699951171875,75.42500305175781 1120.030029296875,209.7659912109375 1280,209.7659912109375C1515.6400146484375,209.76600646972656,1706.6700439453125,400.7909851074219,1706.6700439453125,636.4320068359375L1600,636.4320068359375C1600,459.70098876953125,1456.72998046875,316.4320068359375,1280,316.4320068359375L1120,316.4320068359375 1251.449951171875,447.88299560546875 1176.030029296875,523.3070068359375 915.8330078125,263.1109924316406 1178.93994140625,0z" />
//            </ GeometryDrawing.Geometry >
//        </ GeometryDrawing >
//    </ DrawingImage.Drawing >
//</ DrawingImage >


//var frozenDrawingImage = (DrawingImage)image.Data; // 获取当前 UI 使用的绘图对象
//if (frozenDrawingImage == null) return;
//var modifiableDrawingImage = frozenDrawingImage.Clone();    // 克隆出可修改的副本
//if (modifiableDrawingImage.Drawing is GeometryDrawing geoDrawing)  // DrawingImage.Drawing 可能是 DrawingGroup 或 GeometryDrawing
//{
//    geoDrawing.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
//}
//else if (modifiableDrawingImage.Drawing is DrawingGroup group)
//{
//    foreach (var child in group.Children)
//    {
//        if (child is GeometryDrawing childGeo)
//        {
//            childGeo.Brush = isEnabled ? Brushes.Black : Brushes.Gray;
//        }
//    }
//}

//// 替换 Image.Source，让 UI 用新的对象
//image.Data = modifiableDrawingImage;

//using System.Windows;
//using TabPaint;
//using static TabPaint.MainWindow;

//private void SetPenResizeBarVisibility()
//{
//    if (((_router.CurrentTool is PenTool && _ctx.PenStyle != BrushStyle.Pencil) || _router.CurrentTool is ShapeTool) && !IsViewMode)
//        ((MainWindow)System.Windows.Application.Current.MainWindow).ThicknessPanel.Visibility = Visibility.Visible;
//    else
//    {
//        ((MainWindow)System.Windows.Application.Current.MainWindow).ThicknessPanel.Visibility = Visibility.Collapsed;

//    }

//}
//private void SetOpacityBarVisibility()
//{
//    if ((_router.CurrentTool is PenTool || _router.CurrentTool is TextTool || _router.CurrentTool is PenTool || _router.CurrentTool is ShapeTool) && !IsViewMode)
//        ((MainWindow)System.Windows.Application.Current.MainWindow).OpacityPanel.Visibility = Visibility.Visible;
//    else

//    {
//        ((MainWindow)System.Windows.Application.Current.MainWindow).OpacityPanel.Visibility = Visibility.Collapsed;
//    }

//}