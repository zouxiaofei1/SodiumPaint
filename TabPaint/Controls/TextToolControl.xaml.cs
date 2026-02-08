using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TabPaint.Controls
{
    public partial class TextToolControl : UserControl
    {
        private Point _dragStartPoint;
        private bool _isDragging = false;

        public TextToolControl()
        {
            InitializeComponent();
        }

        #region Drag Logic (Self Contained)
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(this);
                MainBorder.CaptureMouse();
            }
        }
        private Point _lastDragPoint; // 记录上一次鼠标相对于窗口的位置
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var parentWindow = Window.GetWindow(this);
                if (parentWindow == null) return;

                // 获取当前鼠标相对于当前窗口的坐标
                var currentPoint = e.GetPosition(parentWindow);

                if (_lastDragPoint != default)
                {
                    double deltaX = currentPoint.X - _lastDragPoint.X;
                    double deltaY = currentPoint.Y - _lastDragPoint.Y;

                   // DragTransform.X += deltaX;
                  //  DragTransform.Y += deltaY;
                }

                _lastDragPoint = currentPoint;
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                MainBorder.ReleaseMouseCapture();
            }
        }
        #endregion

        public TranslateTransform TextBarDragTransform => DragTransform;
        public Border TextEditBar => MainBorder;

        public ComboBox FontFamilyCombo => FontFamilyBox;
        public ComboBox FontSizeCombo => FontSizeBox;
        public ToggleButton BoldButton => BoldBtn;
        public ToggleButton ItalicButton => ItalicBtn;
        public ToggleButton UnderlineButton => UnderlineBtn;
        public ToggleButton StrikeButton => StrikeBtn;

        public ToggleButton AlignLeftButton => AlignLeftBtn;
        public ToggleButton AlignCenterButton => AlignCenterBtn;
        public ToggleButton AlignRightButton => AlignRightBtn;

        public ToggleButton TextBackgroundButton => TextBackgroundBtn;
        public ToggleButton SubscriptButton => SubscriptBtn;
        public ToggleButton SuperscriptButton => SuperscriptBtn;
        public ToggleButton HighlightButton => HighlightBtn;
        public ToggleButton ShadowButton => ShadowBtn;
        public Button InsertTableButton => InsertTableBtn;
    }
}
