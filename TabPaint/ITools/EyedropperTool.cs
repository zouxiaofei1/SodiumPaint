
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

//
//取色工具
//

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public class EyedropperTool : ToolBase
        {
            public override string Name => "Eyedropper";

            // 1. 定义一个私有变量缓存光标
            private System.Windows.Input.Cursor _cachedCursor;

            // 2. 修改属性获取逻辑
            public override System.Windows.Input.Cursor Cursor
            {
                get
                {
                    if (_cachedCursor == null)
                    {
                        // 仅在第一次访问时加载
                        var resourceInfo = System.Windows.Application.GetResourceStream(
                            new Uri("pack://application:,,,/Resources/Cursors/Eyedropper.cur"));

                        if (resourceInfo != null)
                        {
                            _cachedCursor = new System.Windows.Input.Cursor(resourceInfo.Stream);
                        }
                        else
                        {
                            // 防止资源没找到导致后续空指针，回退到默认
                            return System.Windows.Input.Cursors.Cross;
                        }
                    }
                    return _cachedCursor;
                }
            }

            public override void OnPointerDown(ToolContext ctx, Point viewPos)
            {
                var px = ctx.ToPixel(viewPos);
                // 添加边界检查，防止点击画布外崩溃
                if (px.X >= 0 && px.Y >= 0 && px.X < ctx.Surface.Bitmap.PixelWidth && px.Y < ctx.Surface.Bitmap.PixelHeight)
                {
                    ctx.PenColor = ctx.Surface.GetPixel((int)px.X, (int)px.Y);
                    ((MainWindow)System.Windows.Application.Current.MainWindow).UpdateCurrentColor(ctx.PenColor, ((MainWindow)System.Windows.Application.Current.MainWindow).useSecondColor);

                    // 取色后自动切回上一个工具
                    ((MainWindow)System.Windows.Application.Current.MainWindow)._router.SetTool(((MainWindow)System.Windows.Application.Current.MainWindow).LastTool);
                }
            }
        }
    }
}