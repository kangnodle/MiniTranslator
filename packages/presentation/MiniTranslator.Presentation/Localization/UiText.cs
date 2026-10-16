using System.Collections;
using System.Globalization;
using System.Resources;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Presentation.Localization;

public static class UiText
{
    private static readonly ResourceManager Resources = new("MiniTranslator.Presentation.Localization.Strings", typeof(UiText).Assembly);

    public static CultureInfo Culture(UiLanguage language) => CultureInfo.GetCultureInfo(language == UiLanguage.English ? "en" : "ko");

    public static string Get(string key, UiLanguage language) =>
        Resources.GetString(key, Culture(language)) ?? throw new ArgumentException($"Missing UI text: {key}", nameof(key));

    public static string Format(string key, UiLanguage language, params object?[] arguments) =>
        string.Format(Culture(language), Get(key, language), arguments);

    public static IEnumerable<KeyValuePair<string, string>> All(UiLanguage language) =>
        Resources.GetResourceSet(Culture(language), createIfNotExists: true, tryParents: true)!
            .Cast<DictionaryEntry>().Select(entry => new KeyValuePair<string, string>((string)entry.Key, (string)entry.Value!));

    public static string LanguageName(Language language, UiLanguage uiLanguage) =>
        uiLanguage == UiLanguage.English ? language.EnglishName : language.DisplayName;

    public static string Error(Error error, UiLanguage language)
    {
        if (language == UiLanguage.Korean) return error.Message;
        var format = Resources.GetString(error.Code, Culture(language));
        if (format is null) return error.Message;
        var message = string.Format(Culture(language), format, error.Parameter);
        var detail = error.Message.IndexOf('\n');
        return detail < 0 ? message : message + error.Message[detail..];
    }
}
