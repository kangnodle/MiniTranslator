using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Translations;

public static class TranslationErrors
{
    public static Error EmptyText { get; } =
        new("Translation.EmptyText", "번역할 내용을 입력하세요.");

    public static Error TooLong(int length) =>
        new("Translation.TooLong", $"한 번에 최대 {TranslationRequest.MaxLength:N0}자까지 번역할 수 있습니다. (현재 {length:N0}자)") { Parameter = length.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) };

    public static Error EmptyResponse { get; } =
        new("Translation.EmptyResponse", "번역 서비스가 빈 응답을 반환했습니다.");

    public static Error RateLimited { get; } =
        new("Translation.RateLimited", "Google이 번역 요청을 일시적으로 제한했습니다. (HTTP 429) 잠시 후 다시 시도하세요. 공개 번역 서비스는 네트워크의 자동화 요청을 차단할 수 있습니다.");

    public static Error ProviderRejected(int statusCode, string? detail) =>
        new Error("Translation.ProviderRejected", $"번역 서비스가 요청을 거부했습니다. (HTTP {statusCode})") { Parameter = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture) }.WithDetail(detail);

    public static Error Network(string? detail) =>
        new Error("Translation.Network", "번역 서비스에 연결할 수 없습니다. 네트워크 연결을 확인하세요.").WithDetail(detail);

    public static Error ClaudeUnavailable { get; } =
        new("Translation.ClaudeUnavailable", "Claude Code를 실행할 수 없습니다. Claude Code를 설치하고 터미널에서 'claude auth login'으로 로그인하거나, 설정에서 번역 엔진을 Google로 바꾸세요.");

    public static Error ClaudeFailed(string? detail) =>
        new Error("Translation.ClaudeFailed", "Claude가 번역을 완료하지 못했습니다. 터미널에서 'claude auth status'로 로그인 상태를 확인한 뒤 다시 시도하세요.").WithDetail(detail);

    public static Error ClaudeTimedOut { get; } =
        new("Translation.ClaudeTimedOut", "Claude가 제한 시간 안에 응답하지 않았습니다. 다시 시도하세요.");

    public static Error CodexUnavailable { get; } =
        new("Translation.CodexUnavailable", "Codex CLI를 실행할 수 없습니다. Codex CLI를 설치해 PATH에 추가하거나 설정에서 Codex 실행 파일 경로를 지정하세요. 또는 번역 엔진을 Google로 바꾸세요.");

    public static Error CodexSignedOut { get; } =
        new("Translation.CodexSignedOut", "Codex에 로그인되어 있지 않습니다. 설정 → 번역 엔진에서 Codex에 로그인한 뒤 다시 시도하세요.");

    public static Error CodexFailed(string? detail) =>
        new Error("Translation.CodexFailed", "Codex가 번역을 완료하지 못했습니다. 다시 시도하세요.").WithDetail(detail);

    public static Error CodexTimedOut { get; } =
        new("Translation.CodexTimedOut", "Codex가 제한 시간 안에 응답하지 않았습니다. 다시 시도하세요.");

    public static Error CodexAccountFailed(string? detail) =>
        new Error("Translation.CodexAccountFailed", "Codex 로그인 상태를 확인하거나 바꾸지 못했습니다. 다시 시도하세요.").WithDetail(detail);

    public static Error CodexSignInFailed(string? detail) =>
        new Error("Translation.CodexSignInFailed", "Codex 로그인을 완료하지 못했습니다. 다시 시도하세요.").WithDetail(detail);

    public static Error CodexAccountTimedOut { get; } =
        new("Translation.CodexAccountTimedOut", "Codex 로그인 작업이 제한 시간 안에 끝나지 않았습니다. 다시 시도하세요.");
}
