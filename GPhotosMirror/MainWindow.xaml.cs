using System.Windows;
using System.Windows.Controls;
using GPhotosMirror.Output;
using GPhotosMirror.Output.UI;
using Microsoft.Extensions.DependencyInjection;

namespace GPhotosMirror
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();

            this.DataContext = mainViewModel;
            var outputViewModel = (OutputViewModel)App.Services.GetService<IOutput>();
            //var outputView = (OutputView)App.Services.GetService<IOutputView>();
            //mainGrid.Children.Add(outputView);
            //Grid.SetRow(outputView, 1);
            //outputView.DataContext = outputViewModel;
            //outputViewModel.OnViewLoaded(outputView);
            OutputView.DataContext = outputViewModel;
            outputViewModel.OnViewLoaded(OutputView);

        }
    }
}
