using MiniTranslator.Domain.Common;

namespace MiniTranslator.Adapters.JsonSettings;

internal static class SettingsErrors
{
    public static Error SaveFailed(string? detail) =>
        new Error("Settings.SaveFailed", "설정을 저장하지 못했습니다.").WithDetail(detail);
}
