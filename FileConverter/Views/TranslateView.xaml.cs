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
using System.Net.Http;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
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
                item.FileInformation = fileInfo.Extension.ToUpperInvariant();
                try { item.FileInformation += " · " + GetFileSize(fileInfo.Length); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                item.DetectedLanguage = LocalizationManager.Text("Not detected");
                item.DetectedSourceLanguage = null;
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
            DocumentProgress.Value = 0;
            ProgressPanel.Visibility = Visibility.Visible;
            try
            {
                for (int index = 0; index < added.Count; index++)
                {
                    var item = added[index];
                    ShowDocumentProgress(item, index + 1, added.Count, "Detecting language...");
                    try
                    {
                        await InspectDocumentAsync(item);
                        item.Status = LocalizationManager.Text(item.EffectiveTargetLanguage == null
                            ? "Choose English or French as the target language." : "Queued");
                    }
                    catch (Exception ex)
                    {
                        item.Status = ex.Message;
                    }
                    DocumentProgress.Value = Math.Floor(100.0 * (index + 1) / added.Count);
                }
            }
            finally
            {
                ProgressPanel.Visibility = Visibility.Collapsed;
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
                ProgressPanel.Visibility = Visibility.Collapsed;
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

            string outputFolder;
            if (!OutputLocation.TryGetFolder(out outputFolder))
                return;

            string format = (string)TranslationOutputFormat.SelectedValue ?? "txt";
            TranslationNotice.Visibility = Visibility.Collapsed;
            IsEnabled = false;
            DocumentProgress.Value = 0;
            ProgressPanel.Visibility = Visibility.Visible;
            try
            {
                var endpoint = new Uri(ConfigurationManager.AppSettings["TranslationEndpoint"]);
                bool engineReady = false;
                int completed = 0;
                int failed = 0;
                int skipped = 0;
                using (var service = new LanguageDetectionService(endpoint))
                {
                    for (int index = 0; index < selectedFiles.Count; index++)
                    {
                        var item = selectedFiles[index];
                        ShowDocumentProgress(item, index + 1, selectedFiles.Count, "Detecting language...");
                        string output = null;
                        bool success = false;
                        bool checkingEngine = false;
                        try
                        {
                            string text = await InspectDocumentAsync(item);
                            string target = item.EffectiveTargetLanguage;
                            if (target == null)
                            {
                                item.Status = LocalizationManager.Text("Choose English or French as the target language.");
                                skipped++;
                                continue;
                            }

                            if (!engineReady)
                            {
                                checkingEngine = true;
                                ProgressLabel.Text = LocalizationManager.Text("Checking the local translation engine...");
                                try
                                {
                                    await LocalTranslationServer.EnsureReadyAsync(service, endpoint, () =>
                                        ProgressLabel.Text = LocalizationManager.Text("Starting LibreTranslate and loading language models..."));
                                }
                                catch (Exception ex)
                                {
                                    item.Status = string.Format(LocalizationManager.Text("Failed to translate: {0}"), ex.Message);
                                    throw;
                                }
                                engineReady = true;
                                checkingEngine = false;
                            }

                            string source = target == "fr" ? "en" : "fr";
                            item.Status = LocalizationManager.Text("Translating...");
                            output = Path.Combine(outputFolder ?? Path.GetDirectoryName(item.FullPath),
                                Path.GetFileName(item.FullPath) + "." + target + "." + format);

                            ShowDocumentProgress(item, index + 1, selectedFiles.Count, "Translating...");
                            string translated = await service.TranslateAsync(text, source, target, fraction =>
                            {
                                DocumentProgress.Value = Math.Floor(
                                    100 * (index + Math.Min(fraction, 0.99)) / selectedFiles.Count);
                            });
                            ShowDocumentProgress(item, index + 1, selectedFiles.Count, "Saving translation...");
                            output = await Task.Run(() => BatchOperations.WriteOutput(output,
                                temp => TranslatedDocumentWriter.Write(temp, translated, format)));
                            item.Status = LocalizationManager.Text("Saved: ") + Path.GetFileName(output);
                            success = true;
                        }
                        catch (HttpRequestException ex) when (!checkingEngine)
                        {
                            item.Status = string.Format(LocalizationManager.Text(
                                "Lost the connection to LibreTranslate at {0}. {1}"), endpoint,
                                ex.GetBaseException().Message);
                        }
                        catch (TaskCanceledException) when (!checkingEngine)
                        {
                            item.Status = LocalizationManager.Text("LibreTranslate did not respond in time.");
                        }
                        catch (Exception ex) when (!checkingEngine)
                        {
                            item.Status = string.Format(LocalizationManager.Text("Failed to translate: {0}"), ex.Message);
                        }
                        finally
                        {
                            if (!checkingEngine)
                                DocumentProgress.Value = Math.Floor(100.0 * (index + 1) / selectedFiles.Count);
                        }

                        if (success)
                            completed++;
                        else
                            failed++;
                        HistoryView.Record(item.FullPath, output, success);
                    }
                    ProgressLabel.Text = string.Format(LocalizationManager.Text("{0} completed, {1} failed, {2} skipped."),
                        completed, failed, skipped);
                }
            }
            catch (Exception ex)
            {
                ProgressLabel.Text = LocalizationManager.Text("Translation stopped.");
                TranslationNotice.Text = ex.Message;
                TranslationNotice.Visibility = Visibility.Visible;
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private static async Task<string> InspectDocumentAsync(TranslationFileItem item)
        {
            item.Status = LocalizationManager.Text("Detecting language...");
            item.DetectedLanguage = LocalizationManager.Text("Not detected");
            item.DetectedSourceLanguage = null;

            string text = Path.GetExtension(item.FullPath).Equals(".rtf", StringComparison.OrdinalIgnoreCase)
                ? ReadRtf(item.FullPath)
                : await Task.Run(() => ReadDocument(item.FullPath));
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException(Path.GetExtension(item.FullPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                    ? LocalizationManager.Text("This PDF has no selectable text. Run OCR on scanned pages and try again.")
                    : "The document has no readable text.");

            string source = await Task.Run(() => DocumentLanguageDetector.Detect(text));
            item.DetectedSourceLanguage = source;
            item.DetectedLanguage = source == "en" ? LocalizationManager.Text("English") :
                source == "fr" ? LocalizationManager.Text("French") : source ?? LocalizationManager.Text("Unknown");
            return text;
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

                        using (var input = entry.Open())
                        {
                            var xml = XDocument.Load(input);
                            XNamespace word = xml.Root.Name.Namespace;
                            var body = xml.Root.Element(word + "body");
                            if (body == null)
                                throw new InvalidDataException("Missing DOCX document body.");

                            return string.Join(Environment.NewLine, body.Descendants(word + "p").Select(paragraph =>
                                string.Concat(paragraph.Descendants()
                                    .Where(node => node.Ancestors(word + "p").FirstOrDefault() == paragraph)
                                    .Select(node => node.Name == word + "t" ? node.Value :
                                        node.Name == word + "tab" ? "\t" :
                                        node.Name == word + "br" || node.Name == word + "cr" ? Environment.NewLine : ""))));
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
            var text = new StringBuilder();
            using (var document = PdfDocument.Open(path))
            {
                foreach (var page in document.GetPages())
                    text.AppendLine(ContentOrderTextExtractor.GetText(page));
            }

            return text.ToString();
        }

        private void ShowDocumentProgress(TranslationFileItem item, int current, int total, string stage)
        {
            ProgressLabel.Text = string.Format("{0}/{1} · {2} · {3}", current, total,
                LocalizationManager.Text(stage), item.FileName);

        }

    }

    // Information displayed for each translation document
    public class TranslationFileItem : INotifyPropertyChanged
    {
        public string FullPath { get; set; }

        public string FileName { get; set; }

        public string FileInformation { get; set; }

        private string targetLanguage = "";
        private string detectedSourceLanguage;

        public string TargetLanguage
        {
            get { return targetLanguage; }
            set
            {
                if (targetLanguage == value)
                    return;
                SetValue(ref targetLanguage, value);
                NotifyDirectionChanged();
            }
        }

        internal string DetectedSourceLanguage
        {
            set
            {
                detectedSourceLanguage = value;
                NotifyDirectionChanged();
            }
        }

        internal string EffectiveTargetLanguage
        {
            get
            {
                if (targetLanguage == "en" || targetLanguage == "fr")
                    return targetLanguage;
                return detectedSourceLanguage == "en" ? "fr" : detectedSourceLanguage == "fr" ? "en" : null;
            }
        }

        private void NotifyDirectionChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TranslationDirection)));
        }

        private string detectedLanguage;
        private string status;

        public string DetectedLanguage
        {
            get { return detectedLanguage; }
            set { SetValue(ref detectedLanguage, value); }
        }

        public string TranslationDirection
        {
            get
            {
                string target = EffectiveTargetLanguage;
                return LocalizationManager.Text(target == "fr" ? "English → French" :
                    target == "en" ? "French → English" : "Pending");
            }
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
