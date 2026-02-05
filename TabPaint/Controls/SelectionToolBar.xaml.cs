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
        }

        public event RoutedEventHandler CopyClick;
        public event RoutedEventHandler AiRemoveBgClick;
        public event RoutedEventHandler OcrClick;
    }
}
