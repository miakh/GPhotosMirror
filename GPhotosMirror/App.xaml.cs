using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AsyncAwaitBestPractices;
using Enterwell.Clients.Wpf.Notifications;
using GPhotosMirror.AvalonEdit;
using GPhotosMirror.AvalonEdit.Highlighting;
using GPhotosMirror.Model;
using GPhotosMirror.Output;
using GPhotosMirror.Output.UI;
using GPhotosMirror.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
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
            await CloseBrowser();
        }

        private async Task CloseBrowser()
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
        }
        public IServiceProvider Container { get; private set; }
        public static IServiceProvider Services => ((App)Current).Container;
        public static Logger PuppeteerLogger => Services?.GetService<Logger>();

        private IServiceProvider RegisterServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<MainViewModel>();
            serviceCollection.AddSingleton<MainWindow>();
            serviceCollection.AddSingleton<BrowserInstance>();

            var applicationOutput = new Output.UI.Output("Application");
            var puppeteerOutput = new Output.UI.Output("Puppeteer");
            var outputViewModel = new OutputViewModel(new List<IOutput>() { applicationOutput, puppeteerOutput });
            serviceCollection.AddSingleton(outputViewModel);

            // configure Serilog
            var log = new LoggerConfiguration().MinimumLevel.Debug();
            log.WriteTo.OutputModule(()=>applicationOutput);
            Log.Logger = log.CreateLogger();

            // configure Puppeteer logger
            var puppeteerLog = new LoggerConfiguration().MinimumLevel.Debug();
            puppeteerLog.WriteTo.OutputModule(() => puppeteerOutput);
            Logger puppeteerLogger = puppeteerLog.CreateLogger();
            serviceCollection.AddSingleton(puppeteerLogger);
            serviceCollection.AddSingleton<NotificationMessageManager>();
            serviceCollection.AddSingleton<IOutputLogFilter, SettingsOutputLogFilter>();
            serviceCollection.AddSingleton<IHighlightingProvider, LogHighlightingProvider>();
            return serviceCollection.BuildServiceProvider();
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            Application.Current.DispatcherUnhandledException += async (o, args) =>
            {
                await CloseBrowser();
            };
            Current.MainWindow = Container.GetService<MainWindow>();
            Current.MainWindow.Show();
        }
    }
}
