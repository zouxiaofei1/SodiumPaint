using System.Windows;

namespace TabPaint.UIHandlers
{
    public partial class DropZoneWindow : Window
    {
        public event DragEventHandler TabDropped;

        public DropZoneWindow()
        {
            InitializeComponent();
            this.AllowDrop = true;
            this.DragOver += DropZoneWindow_DragOver;
            this.Drop += DropZoneWindow_Drop;
        }

        private void DropZoneWindow_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintReorderItem"))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void DropZoneWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabPaintReorderItem"))
            {
                TabDropped?.Invoke(this, e);
                e.Handled = true;
            }
        }

        public void ShowAtBottom()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double windowHeight = screenHeight / 10;
            
            this.Width = screenWidth;
            this.Height = windowHeight;
            this.Left = 0;
            this.Top = screenHeight - windowHeight;
            
            this.Show();
        }
    }
}
