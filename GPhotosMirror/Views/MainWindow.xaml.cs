using System;
using System.Windows;
using System.Windows.Controls;
using Enterwell.Clients.Wpf.Notifications;
using GPhotosMirror.Model;
using GPhotosMirror.Output;
using GPhotosMirror.Output.UI;
using MahApps.Metro.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace GPhotosMirror.Views
{
    public interface IWindow
    {
        public void BringWindowToFront();
        public Action OnLoaded { get; set; }
    }
    public partial class MainWindow : MetroWindow, IWindow
    {
        public MainWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();

            // setup MainViewModel
            this.DataContext = mainViewModel;
            mainViewModel.LoadWindow(this);
            this.Loaded += OneTimeLoaded;

            // setup output
            var outputViewModel = App.Services.GetService<OutputViewModel>();
            OutputView.DataContext = outputViewModel;
            outputViewModel.OnViewLoaded(OutputView);

            // setup notifications
            NotificationMessageContainer.Manager = App.Services.GetService<GPhotosNotifications>();
        }

        private void OneTimeLoaded(object sender, RoutedEventArgs e)
        {
            OnLoaded?.Invoke();
            this.Loaded -= OneTimeLoaded;
        }

        public void BringWindowToFront()
        {
            this.Activate();
        }

        public Action OnLoaded { get; set; }
    }
}
