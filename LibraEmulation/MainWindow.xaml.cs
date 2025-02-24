using System.Windows;

namespace LibraEmulation
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Устанавливаем DataContext для MVVM
            DataContext = new MainViewModel();
        }
    }
}