using System;
using System.Threading.Tasks;
using System.Windows;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.DependencyInjection;

namespace GPhotosMirror
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private async void App_OnExit(object sender, ExitEventArgs e)
        {
            var mainViewModel = Container.GetService<MainViewModel>();
            await mainViewModel.Browser.Close();
        }

        public App()
        {
            Container = RegisterServices();
        }
        public IServiceProvider Container { get; private set; }

        private IServiceProvider RegisterServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<MainViewModel>();
            return serviceCollection.BuildServiceProvider();
        }

    }
}
