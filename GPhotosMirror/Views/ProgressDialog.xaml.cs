using System.Windows;

namespace GPhotosMirror.Views
{
    /// <summary>
    /// Interaction logic for ProgressDialog.xaml
    /// </summary>
    public partial class ProgressDialog : Window
    {
        public ProgressDialog()
        {
            InitializeComponent();
        }

        public double Progress
        {
            get => progressBar.Value;
            set => progressBar.Value = value;
        }
    }
}
