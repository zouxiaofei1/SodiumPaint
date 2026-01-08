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

//

//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private void HandleDeleteFileAction()
        {
            if (_currentTabItem == null) return;

            var tab = _currentTabItem;

            // 1. 记录撤销所需信息
            _lastDeletedTabForUndo = tab;
            _lastDeletedTabIndex = FileTabs.IndexOf(tab);

            // 2. 加入待删除队列
            _pendingDeletionTabs.Add(tab);

            // 3. UI 上移除 (让用户感觉已经删了)
            FileTabs.Remove(tab);
            _imageFiles.Remove(tab.FilePath);

            // 4. 切换焦点
            if (FileTabs.Count == 0)
            {
                a.s("switch to new canvas");
                ResetToNewCanvas();
            }
            else
            {
                int nextIndex = Math.Min(_lastDeletedTabIndex, FileTabs.Count - 1);
                SwitchToTab(FileTabs[nextIndex]);
            }

            // 5. 启动/重置 2秒计时器
            _deleteCommitTimer.Stop();
            _deleteCommitTimer.Start();

            // 6. 弹出提示
            ShowToast("已删除 (Ctrl+Z 撤销)");
        }
        private void CommitPendingDeletions()
        {
            _deleteCommitTimer.Stop();

            foreach (var tab in _pendingDeletionTabs)
            {
                try
                {
                    // 只有真实存在的文件才进回收站
                    if (!IsVirtualPath(tab.FilePath) && File.Exists(tab.FilePath))
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            tab.FilePath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    // 如果有备份文件，也顺便清理
                    if (!string.IsNullOrEmpty(tab.BackupPath) && File.Exists(tab.BackupPath))
                    {
                        File.Delete(tab.BackupPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"删除提交失败: {ex.Message}");
                }
            }

            // 清空列表和撤销引用
            _pendingDeletionTabs.Clear();
            _lastDeletedTabForUndo = null;
        }

        private void RestoreLastDeletedTab()
        {
            if (_pendingDeletionTabs.Count == 0) return;

            // 取出最后一个删除的 Tab
            var tabToRestore = _pendingDeletionTabs.Last();
            _pendingDeletionTabs.Remove(tabToRestore);
        
            // 停止计时器 (如果列表空了)
            if (_pendingDeletionTabs.Count == 0) _deleteCommitTimer.Stop();
            //if (IsVirtualPath(tabToRestore.FilePath))
            //{
            //    var conflictTab = FileTabs.FirstOrDefault(t => t.FilePath == tabToRestore.FilePath);

            //    if (conflictTab != null)
            //    {
            //        if (conflictTab.IsNew && !conflictTab.IsDirty)
            //        {
            //            FileTabs.Remove(conflictTab);
            //            _imageFiles.Remove(conflictTab.FilePath);
            //        }
            //        else
            //        {
            //            int newNum = GetNextAvailableUntitledNumber();           //            string newPath = $"{VirtualFilePrefix}{newNum}";

            //            tabToRestore.UntitledNumber = newNum;
            //            tabToRestore.FilePath = newPath;
            //            tabToRestore.Id = $"Virtual_{newNum}"; 
            //        }
            //    }
            //}
            // 恢复到 UI 列表
            if (_lastDeletedTabIndex >= 0 && _lastDeletedTabIndex <= FileTabs.Count)
                FileTabs.Insert(_lastDeletedTabIndex, tabToRestore);
            else
                FileTabs.Add(tabToRestore);

            // 恢复到路径列表
            if (!_imageFiles.Contains(tabToRestore.FilePath))
            {
                if (_lastDeletedTabIndex < _imageFiles.Count)
                    _imageFiles.Insert(_lastDeletedTabIndex, tabToRestore.FilePath);
                else
                    _imageFiles.Add(tabToRestore.FilePath);
            }

            // 马上切回这个 Tab
            SwitchToTab(tabToRestore);

            ShowToast("已撤销删除");
        }
    }
}