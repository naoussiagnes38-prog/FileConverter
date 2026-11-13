using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using System.IO.Compression;
using FileConverter.Services;
using FileConverter.Core.Services;


namespace FileConverter.Views
{
    /// <summary>
    /// Logique d'interaction pour CompressView.xaml
    /// </summary>
    public partial class CompressView : UserControl
    {
        // ==========================================
        // FILE MODEL
        // ==========================================

        public class CompressionFile
        {
            public string FileName { get; set; }

            public string FilePath { get; set; }
        }

        // ==========================================
        // FILE COLLECTION
        // ==========================================

        private ObservableCollection<CompressionFile> selectedFiles;

        private HashSet<string> selectedFilePaths;

        // ==========================================
        // COMPRESSION SETTINGS
        // ==========================================

        private string outputFolder = string.Empty;


        // ==========================================
        // CONSTRUCTOR
        // ==========================================

        public CompressView()
        {
            InitializeComponent();

            selectedFiles = new ObservableCollection<CompressionFile>();

            selectedFilePaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            FileList.ItemsSource = selectedFiles;

            // Compression settings events

            SameFolderRadio.Checked += OutputLocation_Checked;

            CustomFolderRadio.Checked += OutputLocation_Checked;



            UpdateOutputLocationInterface();

            UpdateInterface();
        }

        // ==========================================
        // CHOOSE FILES
        // ==========================================

        private void ChooseFiles_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            dialog.Title = "Select Files to Compress";

            dialog.Multiselect = true;

            dialog.CheckFileExists = true;

            dialog.CheckPathExists = true;

            dialog.Filter = "All Files (*.*)|*.*";

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                AddFiles(dialog.FileNames);
            }
        }

        // ==========================================
        // ADD FILES
        // ==========================================

        private void AddFiles(string[] filePaths)
        {
            if (filePaths == null)
            {
                return;
            }

            int duplicateCount = 0;

            foreach (string filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                if (!File.Exists(filePath))
                {
                    continue;
                }

                string fullPath = System.IO.Path.GetFullPath(filePath);

                if (selectedFilePaths.Contains(fullPath))
                {
                    duplicateCount++;

                    continue;
                }

                CompressionFile file = new CompressionFile();

                file.FileName = System.IO.Path.GetFileName(fullPath);

                file.FilePath = fullPath;

                selectedFiles.Add(file);

                selectedFilePaths.Add(fullPath);
            }

            UpdateInterface();

            if (duplicateCount > 0)
            {
                System.Windows.MessageBox.Show(
                    duplicateCount +
                    " file(s) were already in the selection.",
                    "Duplicate Files",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // ==========================================
        // DRAG OVER
        // ==========================================

        private void DropZone_DragOver(
            object sender,
            System.Windows.DragEventArgs e)
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

        // ==========================================
        // DROP FILES
        // ==========================================

        private void DropZone_Drop(
            object sender,
            System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string[] droppedFiles =
                e.Data.GetData(DataFormats.FileDrop) as string[];

            AddFiles(droppedFiles);
        }

        // ==========================================
        // REMOVE INDIVIDUAL FILE
        // ==========================================

        private void RemoveFile_Click(
            object sender,
            RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
            {
                return;
            }

            string filePath = button.Tag as string;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            CompressionFile file = selectedFiles.FirstOrDefault(
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

        // ==========================================
        // CLEAR ALL FILES
        // ==========================================

        private void ClearAll_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (selectedFiles.Count == 0)
            {
                return;
            }

            MessageBoxResult result = System.Windows.MessageBox.Show(
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

        // ==========================================
        // UPDATE FILE INTERFACE
        // ==========================================

        private void UpdateInterface()
        {
            int count = selectedFiles.Count;

            FileCountText.Text = count.ToString();

            if (count == 0)
            {
                EmptyListText.Visibility = Visibility.Visible;

                ClearAllButton.IsEnabled = false;

                CompressionStatusText.Text = LocalizationManager.Text("Not configured");
            }
            else
            {
                EmptyListText.Visibility = Visibility.Collapsed;

                ClearAllButton.IsEnabled = true;

                CompressionStatusText.Text = LocalizationManager.Text("Files selected");
            }
        }

        // ==========================================
        // COMPRESSION LEVEL
        // ==========================================


        // ==========================================
        // OUTPUT LOCATION
        // ==========================================

        private void OutputLocation_Checked(
            object sender,
            RoutedEventArgs e)
        {
            UpdateOutputLocationInterface();
        }

        // ==========================================
        // UPDATE OUTPUT LOCATION INTERFACE
        // ==========================================

        private void UpdateOutputLocationInterface()
        {
            if (SameFolderRadio.IsChecked == true)
            {
                BrowseFolderButton.IsEnabled = false;

                OutputFolderTextBox.Text =
                    LocalizationManager.Text("Same folder as original files");


            }
            else if (CustomFolderRadio.IsChecked == true)
            {
                BrowseFolderButton.IsEnabled = true;

                if (string.IsNullOrWhiteSpace(outputFolder))
                {
                    OutputFolderTextBox.Text =
                        LocalizationManager.Text("No destination selected");
                }
                else
                {
                    OutputFolderTextBox.Text = outputFolder;
                }
            }
        }

        // ==========================================
        // BROWSE OUTPUT FOLDER
        // ==========================================

        private void BrowseFolder_Click(
    object sender,
    RoutedEventArgs e)
        {
            using (System.Windows.Forms.FolderBrowserDialog dialog =
                new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description =
                    "Select the destination folder for compressed files.";

                dialog.ShowNewFolderButton = true;

                System.Windows.Forms.DialogResult result =
                    dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK &&
                    !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    outputFolder = dialog.SelectedPath;

                    OutputFolderTextBox.Text = outputFolder;
                }
            }
        }

        // ==========================================
        // KEEP ORIGINAL FILES
        // ==========================================

        private async void CompressFiles_Click(object sender, RoutedEventArgs e)
        {
            bool custom = CustomFolderRadio.IsChecked == true;
            if (selectedFiles.Count == 0 || (custom && !Directory.Exists(outputFolder)))
            {
                MessageBox.Show("Select files and a valid destination folder.", "FileConverter");
                return;
            }

            var level = CompressionLevelCombo.SelectedIndex == 0
                ? CompressionLevel.Fastest : CompressionLevel.Optimal;
            var service = new CompressionService();
            await BatchOperations.RunAsync(this, selectedFiles.Select(file => file.FilePath),
                file => Path.Combine(custom ? outputFolder : Path.GetDirectoryName(file),
                    Path.GetFileName(file) + ".zip"),
                (file, output, progress) => service.CompressFile(file, output, level, progress),
                percent => OperationProgress.Value = percent);
        }
    }
}
