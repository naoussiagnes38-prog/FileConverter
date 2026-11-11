
using System.Windows;
using System.Windows.Controls;
using FileConverter.Views;
using FileConverter.Services;

namespace FileConverter
{
    public partial class MainWindow : Window
    {
        private bool sidebarExpanded = true;
        private string currentTitle;

        public MainWindow()
        {
            InitializeComponent();

            NavigateTo(
                new ConvertView(),
                "Convert",
                ConvertButton);
        }

        // Navigate between application screens
        private void NavigateTo(
            UserControl view,
            string title,
            Button selectedButton)
        {
            PageHost.Content = view;
            currentTitle = title;
            RefreshLanguage();

            Button[] buttons =
            {
                ConvertButton,
                TranslateButton,
                HistoryButton,
                ManualButton,
                CompressButton,
                EncryptButton
            };

            foreach (Button button in buttons)
            {
                button.SetResourceReference(
                    Control.BackgroundProperty,
                    "SidebarBackgroundBrush");

                button.SetResourceReference(
                    Control.ForegroundProperty,
                    "PrimaryTextBrush");
            }

            if (selectedButton != null)
            {
                selectedButton.SetResourceReference(
                    Control.BackgroundProperty,
                    "SelectedItemBrush");

                selectedButton.SetResourceReference(
                    Control.ForegroundProperty,
                    "AccentBrush");
            }
        }

        // Expand or collapse sidebar
        private void ToggleSidebar_Click(
            object sender,
            RoutedEventArgs e)
        {
            sidebarExpanded = !sidebarExpanded;

            SidebarColumn.Width = sidebarExpanded
                ? new GridLength(245)
                : new GridLength(75);

            UpdateSidebarContent();
        }

        internal void RefreshLanguage()
        {
            if (currentTitle == null)
                return;
            PageTitle.Text = LocalizationManager.Text(currentTitle);
            UpdateSidebarContent();
        }

        // Update sidebar according to its state
        private void UpdateSidebarContent()
        {
            if (sidebarExpanded)
            {
                ConvertButton.Content = "⇄    " + LocalizationManager.Text("Convert");
                TranslateButton.Content = "文    " + LocalizationManager.Text("Translate");
                HistoryButton.Content = "◷    " + LocalizationManager.Text("History");
                ManualButton.Content = "?    " + LocalizationManager.Text("Manual");
                CompressButton.Content = "▣    " + LocalizationManager.Text("Compress");
                EncryptButton.Content = "◆    " + LocalizationManager.Text("Encrypt");

                ConvertButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                TranslateButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                HistoryButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                ManualButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                CompressButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                EncryptButton.HorizontalContentAlignment = HorizontalAlignment.Left;
            }
            else
            {
                ConvertButton.Content = "⇄";
                TranslateButton.Content = "文";
                HistoryButton.Content = "◷";
                ManualButton.Content = "?";
                CompressButton.Content = "▣";
                EncryptButton.Content = "◆";

                ConvertButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                TranslateButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                HistoryButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                ManualButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                CompressButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                EncryptButton.HorizontalContentAlignment = HorizontalAlignment.Center;
            }
        }

        // Convert page
        private void Convert_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(
                new ConvertView(),
                "Convert",
                ConvertButton);
        }

        // Translation page
        private void Translate_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(
                new TranslateView(),
                "Translate",
                TranslateButton);
        }

        // History page
        private void History_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(
                new HistoryView(),
                "History",
                HistoryButton);
        }

        // Manual page
        private void Manual_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(
                new ManualView(),
                "Manual",
                ManualButton);
        }

        // Compression page
        private void Compress_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(
                new CompressView(),
                "Compress",
                CompressButton);
        }

        // Encryption page
        private void Encrypt_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(
                new EncryptView(),
                "Encrypt",
                EncryptButton);
        }

        // Settings page
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(
                new SettingsView(),
                "Settings",
                null);
        }
    }
}
