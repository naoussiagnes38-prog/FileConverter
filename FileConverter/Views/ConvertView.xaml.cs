using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FileConverter.Services;

namespace FileConverter.Views
{
    public partial class ConvertView : UserControl
    {
        private readonly ObservableCollection<ConversionRow> selectedFiles = new ObservableCollection<ConversionRow>();
        private ConversionMethod? method;
        private bool running;
        private int processedFiles;

        public ConvertView()
        {
            InitializeComponent();
            FileList.ItemsSource = selectedFiles;
            UpdateFileCount();
        }

        private void Method_Checked(object sender, RoutedEventArgs e)
        {
            method = (ConversionMethod)Enum.Parse(typeof(ConversionMethod), (string)((RadioButton)sender).Tag);
            QualityOptions.Visibility = method == ConversionMethod.ImageToPdf || method == ConversionMethod.PdfToImages ||
                method == ConversionMethod.Jpg || method == ConversionMethod.MergePdf ? Visibility.Visible : Visibility.Collapsed;
            UpdateFileCount();
        }

        private async void ConvertFiles_Click(object sender, RoutedEventArgs e)
        {
            if (running || !method.HasValue || selectedFiles.Count == 0)
                return;

            var files = selectedFiles.ToArray();
            var conversion = method.Value;
            if (conversion == ConversionMethod.MergePdf && files.Length < 2)
            {
                BatchStatus.Text = LocalizationManager.Text("Add at least two files to merge.");
                return;
            }

            string outputFolder;
            if (!OutputLocation.TryGetFolder(out outputFolder))
                return;

            int quality = (int)ImageQuality.Value;
            string renderer = ConfigurationManager.AppSettings["PdfToPpmPath"];
            running = true;
            IsEnabled = false;
            processedFiles = 0;
            OverallProgress.Value = 0;
            BatchStatus.Text = LocalizationManager.Text("Converting...");
            foreach (var file in files)
                file.Update(LocalizationManager.Text("Queued"), false, 0);

            try
            {
                if (conversion == ConversionMethod.MergePdf)
                    await MergeAsync(files, quality, outputFolder);
                else
                {
                    using (var workers = new SemaphoreSlim(2))
                        await Task.WhenAll(files.Select(file => ConvertAsync(file, conversion, quality, renderer, outputFolder, workers)));
                }

                BatchStatus.Text = string.Format(LocalizationManager.Text("{0} completed, {1} failed."),
                    files.Count(file => file.HasOutput), files.Count(file => !file.HasOutput));
            }
            catch (Exception ex)
            {
                BatchStatus.Text = ex.Message;
            }
            finally
            {
                running = false;
                IsEnabled = true;
                UpdateFileCount();
            }
        }

        private async Task ConvertAsync(ConversionRow file, ConversionMethod conversion, int quality,
            string renderer, string outputFolder, SemaphoreSlim workers)
        {
            await workers.WaitAsync();
            try
            {
                file.Update(LocalizationManager.Text("Converting..."), true, 0);
                ValidateInput(file.FilePath, conversion);
                bool images = conversion == ConversionMethod.PdfToImages;
                string suffix = images ? ".pages" : conversion == ConversionMethod.PdfToWord ? ".docx" :
                    conversion == ConversionMethod.Jpg ? ".converted.jpg" :
                    conversion == ConversionMethod.Png ? ".converted.png" :
                    conversion == ConversionMethod.Bmp ? ".converted.bmp" : ".pdf";
                string requested = Path.Combine(outputFolder ?? Path.GetDirectoryName(file.FilePath), file.FileName + suffix);
                string output = await RunConversionAsync(conversion, () => BatchOperations.WriteOutput(requested,
                    temp => DesktopConversionService.Convert(file.FilePath, temp, conversion, quality, renderer), images));
                Finish(file, output, null);
            }
            catch (Exception ex)
            {
                Finish(file, null, ex.Message);
            }
            finally
            {
                workers.Release();
            }
        }

        private async Task MergeAsync(ConversionRow[] files, int quality, string outputFolder)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "FileConverter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            try
            {
                string[] prepared;
                using (var workers = new SemaphoreSlim(2))
                    prepared = await Task.WhenAll(files.Select((file, index) =>
                        PrepareMergeAsync(file, Path.Combine(tempDirectory, index + ".pdf"), quality, workers)));

                var ready = prepared.Where(path => path != null).ToArray();
                string output = null;
                string error = LocalizationManager.Text("At least two readable files are needed to merge.");
                if (ready.Length >= 2)
                {
                    try
                    {
                        string requested = Path.Combine(outputFolder ?? Path.GetDirectoryName(files[0].FilePath), "merged.pdf");
                        output = await Task.Run(() => BatchOperations.WriteOutput(requested,
                            temp => DesktopConversionService.MergePdf(ready, temp)));
                    }
                    catch (Exception ex) { error = ex.Message; }
                }

                for (int index = 0; index < files.Length; index++)
                    if (prepared[index] != null)
                        Finish(files[index], output, error);
            }
            finally
            {
                try { Directory.Delete(tempDirectory, true); }
                catch (IOException ex) { Trace.TraceWarning(ex.Message); }
                catch (UnauthorizedAccessException ex) { Trace.TraceWarning(ex.Message); }
            }
        }

