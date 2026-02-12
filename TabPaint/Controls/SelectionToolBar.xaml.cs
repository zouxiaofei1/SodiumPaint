using System.Windows;
using System.Windows.Controls;

namespace TabPaint.Controls
{
    public partial class SelectionToolBar : UserControl
    {
        public SelectionToolBar()
        {
            InitializeComponent();
            
            CopyBtn.Click += (s, e) => CopyClick?.Invoke(this, e);
            AiRemoveBgBtn.Click += (s, e) => AiRemoveBgClick?.Invoke(this, e);
            OcrBtn.Click += (s, e) => OcrClick?.Invoke(this, e);
            RotateBtn.Click += (s, e) => RotateClick?.Invoke(this, e);
        }

        public event RoutedEventHandler CopyClick;
        public event RoutedEventHandler AiRemoveBgClick;
        public event RoutedEventHandler OcrClick;
        public event RoutedEventHandler RotateClick;

        public bool IsRotateChecked
        {
            get => ((RotateBtn as object) as System.Windows.Controls.Primitives.ToggleButton)?.IsChecked ?? false;
            set
            {
                if ((RotateBtn as object) is System.Windows.Controls.Primitives.ToggleButton tb)
                    tb.IsChecked = value;
            }
        }
    }
}
