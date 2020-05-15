using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CSharpFunctionalExtensions;
using GPhotosMirror.AvalonEdit;
using GPhotosMirror.AvalonEdit.Highlighting;
using Microsoft.Extensions.DependencyInjection;

namespace GPhotosMirror.Output.UI
{
    /// <summary>
    /// Interaction logic for OutputView.xaml
    /// </summary>
    public partial class OutputView : UserControl, IOutputView
    {
        public OutputView()
        {
            InitializeComponent();

            outputText.SyntaxHighlighting = App.Services.GetService<IHighlightingProvider>().LoadDefinition(new Maybe<string>());

            ToggleWordWrap();

            toggleWordWrap.Click += (sender, args) => ToggleWordWrap();
        }

        private void ToggleWordWrap()
        {
            var isChecked = toggleWordWrap.IsChecked ?? false;
            outputText.HorizontalScrollBarVisibility = isChecked ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            outputText.WordWrap = isChecked;
        }

        public void ScrollToEnd()
        {
            outputText.ScrollToLine(outputText.LineCount);
        }

        public void Clear()
        {
            Dispatcher.BeginInvoke((Action) (() =>
            {
                outputText.Clear();

            }), DispatcherPriority.Normal);
        }

        public void AppendText(string text)
        {
            Dispatcher.BeginInvoke((Action) (() =>
            {
                outputText.AppendText(text);
                ScrollToEnd();

            }), DispatcherPriority.Normal);
        }

        public void SetText(string text)
        {
            Dispatcher.BeginInvoke((Action) (() =>
            {
                outputText.Document.Text = text;
                ScrollToEnd();

            }), DispatcherPriority.Normal);
        }

        private void ClearAll_OnClick(object sender, RoutedEventArgs e)
        {
            Clear();
        }

    }
}
