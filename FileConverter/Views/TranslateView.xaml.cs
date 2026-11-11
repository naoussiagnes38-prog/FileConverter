using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Configuration;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Xml.Linq;
using FileConverter.Core.Services;
using FileConverter.Services;

namespace FileConverter.Views
{
    public partial class TranslateView : UserControl
    {
        private ObservableCollection<TranslationFileItem> selectedFiles;

        public TranslateView()
        {
            InitializeComponent();

            selectedFiles = new ObservableCollection<TranslationFileItem>();

            FilesList.ItemsSource = selectedFiles;

            UpdateFileCount();
        }

        // Browse for documents
        private async void BrowseFiles_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            dialog.Title = "Select documents to translate";
            dialog.Multiselect = true;
            dialog.CheckFileExists = true;

            dialog.Filter =
                "Supported documents (*.docx;*.pdf;*.txt;*.rtf)|*.docx;*.pdf;*.txt;*.rtf|" +
                "Word documents (*.docx)|*.docx|" +
                "PDF documents (*.pdf)|*.pdf|" +
                "Text documents (*.txt)|*.txt|" +
                "Rich Text documents (*.rtf)|*.rtf|" +
                "All files (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                await AddFilesAsync(dialog.FileNames);
            }
        }

        // Handle drag entering
        private void FileDrop_DragEnter(
            object sender,
            DragEventArgs e)
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

        // Handle dragging over the upload area
        private void FileDrop_DragOver(
            object sender,
            DragEventArgs e)
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

        // Handle dropped files
        private async void FileDrop_Drop(
            object sender,
            DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string[] files =
                e.Data.GetData(DataFormats.FileDrop) as string[];

            if (files != null)
            {
                await AddFilesAsync(files);
            }
        }

        // Add supported documents
        private async Task AddFilesAsync(IEnumerable<string> files)
        {
            var added = new List<TranslationFileItem>();
            int unsupportedCount = 0;

            foreach (string file in files)
            {
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                if (!File.Exists(file))
                {
                    continue;
                }

                if (!IsSupportedFile(file))
                {
                    unsupportedCount++;
                    continue;
                }

                bool alreadyAdded = selectedFiles.Any(
                    existing => string.Equals(
                        existing.FullPath,
                        file,
                        StringComparison.OrdinalIgnoreCase));

                if (alreadyAdded)
                {
                    continue;
                }

                FileInfo fileInfo = new FileInfo(file);

                TranslationFileItem item = new TranslationFileItem();

                item.FullPath = fileInfo.FullName;
                item.FileName = fileInfo.Name;
                item.FileInformation = fileInfo.Extension.ToUpperInvariant() + " · " + GetFileSize(fileInfo.Length);
                item.DetectedLanguage = LocalizationManager.Text("Not detected");
                item.TranslationDirection = LocalizationManager.Text("Pending");
                item.Status = LocalizationManager.Text("Queued");

                selectedFiles.Add(item);

                added.Add(item);
            }

            UpdateFileCount();

            if (unsupportedCount > 0)
            {
                MessageBox.Show(
                    unsupportedCount +
                    " unsupported document(s) were skipped.\n\n" +
                    "Supported formats: DOCX, PDF, TXT and RTF.",
                    "Unsupported documents",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else if (added.Count == 0)
            {
                MessageBox.Show(
                    "No new documents were added.",
                    "Translation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            if (added.Count == 0)
                return;

            IsEnabled = false;
            try
            {
                foreach (var item in added)
                {
                    try
                    {
                        await InspectDocumentAsync(item);
                        item.Status = LocalizationManager.Text("Queued");
                    }
                    catch (Exception ex)
                    {
                        item.Status = ex.Message;
                    }
                }
            }
            finally
            {
                IsEnabled = true;
            }
        }

        // Check supported file extensions
        private bool IsSupportedFile(string file)
        {
            string extension =
                Path.GetExtension(file).ToLowerInvariant();

            return extension == ".docx" ||
                   extension == ".pdf" ||
                   extension == ".txt" ||
                   extension == ".rtf";
        }

        // Format file size
        private string GetFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes + " B";
            }

            if (bytes < 1024 * 1024)
            {
                return string.Format(
                    "{0:0.##} KB",
                    bytes / 1024.0);
            }

            if (bytes < 1024L * 1024L * 1024L)
            {
                return string.Format(
                    "{0:0.##} MB",
                    bytes / (1024.0 * 1024.0));
            }

            return string.Format(
                "{0:0.##} GB",
                bytes / (1024.0 * 1024.0 * 1024.0));
        }

        // Update document count and empty state
        private void UpdateFileCount()
        {
            int count = selectedFiles.Count;

            if (count == 1)
            {
                FileCountText.Text = string.Format(LocalizationManager.Text("{0} document"), count);
            }
            else
            {
                FileCountText.Text = string.Format(LocalizationManager.Text("{0} documents"), count);
            }

            EmptyQueueText.Visibility = count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // Remove an individual document
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

            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            TranslationFileItem item = selectedFiles.FirstOrDefault(
                file => string.Equals(
                    file.FullPath,
                    filePath,
                    StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                selectedFiles.Remove(item);
            }

            UpdateFileCount();
        }

        // Clear all selected documents
        private void ClearFiles_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (selectedFiles.Count == 0)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to remove all selected documents?",
                "Clear documents",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                selectedFiles.Clear();
                UpdateFileCount();
            }
        }

        private async void TranslateFiles_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFiles.Count == 0)
            {
                MessageBox.Show("Select at least one document.", "FileConverter");
                return;
            }

            IsEnabled = false;
            try
            {
                using (var service = new LanguageDetectionService(
                    new Uri(ConfigurationManager.AppSettings["TranslationEndpoint"])))
                {
                    foreach (var item in selectedFiles.ToList())
                    {
                        string output = null;
                        bool success = false;
                        try
                        {
                            var document = await InspectDocumentAsync(item);
                            string text = document.Item1;
                            string source = document.Item2;
                            string target = source == "en" ? "fr" : "en";
                            item.Status = LocalizationManager.Text("Translating...");
                            output = Path.Combine(Path.GetDirectoryName(item.FullPath),
                                Path.GetFileName(item.FullPath) + "." + target + ".txt");

                            string translated = await service.TranslateAsync(text, source, target);
                            output = await Task.Run(() => BatchOperations.WriteOutput(output,
                                temp => File.WriteAllText(temp, translated, Encoding.UTF8)));
                            item.Status = LocalizationManager.Text("Saved: ") + Path.GetFileName(output);
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            item.Status = string.Format(LocalizationManager.Text("Failed to translate: {0}"), ex.Message);
                        }

                        HistoryView.Record(item.FullPath, output, success);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Translation");
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private static async Task<Tuple<string, string>> InspectDocumentAsync(TranslationFileItem item)
        {
            item.Status = LocalizationManager.Text("Detecting language...");
            item.DetectedLanguage = LocalizationManager.Text("Not detected");
            item.TranslationDirection = LocalizationManager.Text("Pending");

            string text = Path.GetExtension(item.FullPath).Equals(".rtf", StringComparison.OrdinalIgnoreCase)
                ? ReadRtf(item.FullPath)
                : await Task.Run(() => ReadDocument(item.FullPath));
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException("The document has no readable text.");

            string source = await Task.Run(() => DocumentLanguageDetector.Detect(text));
            if (source != "en" && source != "fr")
            {
                item.DetectedLanguage = source ?? LocalizationManager.Text("Unknown");
                throw new InvalidDataException(source == null
                    ? "There is not enough text to identify the language."
                    : "Only English and French documents can be translated.");
            }

            item.DetectedLanguage = LocalizationManager.Text(source == "en" ? "English" : "French");
            item.TranslationDirection = LocalizationManager.Text(
                source == "en" ? "English → French" : "French → English");
            return Tuple.Create(text, source);
        }

        private static string ReadRtf(string path)
        {
            var document = new FlowDocument();
            var range = new TextRange(document.ContentStart, document.ContentEnd);
            using (var input = File.OpenRead(path))
                range.Load(input, DataFormats.Rtf);
            return range.Text;
        }

        private static string ReadDocument(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".txt":
                    try
                    {
                        return File.ReadAllText(path, new UTF8Encoding(false, true));
                    }
                    catch (DecoderFallbackException)
                    {
                        return File.ReadAllText(path, Encoding.GetEncoding(1252));
                    }
                case ".docx":
                    using (var zip = ZipFile.OpenRead(path))
                    {
                        var entry = zip.GetEntry("word/document.xml");
                        if (entry == null)
                            throw new InvalidDataException("Missing DOCX document body.");

                        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        using (var input = entry.Open())
                        {
                            var xml = XDocument.Load(input);
                            return string.Join(Environment.NewLine, xml.Descendants(word + "p").Select(
                                paragraph => string.Concat(paragraph.Descendants(word + "t").Select(node => node.Value))));
                        }
                    }
                case ".pdf":
                    return ReadPdf(path);
                default:
                    throw new InvalidDataException("Unsupported document extension.");
            }
        }

        private static string ReadPdf(string path)
        {
            string executable = ConfigurationManager.AppSettings["PdfToTextPath"];
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                throw new InvalidOperationException("Set PdfToTextPath to the installed pdftotext executable for PDF support.");

            var start = new ProcessStartInfo(executable, "-enc UTF-8 -nopgbrk \"" + path + "\" -")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(start))
            {
                var text = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill();
                    throw new IOException("PDF text extraction timed out.");
                }
                if (process.ExitCode != 0)
                    throw new IOException("PDF text extraction failed.");
                return text.GetAwaiter().GetResult();
            }
        }
    }

    // Information displayed for each translation document
    public class TranslationFileItem : INotifyPropertyChanged
    {
        public string FullPath { get; set; }

        public string FileName { get; set; }

        public string FileInformation { get; set; }

        private string detectedLanguage;
        private string translationDirection;
        private string status;

        public string DetectedLanguage
        {
            get { return detectedLanguage; }
            set { SetValue(ref detectedLanguage, value); }
        }

        public string TranslationDirection
        {
            get { return translationDirection; }
            set { SetValue(ref translationDirection, value); }
        }

        public string Status
        {
            get { return status; }
            set { SetValue(ref status, value); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetValue(ref string field, string value, [CallerMemberName] string property = null)
        {
            if (field == value)
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
