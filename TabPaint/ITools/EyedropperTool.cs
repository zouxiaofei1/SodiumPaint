
//
//EyedropperTool.cs
//取色工具实现，允许用户从画布上拾取颜色并自动更新当前前景色或背景色，随后切回上一个使用的工具。
//
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace TabPaint
{
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        public class EyedropperTool : ToolBase
        {
            public override string Name => "Eyedropper";
            private System.Windows.Input.Cursor _cachedCursor;
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
            public override void SetCursor(ToolContext ctx)
            {
                System.Windows.Input.Mouse.OverrideCursor = null;

                if (ctx.ViewElement != null)
                {
                    ctx.ViewElement.Cursor = this.Cursor;
                }
            }
            public override void OnPointerDown(ToolContext ctx, Point viewPos, float pressure = 1.0f)
            {
                MainWindow  mw = (MainWindow)System.Windows.Application.Current.MainWindow;
                if (mw.IsViewMode) return;
                var px = ctx.ToPixel(viewPos);
                if (px.X >= 0 && px.Y >= 0 && px.X < ctx.Surface.Bitmap.PixelWidth && px.Y < ctx.Surface.Bitmap.PixelHeight)
                {
                    ctx.PenColor = ctx.Surface.GetPixel((int)px.X, (int)px.Y);
                   mw.UpdateCurrentColor(ctx.PenColor, mw.useSecondColor);

                    // 取色后自动切回上一个工具
                    if(mw.LastTool!=null)
                        mw._router.SetTool(mw.LastTool);
                }
            }
        }
    }
}