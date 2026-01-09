//using System.Runtime.InteropServices;
//using System.Windows;
//using TabPaint;

//private static void EnableAcrylic(IntPtr hwnd)
//{
//    // 1. 确保 DWM 合成已开启（Win10 默认开启，但在某些精简版或远程桌面可能关闭）
//    DwmIsCompositionEnabled(out bool compositionEnabled);
//    if (!compositionEnabled) return;

//    // 2. 关键：在 Win10 上，为了避免卡顿，必须确保 WPF 认为窗口是不透明的，
//    // 但 DWM 认为客户区是玻璃。
//    // 我们不需要修改 AllowsTransparency，但需要修改背景色。

//    System.Windows.Application.Current.Dispatcher.Invoke(() =>
//    {
//        var win = (MainWindow)System.Windows.Application.Current.MainWindow;
//        // 设置为纯透明，让 DWM 接管背景绘制
//        win.Background = Brushes.Transparent;
//        if (win.ResizeMode == ResizeMode.NoResize)
//            win.ResizeMode = ResizeMode.CanResize;
//    });

//    // 3. 启用亚克力 (使用原有 API，但参数微调)
//    var accent = new ACCENT_POLICY
//    {
//        AccentState = (int)AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT,
//        AccentFlags = 2,
//        GradientColor = 0x99F3F3F3
//    };

//    int accentSize = Marshal.SizeOf(accent);
//    IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
//    Marshal.StructureToPtr(accent, accentPtr, false);

//    var data = new WINDOWCOMPOSITIONATTRIBUTE_DATA
//    {
//        Attribute = 19, // WCA_ACCENT_POLICY
//        SizeOfData = accentSize,
//        Data = accentPtr
//    };

//    SetWindowCompositionAttribute(hwnd, ref data);
//    Marshal.FreeHGlobal(accentPtr);

//    // 4. 【核心修复】 DwmExtendFrameIntoClientArea
//    // 在 Win10 上，必须配合 WindowChrome 使用才能完美。
//    // 确保你的 XAML 中有 <WindowChrome.WindowChrome> 标签
//    var margins = new MARGINS(-1);
//    DwmExtendFrameIntoClientArea(hwnd, ref margins);
//}

//private static bool IsWin10OrLater()
//{
//    var version = Environment.OSVersion.Version.Major;
//    return version >= 10;
//}