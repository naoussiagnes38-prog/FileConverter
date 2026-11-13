
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Threading.Tasks;
using FileConverter.Services;
using FileConverter.Core.Services;

namespace FileConverter.Views
{
    public partial class EncryptView : UserControl
    {
        public class EncryptionFile
        {
            public string FileName { get; set; }
            public string FilePath { get; set; }
        }

        private ObservableCollection<EncryptionFile> selectedFiles;
        private HashSet<string> selectedFilePaths;

        private string outputFolder = string.Empty;

        public EncryptView()
        {
            InitializeComponent();

            selectedFiles = new ObservableCollection<EncryptionFile>();

            selectedFilePaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            FileList.ItemsSource = selectedFiles;

            SameFolderRadio.Checked += OutputLocation_Checked;
            CustomFolderRadio.Checked += OutputLocation_Checked;

            UpdateOutputLocationInterface();
            UpdateInterface();
        }

        // Browse and select files
        private void ChooseFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            dialog.Title = "Select files to encrypt or decrypt";
            dialog.Filter = "All files (*.*)|*.*";
            dialog.Multiselect = true;

            if (dialog.ShowDialog() == true)
            {
                AddFiles(dialog.FileNames);
            }
        }

        // Add selected files
        private void AddFiles(string[] filePaths)
        {
            int duplicates = 0;

            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                    continue;

                string fullPath = Path.GetFullPath(filePath);

                if (selectedFilePaths.Contains(fullPath))
                {
                    duplicates++;
                    continue;
                }

                EncryptionFile file = new EncryptionFile();

                file.FileName = Path.GetFileName(fullPath);
                file.FilePath = fullPath;

                selectedFiles.Add(file);
                selectedFilePaths.Add(fullPath);
            }

            UpdateInterface();

            if (duplicates > 0)
            {
                MessageBox.Show(
                    duplicates + " file(s) were already selected.",
                    "Duplicate Files",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // Drag over upload area
        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        // Drop files
        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files =
                    (string[])e.Data.GetData(DataFormats.FileDrop);

                AddFiles(files);
            }
        }

        // Remove individual file
        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
                return;

            string filePath = button.Tag as string;

            if (string.IsNullOrEmpty(filePath))
                return;

            EncryptionFile file = selectedFiles.FirstOrDefault(
                f => string.Equals(
                    f.FilePath,
                    filePath,
                    StringComparison.OrdinalIgnoreCase));

            if (file != null)
            {
                selectedFiles.Remove(file);
                selectedFilePaths.Remove(filePath);
            }

            UpdateInterface();
        }

        // Clear all selected files
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFiles.Count == 0)
                return;

            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to remove all selected files?",
                "Clear Selected Files",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                selectedFiles.Clear();
                selectedFilePaths.Clear();

                UpdateInterface();
            }
        }

        // Update file selection interface
        private void UpdateInterface()
        {
            int count = selectedFiles.Count;

            FileCountText.Text = string.Format(LocalizationManager.Text("{0} file(s) selected"), count);

            EmptyListText.Visibility =
                count == 0 ? Visibility.Visible : Visibility.Collapsed;

            ClearAllButton.IsEnabled = count > 0;

            if (count == 0)
            {
                EncryptionStatusText.Text = LocalizationManager.Text("Ready.");
            }
            else
            {
                EncryptionStatusText.Text =
                    string.Format(LocalizationManager.Text("{0} file(s) ready."), count);
            }
        }

        // Output folder selection
        private void OutputLocation_Checked(
            object sender, RoutedEventArgs e)
        {
            UpdateOutputLocationInterface();
        }

        private void UpdateOutputLocationInterface()
        {
            bool customFolder =
                CustomFolderRadio.IsChecked == true;

            BrowseFolderButton.IsEnabled = customFolder;
            OutputFolderTextBox.IsEnabled = customFolder;

            if (customFolder)
            {
                OutputFolderTextBox.Text = outputFolder;
            }
            else
            {
                OutputFolderTextBox.Text =
                    LocalizationManager.Text("Original file locations");
            }
        }

        // Browse output folder
        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.FolderBrowserDialog dialog =
                new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select the output folder";

                if (dialog.ShowDialog() ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    outputFolder = dialog.SelectedPath;
                    OutputFolderTextBox.Text = outputFolder;
                }
            }
        }

        private async void EncryptButton_Click(object sender, RoutedEventArgs e)
        {
            await ProcessFilesAsync(false);
        }

        private async void DecryptButton_Click(object sender, RoutedEventArgs e)
        {
            await ProcessFilesAsync(true);
        }

        private async Task ProcessFilesAsync(bool decrypt)
        {
            if (selectedFiles.Count == 0)
            {
                MessageBox.Show("Select at least one file.", "FileConverter");
                return;
            }

            string password = EncryptionPasswordBox.Password;
            if (string.IsNullOrEmpty(password) ||
                (!decrypt && (password.Length < 8 || password != ConfirmPasswordBox.Password)))
            {
                MessageBox.Show(decrypt ? "Enter the original password." :
                    "Enter and confirm a password of at least eight characters.", "FileConverter");
                return;
            }

            bool custom = CustomFolderRadio.IsChecked == true;
            if (custom && !Directory.Exists(outputFolder))
            {
                MessageBox.Show("Select a valid destination folder.", "FileConverter");
                return;
            }

            const string suffix = ".encrypted";
            var service = new FileEncryptionService();
            EncryptionStatusText.Text = decrypt ? LocalizationManager.Text("Decrypting files...") : LocalizationManager.Text("Encrypting files...");
            try
            {
                await BatchOperations.RunAsync(this, selectedFiles.Select(file => file.FilePath),
                    file =>
                    {
                        string name = Path.GetFileName(file);
                        if (decrypt && (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                            name.Length <= suffix.Length))
                            throw new InvalidDataException("Select a FileConverter .encrypted file.");
                        string outputName = decrypt ? name.Substring(0, name.Length - suffix.Length) : name + suffix;
                        return Path.Combine(custom ? outputFolder : Path.GetDirectoryName(file), outputName);
                    },
                    (file, output, progress) =>
                    {
                        if (decrypt)
                            service.DecryptFile(file, output, password, progress);
                        else
                            service.EncryptFile(file, output, password, progress);
                    }, percent => OperationProgress.Value = percent);
            }
            finally
            {
                EncryptionPasswordBox.Clear();
                ConfirmPasswordBox.Clear();
                UpdateInterface();
            }
        }
    }
}
