

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