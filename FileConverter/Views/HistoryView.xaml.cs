
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FileConverter.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FileConverter.Views
{
    public partial class HistoryView : UserControl
    {
        private static readonly string HistoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileConverter", "history.xml");
        private static readonly ObservableCollection<HistoryItem> History = LoadHistory();

        private ICollectionView historyView;

        private string selectedStatus = "All";

        public HistoryView()
        {
            InitializeComponent();

            historyView = new ListCollectionView(History);

            historyView.Filter = FilterHistory;

            HistoryDataGrid.ItemsSource = historyView;
            HistoryDataGrid.SelectedIndex = History.Count > 0 ? 0 : -1;

            UpdateCounts();
            Loaded += (sender, args) =>
            {
                History.CollectionChanged += History_CollectionChanged;
                UpdateCounts();
            };
            Unloaded += (sender, args) => History.CollectionChanged -= History_CollectionChanged;

            foreach (var column in HistoryDataGrid.Columns)
                column.Header = LocalizationManager.Text(column.Header.ToString());

            UpdateFilterButtons();
        }

        private void History_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            TotalCount.Text = History.Count.ToString();
            SuccessCount.Text = History.Count(item => item.Status == "Completed").ToString();
            FailureCount.Text = History.Count(item => item.Status == "Failed").ToString();
        }

        internal static void Record(string input, string output, bool success)
        {
            History.Insert(0, new HistoryItem
            {
                FileName = Path.GetFileName(input),
                InputPath = input,
                OutputPath = success ? output : null,
                InputFormat = Path.GetExtension(input).TrimStart('.').ToUpperInvariant(),
                OutputFormat = success ? (Path.GetExtension(output) ?? "").TrimStart('.').ToUpperInvariant() : "",
                Date = DateTime.Now.ToString("g"),
                Status = success ? "Completed" : "Failed"
            });

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath));
                var document = new XDocument(new XElement("History", History.Select(item =>
                    new XElement("Entry", new XElement("File", item.FileName),
                        new XElement("Input", item.InputFormat), new XElement("Output", item.OutputFormat),
                        new XElement("InputPath", item.InputPath), new XElement("OutputPath", item.OutputPath),
                        new XElement("Date", item.Date), new XElement("Status", item.Status)))));
                string temp = HistoryPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    document.Save(temp);
                    if (File.Exists(HistoryPath))
                        File.Replace(temp, HistoryPath, null);
                    else
                        File.Move(temp, HistoryPath);
                }
                finally
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
            }
            catch (IOException ex) { System.Diagnostics.Trace.TraceWarning(ex.Message); }
            catch (UnauthorizedAccessException ex) { System.Diagnostics.Trace.TraceWarning(ex.Message); }
        }

        private static ObservableCollection<HistoryItem> LoadHistory()
        {
            try
            {
                return new ObservableCollection<HistoryItem>(
                    XDocument.Load(HistoryPath).Root.Elements("Entry").Select(item => new HistoryItem
                    {
                        FileName = (string)item.Element("File") ?? "",
                        InputPath = (string)item.Element("InputPath"),
                        OutputPath = (string)item.Element("OutputPath"),
                        InputFormat = (string)item.Element("Input") ?? "",
                        OutputFormat = (string)item.Element("Output") ?? "",
                        Date = (string)item.Element("Date") ?? "",
                        Status = (string)item.Element("Status") ?? ""
                    }));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Xml.XmlException) { }
            return new ObservableCollection<HistoryItem>();
        }

        // SEARCH AND STATUS FILTER
        private bool FilterHistory(object item)
        {
            HistoryItem historyItem = item as HistoryItem;

            if (historyItem == null)
            {
                return false;
            }

            // STATUS FILTER
            if (selectedStatus != "All" &&
                historyItem.Status != selectedStatus)
            {
                return false;
            }

            // SEARCH FILTER
            string searchText = SearchTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            return
                historyItem.FileName.IndexOf(
                    searchText,
                    StringComparison.OrdinalIgnoreCase) >= 0

                ||

                historyItem.DisplayInput.IndexOf(
                    searchText,
                    StringComparison.OrdinalIgnoreCase) >= 0

                ||

                historyItem.DisplayOutput.IndexOf(
                    searchText,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // SEARCH TEXT CHANGED
        private void SearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (historyView != null)
            {
                historyView.Refresh();
            }
        }

        // FILTER BUTTON CLICKED
        private void FilterButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;

            if (clickedButton == null ||
                clickedButton.Tag == null)
            {
                return;
            }

            selectedStatus = clickedButton.Tag.ToString();

            UpdateFilterButtons();

            if (historyView != null)
            {
                historyView.Refresh();
            }
        }

        // UPDATE FILTER BUTTON APPEARANCE
        private void UpdateFilterButtons()
        {
            Button[] buttons =
            {
                AllFilterButton,
                CompletedFilterButton,
                FailedFilterButton
            };

            foreach (Button button in buttons)
            {
                button.SetResourceReference(
                    Control.BackgroundProperty,
                    "InputBackgroundBrush");

                button.SetResourceReference(
                    Control.ForegroundProperty,
                    "PrimaryTextBrush");
            }

            Button selectedButton = AllFilterButton;

            if (selectedStatus == "Completed")
            {
                selectedButton = CompletedFilterButton;
            }
            else if (selectedStatus == "Failed")
            {
                selectedButton = FailedFilterButton;
            }


            selectedButton.SetResourceReference(
                Control.BackgroundProperty,
                "SelectedItemBrush");

            selectedButton.SetResourceReference(
                Control.ForegroundProperty,
                "AccentBrush");
        }
    }

    // HISTORY RECORD MODEL
    public class HistoryItem
    {
        public string FileName { get; set; }

        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public string DisplayInput { get { return string.IsNullOrEmpty(InputPath) ? InputFormat : InputPath; } }
        public string DisplayOutput { get { return Status == "Failed" ? LocalizationManager.Text("No output file") :
            string.IsNullOrEmpty(OutputPath) ? OutputFormat : OutputPath; } }

        public string InputFormat { get; set; }

        public string OutputFormat { get; set; }

        public string Date { get; set; }

        public string Status { get; set; }
        public string DisplayStatus { get { return LocalizationManager.Text(Status); } }
    }
}
