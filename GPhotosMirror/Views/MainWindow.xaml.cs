using GPhotosMirror.Output;
using GPhotosMirror.Output.UI;
using MahApps.Metro.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace GPhotosMirror.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
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
