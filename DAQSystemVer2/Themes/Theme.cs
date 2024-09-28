using DAQSystem.Common.Model;
using System.Windows;
using System.Windows.Media;

namespace DAQSystem.Application.Themes
{
    internal static class Theme
    {
        public static string GetString(string key) => GetResource<string>(key);

        public static ImageSource GetImageSource(string key) => GetResource<ImageSource>(key);

        public static void AddStringsDictionary(Language language)
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
            {
                Source = new Uri(string.Format(@"Themes/Strings.{0}.xaml", language), UriKind.RelativeOrAbsolute)
            });
        }

        private static T GetResource<T>(string key)
        {
            return (T)System.Windows.Application.Current.FindResource(key);
        }
    }
}
