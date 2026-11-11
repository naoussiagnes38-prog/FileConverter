using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Linq;

namespace FileConverter.Services
{
    internal static class LocalizationManager
    {
        private static ResourceDictionary currentDictionary;

        internal static string SettingsPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FileConverter", "settings.xml");
            }
        }

        public static string SystemLanguage
        {
            get { return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "French" : "English"; }
        }

        public static void Initialize()
        {
            ApplyLanguage(ReadPreference("Language"));
        }

        internal static string ReadPreference(string key)
        {
            try
            {
                return (string)XDocument.Load(SettingsPath).Root.Attribute(key);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Xml.XmlException) { }
            return null;
        }

        public static void ApplyLanguage(string language)
        {
            if (language != "French" && language != "English")
                language = SystemLanguage;

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var next = new ResourceDictionary
            {
                Source = new Uri("/FileConverter;component/Resources/Languages/" + language + ".xaml", UriKind.Relative)
            };
            dictionaries.Add(next);
            if (currentDictionary != null)
                dictionaries.Remove(currentDictionary);
            currentDictionary = next;

            var window = Application.Current.MainWindow as MainWindow;
            if (window != null)
                window.RefreshLanguage();
        }

        public static string Text(string english)
        {
            string key = "L_" + Regex.Replace(english, "[^A-Za-z0-9]+", "_").Trim('_');
            return Application.Current.TryFindResource(key) as string ?? english;
        }
    }
}
