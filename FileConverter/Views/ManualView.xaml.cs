using System;
using FileConverter.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FileConverter.Views
{
    public partial class ManualView : UserControl
    {
        private class ManualTopic
        {
            public string Title { get; set; }

            public string Description { get; set; }

            public string[] Steps { get; set; }
        }

        private List<ManualTopic> topics;

        private Dictionary<string, Border> topicCards;

        public ManualView()
        {
            InitializeComponent();

            LoadTopics();

            InitializeTopicCards();
        }

        // ==========================================
        // LOAD HELP TOPICS
        // ==========================================

        private void LoadTopics()
        {
            topics = new List<ManualTopic>();

            topics.Add(new ManualTopic
            {
                Title = "Getting Started",

                Description = "Learn the basic features and navigation of FileConverter.",

                Steps = new string[]
                {
                    "Open FileConverter.",
                    "Use the sidebar to navigate between sections.",
                    "Open Convert to select files.",
                    "Visit History to review conversion records."
                }
            });

            topics.Add(new ManualTopic
            {
                Title = "File Conversion",

                Description = "Learn how to prepare files for conversion.",

                Steps = new string[]
                {
                    "Open the Convert section.",
                    "Click Choose Files to select one or more files.",
                    "You can also drag and drop files into the selection area.",
                    "Review the selected files.",
                    "Remove individual files or use Clear All.",
                    "Choose the conversion method that matches your files, then click Convert Files.",
                    "Word and Excel conversions use the locally installed Office apps. PDF to images uses Poppler."
                }
            });

            topics.Add(new ManualTopic
            {
                Title = "Conversion History",

                Description = "Learn how to find previous operation records.",

                Steps = new string[]
                {
                    "Open the History section.",
                    "Use the search field to find records.",
                    "Select All to display every record.",
                    "Select Completed to display successful records.",
                    "Select Failed to display unsuccessful records."
                }
            });

            topics.Add(new ManualTopic
            {
                Title = "File Compression",

                Description = "Create one ZIP archive per selected file.",

                Steps = new string[]
                {
                    "Open the Compress section.",
                    "Select the file you want to compress.",
                    "Review the available compression options.",
                    "Choose an output destination.",
                    "Click Create ZIP archives."
                }
            });

            topics.Add(new ManualTopic
            {
                Title = "File Encryption",

                Description = "Encrypt files and restore them with the original password.",

                Steps = new string[]
                {
                    "Open the Encrypt section.",
                    "Select the files you want to protect.",
                    "Enter and confirm your password, then click Encrypt Files.",
                    "To restore files, select the .encrypted files, enter their original password and click Decrypt Files.",
                    "Choose another output folder if a file with the original name already exists."
                }
            });

            topics.Add(new ManualTopic
            {
                Title = "Application Settings",

                Description = "Learn about configuring FileConverter.",

                Steps = new string[]
                {
                    "Open Settings using the settings shortcut.",
                    "Review the available preferences.",
                    "Choose English or French.",
                    "Choose Light or Dark.",
                    "Preferences are saved automatically."
                }
            });
        }

        // ==========================================
        // CONNECT TOPICS TO THEIR CARDS
        // ==========================================

        private void InitializeTopicCards()
        {
            topicCards = new Dictionary<string, Border>(
                StringComparer.OrdinalIgnoreCase);

            topicCards.Add("Getting Started", GettingStartedCard);

            topicCards.Add("File Conversion", FileConversionCard);

            topicCards.Add("Conversion History", ConversionHistoryCard);

            topicCards.Add("File Compression", FileCompressionCard);

            topicCards.Add("File Encryption", FileEncryptionCard);

            topicCards.Add("Application Settings", ApplicationSettingsCard);
        }

        // ==========================================
        // LIVE SEARCH
        // ==========================================

        private void ManualSearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (topics == null || topicCards == null)
            {
                return;
            }

            string searchText = ManualSearchBox.Text.Trim();

            int visibleTopics = 0;

            foreach (ManualTopic topic in topics)
            {
                bool matches = string.IsNullOrWhiteSpace(searchText)
                    || LocalizationManager.Text(topic.Title).IndexOf(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) >= 0
                    || LocalizationManager.Text(topic.Description).IndexOf(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) >= 0
                    || topic.Steps.Any(step =>
                        LocalizationManager.Text(step).IndexOf(
                            searchText,
                            StringComparison.OrdinalIgnoreCase) >= 0);

                Border card = topicCards[topic.Title];

                if (matches)
                {
                    card.Visibility = Visibility.Visible;
                    visibleTopics++;
                }
                else
                {
                    card.Visibility = Visibility.Collapsed;
                }
            }

            if (visibleTopics == 0)
            {
                NoResultsText.Visibility = Visibility.Visible;
            }
            else
            {
                NoResultsText.Visibility = Visibility.Collapsed;
            }
        }

        // ==========================================
        // OPEN A HELP ARTICLE
        // ==========================================

        private void TopicButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
            {
                return;
            }

            string topicTitle = button.Tag as string;

            if (string.IsNullOrWhiteSpace(topicTitle))
            {
                return;
            }

            ManualTopic selectedTopic = topics.FirstOrDefault(
                t => t.Title == topicTitle);

            if (selectedTopic == null)
            {
                return;
            }

            DisplayArticle(selectedTopic);
        }

        // ==========================================
        // DISPLAY ARTICLE CONTENT
        // ==========================================

        private void DisplayArticle(ManualTopic topic)
        {
            ArticleTitle.Text = LocalizationManager.Text(topic.Title);

            ArticleDescription.Text = LocalizationManager.Text(topic.Description);

            // Remove the previous article instructions.

            ArticleStepsPanel.Children.Clear();

            // Generate the numbered instructions.

            for (int i = 0; i < topic.Steps.Length; i++)
            {
                Border stepCard = new Border();

                // Theme-aware card background and border.

                stepCard.SetResourceReference(
                    Border.BackgroundProperty,
                    "CardBackgroundBrush");

                stepCard.SetResourceReference(
                    Border.BorderBrushProperty,
                    "BorderBrush");

                stepCard.BorderThickness = new Thickness(1);

                stepCard.CornerRadius = new CornerRadius(8);

                stepCard.Padding = new Thickness(15);

                stepCard.Margin = new Thickness(0, 0, 0, 12);

                Grid stepGrid = new Grid();

                stepGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(42)
                    });

                stepGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(1, GridUnitType.Star)
                    });

                // ==================================
                // NUMBER INDICATOR
                // ==================================

                Border numberCircle = new Border();

                numberCircle.Width = 30;

                numberCircle.Height = 30;

                numberCircle.CornerRadius = new CornerRadius(15);

                numberCircle.SetResourceReference(
                    Border.BackgroundProperty,
                    "SelectedItemBrush");

                numberCircle.HorizontalAlignment =
                    HorizontalAlignment.Left;

                numberCircle.VerticalAlignment =
                    VerticalAlignment.Top;

                TextBlock numberText = new TextBlock();

                numberText.Text = (i + 1).ToString();

                numberText.FontSize = 13;

                numberText.FontWeight = FontWeights.SemiBold;

                numberText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "AccentBrush");

                numberText.HorizontalAlignment =
                    HorizontalAlignment.Center;

                numberText.VerticalAlignment =
                    VerticalAlignment.Center;

                numberCircle.Child = numberText;

                Grid.SetColumn(numberCircle, 0);

                stepGrid.Children.Add(numberCircle);

                // ==================================
                // INSTRUCTION TEXT
                // ==================================

                TextBlock instructionText = new TextBlock();

                instructionText.Text = LocalizationManager.Text(topic.Steps[i]);

                instructionText.FontSize = 13;

                instructionText.SetResourceReference(
                    TextBlock.ForegroundProperty,
                    "PrimaryTextBrush");

                instructionText.TextWrapping = TextWrapping.Wrap;

                instructionText.VerticalAlignment =
                    VerticalAlignment.Center;

                Grid.SetColumn(instructionText, 1);

                stepGrid.Children.Add(instructionText);

                // Add the instruction content to its card.

                stepCard.Child = stepGrid;

                ArticleStepsPanel.Children.Add(stepCard);
            }

            // Hide the overview and display the article.

            OverviewView.Visibility = Visibility.Collapsed;

            ArticleView.Visibility = Visibility.Visible;

            ArticleView.ScrollToTop();
        }

        // ==========================================
        // RETURN TO TOPIC OVERVIEW
        // ==========================================

        private void BackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ArticleView.Visibility = Visibility.Collapsed;

            OverviewView.Visibility = Visibility.Visible;

            OverviewView.ScrollToTop();
        }
    }
}