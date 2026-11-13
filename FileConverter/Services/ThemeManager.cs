
using System;
using System.Windows;
using Microsoft.Win32;

namespace FileConverter.Services
{
    public static class ThemeManager
    {

        public static void Initialize()
        {
            string theme = LocalizationManager.ReadPreference("Theme");

            if (theme == "System")
            {
                theme = GetSystemTheme();
            }

            ApplyTheme(theme);
        }

        public static void ApplyTheme(string theme)
        {
            if (Application.Current == null)
                return;

            if (theme != "Light" && theme != "Dark")
            {
                theme = GetSystemTheme();
            }

            string dictionaryPath =
                theme == "Dark"
                ? "Themes/DarkTheme.xaml"
                : "Themes/LightTheme.xaml";

            ResourceDictionary newDictionary =
                new ResourceDictionary();

            // Load the new theme before changing existing resources.
            try
            {
                newDictionary.Source = new Uri(
                    "/FileConverter;component/" + dictionaryPath,
                    UriKind.Relative);
            }
            catch (Exception)
            {
                // If Dark cannot be loaded, try Light as a fallback.
                if (theme == "Dark")
                {
                    try
                    {
                        newDictionary = new ResourceDictionary();

                        newDictionary.Source = new Uri(
                            "/FileConverter;component/Themes/LightTheme.xaml",
                            UriKind.Relative);
                    }
                    catch (Exception)
                    {
                        // Keep the currently active resources.
                        return;
                    }
                }
                else
                {
                    // Keep the currently active resources.
                    return;
                }
            }

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            for (int index = dictionaries.Count - 1; index >= 0; index--)
            {
                if (IsThemeDictionary(dictionaries[index]))
                {
                    dictionaries[index] = newDictionary;
                    return;
                }
            }

            dictionaries.Add(newDictionary);
        }

        private static bool IsThemeDictionary(
            ResourceDictionary dictionary)
        {
            if (dictionary.Source == null)
                return false;

            string source = dictionary.Source.OriginalString;

            return source.EndsWith(
                       "LightTheme.xaml",
                       StringComparison.OrdinalIgnoreCase)
                   ||
                   source.EndsWith(
                       "DarkTheme.xaml",
                       StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetSystemTheme()
        {
            try
            {
                object value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    1);

                if (value != null && Convert.ToInt32(value) == 0)
                    return "Dark";
            }
            catch
            {
                // Use Light as fallback.
            }

            return "Light";
        }
    }
}
