

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

