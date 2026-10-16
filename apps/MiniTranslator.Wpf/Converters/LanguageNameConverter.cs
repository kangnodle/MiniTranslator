using System.Globalization;
using System.Windows.Data;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.Localization;

namespace MiniTranslator.Wpf.Converters;

public sealed class LanguageNameConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values is [Language language, UiLanguage uiLanguage] ? UiText.LanguageName(language, uiLanguage) : string.Empty;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
