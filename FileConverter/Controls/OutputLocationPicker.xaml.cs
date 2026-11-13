using System.IO;
using System.Windows;
using System.Windows.Controls;
using FileConverter.Services;
using Forms = System.Windows.Forms;

namespace FileConverter.Controls
{
    public partial class OutputLocationPicker : UserControl
    {
        public OutputLocationPicker()
        {
            InitializeComponent();
        }

        public bool TryGetFolder(out string folder)
        {
            folder = CustomFolder.IsChecked == true ? FolderPath.Text : null;
            if (folder == null || Directory.Exists(folder))
                return true;

            MessageBox.Show(LocalizationManager.Text("Choose an existing output folder."),
                LocalizationManager.Text("Output Location"), MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = LocalizationManager.Text("Choose an output folder.");
                if (Directory.Exists(FolderPath.Text))
                    dialog.SelectedPath = FolderPath.Text;
                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                    FolderPath.Text = dialog.SelectedPath;
            }
        }
    }
}
