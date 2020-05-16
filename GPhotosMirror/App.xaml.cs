using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AsyncAwaitBestPractices;
using GPhotosMirror.AvalonEdit;
using GPhotosMirror.AvalonEdit.Highlighting;
using GPhotosMirror.Output;
using GPhotosMirror.Output.UI;
using GPhotosMirror.Views;
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
            if (mainViewModel.Browser != null)
            {
                await mainViewModel.Browser.Close();
            }
        }

        public App()
        {
            Container = RegisterServices();

            // configure Serilog
            var log = new LoggerConfiguration()
                .MinimumLevel.Debug();
            log.WriteTo.OutputModule(
                () => Services.GetService<OutputViewModel>().OutputSource.First(),
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
        public static IServiceProvider Services => ((App)Current).Container;

        private IServiceProvider RegisterServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<MainViewModel>();
            serviceCollection.AddSingleton<MainWindow>();

            var applicationOutput = new Output.UI.Output("Application");
            var puppeteerOutput = new Output.UI.Output("Puppeteer");
            var outputViewModel = new OutputViewModel(new List<IOutput>() { applicationOutput, puppeteerOutput });

            serviceCollection.AddSingleton(outputViewModel);
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
