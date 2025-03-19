using LibraEmulation.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LibraEmulation.View
{
    /// <summary>
    /// Логика взаимодействия для LibraControl.xaml
    /// </summary>
    public partial class LibraControl : UserControl
    {
        public LibraControl()
        {
            InitializeComponent();
        }

        public LibraModel ScaleModel
        {
            get { return (LibraModel)GetValue(ScaleModelProperty); }
            set { SetValue(ScaleModelProperty, value); }
        }

        public static readonly DependencyProperty ScaleModelProperty =
            DependencyProperty.Register("ScaleModel", typeof(LibraModel), typeof(LibraControl),
                new PropertyMetadata(null, OnScaleModelChanged));

        private static void OnScaleModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            LibraControl control = d as LibraControl;
            if (control != null)
            {
                // Устанавливаем DataContext для всех биндингов внутри UserControl
                control.DataContext = e.NewValue;
            }
        }
    }
}
