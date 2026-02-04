//
//EventHandler.Dragdrop.cs
//处理全局的文件拖拽和文本拖拽逻辑，支持将图片拖入标签栏或画布，以及将文本拖入创建文字图层。
//
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static TabPaint.MainWindow;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void OnGlobalDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintSelectionDrag"))
            {
                HideDragOverlay();
                e.Effects = DragDropEffects.None; // 此时在窗口内显示禁止符号，或者改为 Move/Copy 也可以，只要不弹窗
                e.Handled = true;
                return;
            }
        
            // 1. 屏蔽程序内部拖拽 (如标签页排序)
            if (e.Data.GetDataPresent("TabPaintInternalDrag"))
            {
                Point pos = e.GetPosition(this);
                if (pos.Y < AppConsts.DragImageBarThresholdY && pos.Y > AppConsts.DragTitleBarThresholdY)
                {
                    HideDragOverlay();
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    return;
                }
            }
   
            // 2. 检查是否有文件拖入
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] allFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
                // 简单的图片过滤
                var imageFiles = allFiles?.Where(f => IsImageFile(f)).ToArray();

                if (imageFiles != null && imageFiles.Length > 0)
                {
                    Point pos = e.GetPosition(this); 
                    if (pos.Y <= AppConsts.DragTitleBarThresholdY)
                    {
                        e.Effects = DragDropEffects.Move;
                        ShowDragOverlay(
              LocalizationManager.GetString("L_Drag_SwitchWorkspace_Title"),
              LocalizationManager.GetString("L_Drag_SwitchWorkspace_Desc")
          );
                    }
                    // B. 其他区域 (菜单栏、工具栏、ImageBar、画布)
                    else
                    {
                        e.Effects = DragDropEffects.Copy;
                        if (imageFiles.Length > 1)
                        {
                            ShowDragOverlay(
                      LocalizationManager.GetString("L_Drag_BatchOpen_Title"),
                      string.Format(LocalizationManager.GetString("L_Drag_BatchOpen_Desc"), imageFiles.Length)
                  );
                        }
                        else
                        {
                            if (pos.Y < AppConsts.DragImageBarThresholdY)
                            {
                                ShowDragOverlay(
                                LocalizationManager.GetString("L_Drag_AddToList_Title"),
                                LocalizationManager.GetString("L_Drag_AddToList_Desc")
                            );
                            }
                            else
                            {
                                ShowDragOverlay(
                             LocalizationManager.GetString("L_Drag_Insert_Title"),
                             LocalizationManager.GetString("L_Drag_Insert_Desc")
                         );
                            }
                        }
                    }
                }
                else
                {
                    // 拖进来的不是图片
                    e.Effects = DragDropEffects.None;
                    HideDragOverlay();
                }
            }
            else if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText)|| e.Data.GetDataPresent(DataFormats.Rtf))
            {
                if ((e.AllowedEffects & DragDropEffects.Copy) == DragDropEffects.Copy)
                {
                    e.Effects = DragDropEffects.Copy;
                }
                // 如果不允许 Copy，检查是否允许 Move (比如从 Word 拖拽有时候是 Move)
                else if ((e.AllowedEffects & DragDropEffects.Move) == DragDropEffects.Move) e.Effects = DragDropEffects.Move;

                else
                {
                    e.Effects = e.AllowedEffects;
                }
                ShowDragOverlay(
           LocalizationManager.GetString("L_Drag_InsertText_Title"),
           LocalizationManager.GetString("L_Drag_InsertText_Desc")
       );
            }
            else
            {
                e.Effects = DragDropEffects.None;
                HideDragOverlay();
            }

            e.Handled = true;
        }
        private void InsertTabToCanvas(FileTabItem tab)
        {
            try
            {
                BitmapSource bitmapToInsert = null;

                bitmapToInsert = GetHighResImageForTab(tab);

                if (bitmapToInsert == null)
                {
                    ShowToast("L_Toast_NoImageData");
                    return;
                }
                _router.SetTool(_tools.Select);
                if (_tools.Select is SelectTool st)
                {
                    st.InsertImageAsSelection(_ctx, bitmapToInsert);
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_InsertFailed"), ex.Message));
            }
        }

        private async void OnGlobalDrop(object sender, DragEventArgs e)
        {
            HideDragOverlay(); 

            // 1. 屏蔽内部拖拽
            if (e.Data.GetDataPresent("TabPaintInternalDrag"))
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    var imageFiles = files?.Where(f => IsImageFile(f)).ToArray();
                    if (imageFiles[0].IndexOf(_currentTabItem.DisplayName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        e.Handled = true;
                        return;
                    }
                }
                else
                {
                    e.Handled = true; return; 
                }
            
            }
            // 2. 处理文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                // 再次过滤，确保安全
                var imageFiles = files?.Where(f => IsImageFile(f)).ToArray();

                if (imageFiles != null && imageFiles.Length > 0)
                {
                    Point pos = e.GetPosition(this);

                    // A. 标题栏区域 -> 切换工作区
                    if (pos.Y <= AppConsts.DragTitleBarThresholdY)
                    {
                        await SwitchWorkspaceToNewFile(imageFiles[0]);
                    }
                    else
                    {
                    // B-1. 多文件 -> 全部新建标签页
                    if (imageFiles.Length > 1) await OpenFilesAsNewTabs(imageFiles);

                    else
                    {
                        string filePath = imageFiles[0];

                        if (pos.Y < AppConsts.DragImageBarThresholdY) await OpenFilesAsNewTabs(new string[] { filePath });
                        else InsertImageToCanvas(filePath);
                    }
                    }
                }
                e.Handled = true;
            }
            else if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                bool hasRtf = e.Data.GetDataPresent(DataFormats.Rtf);
                bool hasText = e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text);

                if (hasRtf || hasText)
                {
                    string textToInsert = null;
                    TextStyleInfo styleInfo = null;

                    // 1. 优先尝试 RTF
                    if (hasRtf)
                    {
                        try
                        {
                            string rtfContent = e.Data.GetData(DataFormats.Rtf) as string;
                            styleInfo = TextFormatHelper.ParseRtf(rtfContent);
                            if (styleInfo != null)
                            {
                                textToInsert = styleInfo.Text;
                            }
                        }
                        catch { }
                    }

                    // 2. 如果 RTF 失败或没有，回退到普通文本
                    if (string.IsNullOrEmpty(textToInsert))
                    {
                        if (e.Data.GetDataPresent(DataFormats.UnicodeText))
                            textToInsert = (string)e.Data.GetData(DataFormats.UnicodeText);
                        else if (e.Data.GetDataPresent(DataFormats.Text))
                            textToInsert = (string)e.Data.GetData(DataFormats.Text);
                    }

                    if (!string.IsNullOrWhiteSpace(textToInsert))
                    {
                        Point dropPos = e.GetPosition(CanvasWrapper);

                        // 切换工具
                        _router.SetTool(_tools.Text);
                        if (styleInfo != null)
                        {
                            ApplyDetectedTextStyle(styleInfo);
                        }

                        // 生成文本框
                        InsertTextToCanvas(dropPos, textToInsert);
                    }
                    e.Handled = true;
                }
            }
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
        private void InsertTextToCanvas(Point viewPos, string text)
        {
            _router.SetTool(_tools.Text);
            if (_router.CurrentTool is TextTool textTool) textTool.SpawnTextBox(_ctx, viewPos, text);
        }
        private DispatcherTimer _dragWatchdog;
        private void DragWatchdog_Tick(object sender, EventArgs e)
        {
            if (!_isDragOverlayVisible || this.WindowState == WindowState.Minimized)
            {
                _dragWatchdog.Stop();
                return;
            }

            POINT cursorScreenPos;
            GetCursorPos(out cursorScreenPos);

            // 获取窗口物理位置
            Point p1 = this.PointToScreen(new Point(0, 0));
            Point p2 = this.PointToScreen(new Point(this.ActualWidth, this.ActualHeight));
            double padding = AppConsts.DragGlobalPadding;

            bool isInside = (cursorScreenPos.X >= p1.X - padding && cursorScreenPos.X <= p2.X + padding &&
                             cursorScreenPos.Y >= p1.Y - padding && cursorScreenPos.Y <= p2.Y + padding);

            if (!isInside)
            {
                HideDragOverlay();
            }
        }

        private void OnGlobalDragLeave(object sender, DragEventArgs e)
        {
            // 获取当前窗口
            var window = Window.GetWindow(this);
            if (window == null) return;

            POINT cursorScreenPos;
            GetCursorPos(out cursorScreenPos);

            Point p1 = window.PointToScreen(new Point(0, 0));
            Point p2 = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));

            // 计算物理像素下的窗口范围
            double left = p1.X;
            double top = p1.Y;
            double right = p2.X;
            double bottom = p2.Y;
            bool isInside = (cursorScreenPos.X >= left && cursorScreenPos.X <= right &&
                             cursorScreenPos.Y >= top && cursorScreenPos.Y <= bottom);

            if (!isInside)
            {
                HideDragOverlay();
            }
        }
        private void ShowDragOverlay(string title, string subText)
        {
            // 更新文字内容
            DragOverlayText.Text = title;
            DragOverlaySubText.Text = subText;

            if (_isDragOverlayVisible) return;

            _isDragOverlayVisible = true;

            // 播放淡入动画
            Storyboard fadeIn = (Storyboard)this.Resources["FadeInDragOverlay"];
            fadeIn.Begin(); _dragWatchdog.Start();
        }

        private void HideDragOverlay()
        {
            if (!_isDragOverlayVisible) return;

            _isDragOverlayVisible = false;

            // 播放淡出动画
            Storyboard fadeOut = (Storyboard)this.Resources["FadeOutDragOverlay"];
            fadeOut.Begin(); _dragWatchdog.Stop();
        }

        private void InsertImageToCanvas(string filePath)
        {
            try
            {
                BitmapSource bitmap;
                string ext = System.IO.Path.GetExtension(filePath)?.ToLower();
                if (ext == ".svg")
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    bitmap = DecodeSvg(bytes, CancellationToken.None);
                }
                else
                {
                    BitmapImage bmi = new BitmapImage();
                    bmi.BeginInit();
                    bmi.UriSource = new Uri(filePath);
                    bmi.CacheOption = BitmapCacheOption.OnLoad; // 必须 OnLoad 才能解除文件占用
                    bmi.EndInit();
                    bmi.Freeze(); // 性能优化
                    bitmap = bmi;
                }

                if (bitmap == null) return;

                // 切换到选择工具
                _router.SetTool(_tools.Select);

                if (_tools.Select is SelectTool st)
                {
                    st.InsertImageAsSelection(_ctx, bitmap);
                }
            }
            catch (Exception ex)
            {
                ShowToast(string.Format(LocalizationManager.GetString("L_Toast_CannotInsertImage"), ex.Message));
            }
        }
    }
}