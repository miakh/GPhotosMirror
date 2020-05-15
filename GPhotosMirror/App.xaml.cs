using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using AsyncAwaitBestPractices;
using GPhotosMirror.AvalonEdit;
using GPhotosMirror.AvalonEdit.Highlighting;
using GPhotosMirror.Output;
using GPhotosMirror.Output.UI;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

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

            var log = new LoggerConfiguration()
                .MinimumLevel.Debug();
            log.WriteTo.OutputModule(
                () => Services.GetService<IOutput>(),
                () =>
                {
                    var outputLogFilter = Services.GetService<IOutputLogFilter>();
#if DEBUG
                    outputLogFilter.MinLogLevel = LogEventLevel.Debug;
#endif
                    return outputLogFilter;
                });

            Log.Logger = log.CreateLogger();

        }
        public IServiceProvider Container { get; private set; }
        public static IServiceProvider Services
        {
            get { return (App.Current as App).Container; }
        }
        private IServiceProvider RegisterServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<MainViewModel>();
            serviceCollection.AddSingleton<MainWindow>();

            //serviceCollection.AddSingleton<IOutputView, OutputView>();
            serviceCollection.AddSingleton<IOutput, OutputViewModel>();
            serviceCollection.AddSingleton<IOutputLogFilter, SettingsOutputLogFilter>();
            serviceCollection.AddSingleton<IHighlightingProvider, LogHighlightingProvider>();
            return serviceCollection.BuildServiceProvider();
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            Current.MainWindow = Container.GetService<MainWindow>();
            Current.MainWindow.Show();
        }
    }
}