        private async Task<string> PrepareMergeAsync(ConversionRow file, string temp, int quality, SemaphoreSlim workers)
        {
            await workers.WaitAsync();
            try
            {
                file.Update(LocalizationManager.Text("Converting..."), true, 0);
                if (Path.GetExtension(file.FilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    await Task.Run(() => DesktopConversionService.MergePdf(new[] { file.FilePath }, temp));
                else
                {
                    var conversion = DesktopConversionService.PdfInputMethod(file.FilePath);
                    if (!conversion.HasValue)
                        throw new InvalidDataException(LocalizationManager.Text("This file type cannot be merged."));
                    await RunConversionAsync(conversion.Value, () =>
                    {
                        DesktopConversionService.Convert(file.FilePath, temp, conversion.Value, quality, null);
                        return temp;
                    });
                }
                file.Update(LocalizationManager.Text("Waiting for merge..."), true, 0);
                return temp;
            }
            catch (Exception ex)
            {
                Finish(file, null, ex.Message);
                return null;
            }
            finally { workers.Release(); }
        }

        private static Task<string> RunConversionAsync(ConversionMethod conversion, Func<string> work)
        {
            if (conversion != ConversionMethod.WordToPdf && conversion != ConversionMethod.ExcelToPdf &&
                conversion != ConversionMethod.PdfToWord)
                return Task.Run(work);

            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { completion.SetResult(work()); }
                catch (Exception ex) { completion.SetException(ex); }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private static void ValidateInput(string path, ConversionMethod conversion)
        {
            ConversionMethod? input = DesktopConversionService.PdfInputMethod(path);
            bool valid = conversion == ConversionMethod.PdfToWord || conversion == ConversionMethod.PdfToImages
                ? Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                : conversion == ConversionMethod.Jpg || conversion == ConversionMethod.Png || conversion == ConversionMethod.Bmp
                    ? input == ConversionMethod.ImageToPdf : input == conversion;
            if (!valid)
                throw new InvalidDataException(LocalizationManager.Text("This file type does not match the selected conversion."));
        }

        private void Finish(ConversionRow file, string output, string error)
        {
            file.Update(output == null ? LocalizationManager.Text("Failed") + " · " + error :
                LocalizationManager.Text("Done") + " · " + Path.GetFileName(output), false, 100, output);
            processedFiles++;
            OverallProgress.Value = Math.Floor(100.0 * processedFiles / selectedFiles.Count);
            BatchStatus.Text = string.Format(LocalizationManager.Text("{0} of {1} files processed"),
                processedFiles, selectedFiles.Count);
            HistoryView.Record(file.FilePath, output, output != null);
        }

        private void ChooseFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Supported files|*.doc;*.docx;*.xls;*.xlsx;*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|All files|*.*"
            };
            if (dialog.ShowDialog() == true)
                AddFiles(dialog.FileNames);
        }

        private void AddFiles(string[] paths)
        {
            if (paths == null || running)
                return;
            foreach (string path in paths.Where(File.Exists))
            {
                string fullPath = Path.GetFullPath(path);
                if (!selectedFiles.Any(file => string.Equals(file.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)))
                    selectedFiles.Add(new ConversionRow(fullPath));
            }
            UpdateFileCount();
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            bool canDrop = !running && e.Data.GetDataPresent(DataFormats.FileDrop);
            e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
            if (canDrop)
                DropZone.SetResourceReference(Border.BackgroundProperty, "SelectedItemBrush");
            e.Handled = true;
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            DropZone.SetResourceReference(Border.BackgroundProperty, "UploadSurfaceBrush");
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZone_DragLeave(sender, e);
            AddFiles(e.Data.GetData(DataFormats.FileDrop) as string[]);
            e.Handled = true;
        }

        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            selectedFiles.Remove((ConversionRow)((Button)sender).Tag);
            UpdateFileCount();
        }

        private void ClearFiles_Click(object sender, RoutedEventArgs e)
        {
            selectedFiles.Clear();
            OverallProgress.Value = 0;
            BatchStatus.Text = "";
            UpdateFileCount();
        }

        private void OpenOutput_Click(object sender, RoutedEventArgs e)
        {
            string path = (string)((Button)sender).Tag;
            try
            {
                if (Directory.Exists(path))
                    Process.Start("explorer.exe", "\"" + path + "\"");
                else if (File.Exists(path))
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else
                    throw new FileNotFoundException(LocalizationManager.Text("The output could not be found."));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "FileConverter"); }
        }

        private void UpdateFileCount()
        {
            FileCountText.Text = string.Format(LocalizationManager.Text("{0} file(s) selected"), selectedFiles.Count);
            ConvertButton.IsEnabled = method.HasValue && selectedFiles.Count > 0 && !running;
            ClearFilesButton.IsEnabled = selectedFiles.Count > 0 && !running;
        }
    }

    public sealed class ConversionRow : INotifyPropertyChanged
    {
        public ConversionRow(string path)
        {
            FilePath = path;
            StatusText = LocalizationManager.Text("Queued");
        }

        public string FilePath { get; private set; }
        public string FileName { get { return Path.GetFileName(FilePath); } }
        public string StatusText { get; private set; }
        public string OutputPath { get; private set; }
        public bool HasOutput { get { return OutputPath != null; } }
        public bool IsWorking { get; private set; }
        public int Progress { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        internal void Update(string status, bool working, int progress, string output = null)
        {
            StatusText = status;
            IsWorking = working;
            Progress = progress;
            OutputPath = output;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }
}
