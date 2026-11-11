
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Linq;
using FileConverter.Services;
using FileConverter.Core.Services;

using FileConverter.Core.Models;
using FileConverter.Core.Enums;

namespace FileConverter.Views
{
    public partial class ConvertView : UserControl
    {
        private ObservableCollection<FileItem> selectedFiles;

        private HashSet<string> filePaths;

        public ConvertView()
        {
            InitializeComponent();

            selectedFiles = new ObservableCollection<FileItem>();

            filePaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            FileList.ItemsSource = selectedFiles;

            UpdateFileCount();
        }

        private async void ConvertFiles_Click(object sender, RoutedEventArgs e)
        {
            var choice = OutputFormatCombo.SelectedItem as ComboBoxItem;
            if (selectedFiles.Count == 0 || OutputFormatCombo.SelectedIndex <= 0 || choice == null)
            {
                MessageBox.Show("Select images and an output format.", "FileConverter");
                return;
            }

            string format = choice.Content.ToString();
            var service = new ConversionService();
            var byPath = selectedFiles.ToDictionary(file => file.FilePath, StringComparer.OrdinalIgnoreCase);
            foreach (FileItem item in selectedFiles)
                item.Status = ConversionStatus.Waiting;
            await BatchOperations.RunAsync(this, selectedFiles.Select(file => file.FilePath),
                file => Path.Combine(Path.GetDirectoryName(file),
                    Path.GetFileName(file) + ".converted." + format.ToLowerInvariant()),
                (file, output) => service.ConvertImage(file, output, format),
                (file, success) => byPath[file].Status = success
                    ? ConversionStatus.Completed : ConversionStatus.Failed);
        }

        // OPEN FILE PICKER
        private void ChooseFiles_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            dialog.Title = "Select files to convert";
            dialog.Multiselect = true;
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;
            dialog.Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff";

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                AddFiles(dialog.FileNames);
            }
        }

        // ADD FILES TO COLLECTION
        private void AddFiles(string[] paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    string fullPath =
                        System.IO.Path.GetFullPath(path);

                    if (filePaths.Contains(fullPath))
                    {
                        continue;
                    }

                    FileItem item = new FileItem(fullPath);

                    selectedFiles.Add(item);

                    filePaths.Add(fullPath);
                }
                catch (Exception)
                {
                    // Skip files that cannot be accessed.
                }
            }

            UpdateFileCount();
        }

        // DRAG ENTER
        private void DropZone_DragEnter(
            object sender,
            DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;

                DropZone.Background =
                    (System.Windows.Media.Brush)
                    Application.Current.Resources["SelectedItemBrush"];
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        // RESTORE DROP ZONE THEME
        private void ResetDropZoneBackground()
        {
            DropZone.SetResourceReference(
                Border.BackgroundProperty,
                "CardBackgroundBrush");
        }

        // DRAG LEAVE
        private void DropZone_DragLeave(
            object sender,
            DragEventArgs e)
        {
            ResetDropZoneBackground();
        }

        // DROP FILES
        private void DropZone_Drop(
            object sender,
            DragEventArgs e)
        {
            ResetDropZoneBackground();

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths =
                    (string[])e.Data.GetData(
                        DataFormats.FileDrop);

                AddFiles(paths);
            }

            e.Handled = true;
        }

        // REMOVE ONE FILE
        private void RemoveFile_Click(
            object sender,
            RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
            {
                return;
            }

            FileItem item = button.Tag as FileItem;

            if (item != null)
            {
                selectedFiles.Remove(item);

                filePaths.Remove(item.FilePath);

                UpdateFileCount();
            }
        }

        // CLEAR ALL FILES
        private void ClearFiles_Click(
            object sender,
            RoutedEventArgs e)
        {
            selectedFiles.Clear();

            filePaths.Clear();

            UpdateFileCount();
        }

        // UPDATE FILE COUNTER
        private void UpdateFileCount()
        {
            int count = selectedFiles.Count;

            FileCountText.Text =
                string.Format(LocalizationManager.Text("{0} image files selected"), count);
        }
    }
}