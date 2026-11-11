using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace FileConverter.Views
{
    public partial class SettingsView : UserControl
    {
        private string settingsFile;

        private string selectedTheme = "System";
        private string selectedLanguage = "System";

        private bool isInitializing = true;

        public SettingsView()
        {
            InitializeComponent();

            settingsFile = FileConverter.Services.LocalizationManager.SettingsPath;
            LoadSettings();

            ApplyInitialSelections();

            isInitializing = false;

            UpdateLanguage();
        }

        private void LoadSettings()
        {
            selectedTheme = FileConverter.Services.LocalizationManager.ReadPreference("Theme") ?? "System";
            selectedLanguage = FileConverter.Services.LocalizationManager.ReadPreference("Language") ?? "System";
        }

        // Save preferences
        private bool SaveSettings()
        {
            try
            {
                XDocument document = new XDocument(
                    new XElement("Settings",
                        new XAttribute("Theme", selectedTheme),
                        new XAttribute("Language", selectedLanguage)
                    )
                );

                Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
                string temp = settingsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    document.Save(temp);
                    if (File.Exists(settingsFile))
                        File.Replace(temp, settingsFile, null);
                    else
                        File.Move(temp, settingsFile);
                }
                finally
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to save settings.\n\n" + ex.Message,
                    "Settings Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        // Select initial options
        private void ApplyInitialSelections()
        {
            LightThemeRadio.IsChecked = selectedTheme == "Light";
            DarkThemeRadio.IsChecked = selectedTheme == "Dark";
            SystemThemeRadio.IsChecked = selectedTheme != "Light" && selectedTheme != "Dark";

            EnglishRadio.IsChecked = selectedLanguage == "English";
            FrenchRadio.IsChecked = selectedLanguage == "French";
            SystemLanguageRadio.IsChecked = selectedLanguage != "English" && selectedLanguage != "French";
        }

        // Theme selection

        private void ThemeOption_Checked(
            object sender, RoutedEventArgs e)
        {
            if (isInitializing)
                return;

            selectedTheme = sender == LightThemeRadio ? "Light" :
                sender == DarkThemeRadio ? "Dark" : "System";

            if (!SaveSettings())
            {
                isInitializing = true;
                LoadSettings();
                ApplyInitialSelections();
                isInitializing = false;
                return;
            }

            FileConverter.Services.ThemeManager.ApplyTheme(
                selectedTheme);

            SettingsStatusText.Text =
                selectedLanguage == "French" ||
                (selectedLanguage == "System" && FileConverter.Services.LocalizationManager.SystemLanguage == "French")
                ? "Préférence de thème enregistrée."
                : "Theme preference saved.";
        }


        // Language selection
        private void LanguageOption_Checked(
            object sender, RoutedEventArgs e)
        {
            if (isInitializing)
                return;

            selectedLanguage = sender == EnglishRadio ? "English" :
                sender == FrenchRadio ? "French" : "System";

            if (!SaveSettings())
            {
                isInitializing = true;
                LoadSettings();
                ApplyInitialSelections();
                isInitializing = false;
                return;
            }
            FileConverter.Services.LocalizationManager.ApplyLanguage(selectedLanguage);
            UpdateLanguage();

            SettingsStatusText.Text =
                selectedLanguage == "French" ||
                (selectedLanguage == "System" && FileConverter.Services.LocalizationManager.SystemLanguage == "French")
                ? "Préférence linguistique enregistrée."
                : "Language preference saved.";
        }

        // Update Settings screen language
        private void UpdateLanguage()
        {
            string language = selectedLanguage;

            if (language != "English" && language != "French")
            {
                language = FileConverter.Services.LocalizationManager.SystemLanguage;
            }

            bool french = language == "French";

            if (french)
            {
                PageHeading.Text = "Paramètres";
                PageDescription.Text =
                    "Personnalisez votre expérience FileConverter.";

                AppearanceHeading.Text = "Apparence";
                AppearanceDescription.Text =
                    "Choisissez l'apparence de FileConverter.";
                ThemeLabel.Text = "Thème de l'application";
                LightThemeRadio.Content = "Clair";
                DarkThemeRadio.Content = "Sombre";
                ThemeHint.Text =
                    "Votre choix sera enregistré automatiquement.";

                LanguageHeading.Text = "Langue";
                LanguageDescription.Text =
                    "Choisissez la langue de votre application.";
                LanguageLabel.Text = "Langue de l'application";
                EnglishRadio.Content = "Anglais";
                FrenchRadio.Content = "Français";
                LanguageHint.Text =
                    "Votre choix sera enregistré automatiquement.";

                AboutHeading.Text = "À propos de FileConverter";
                AboutDescription.Text =
                    "Informations sur cette application.";
                ApplicationNameLabel.Text = "Application";
                VersionLabel.Text = "Version";
            }
            else
            {
                PageHeading.Text = "Settings";
                PageDescription.Text =
                    "Customize your FileConverter experience.";

                AppearanceHeading.Text = "Appearance";
                AppearanceDescription.Text =
                    "Choose how FileConverter looks.";
                ThemeLabel.Text = "Application theme";
                LightThemeRadio.Content = "Light";
                DarkThemeRadio.Content = "Dark";
                ThemeHint.Text =
                    "Your selection will be saved automatically.";

                LanguageHeading.Text = "Language";
                LanguageDescription.Text =
                    "Choose your preferred application language.";
                LanguageLabel.Text = "Application language";
                EnglishRadio.Content = "English";
                FrenchRadio.Content = "French";
                LanguageHint.Text =
                    "Your selection will be saved automatically.";

                AboutHeading.Text = "About FileConverter";
                AboutDescription.Text =
                    "Information about this application.";
                ApplicationNameLabel.Text = "Application";
                VersionLabel.Text = "Version";
            }
        }
    }
}
