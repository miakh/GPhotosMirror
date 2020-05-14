using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace GPhotosMirror
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = (App.Current as App).Container.GetService<MainViewModel>();
        }
    }
}
