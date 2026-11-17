using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Testing;

public sealed class FakeStartupRegistration : IStartupRegistration
{
    public bool Enabled { get; set; }

    public Error? ReadError { get; set; }

    public bool? FailWhenEnabled { get; set; }

    public List<bool> Changes { get; } = [];

    public Result<bool> ReadEnabled() => ReadError is null ? Enabled : ReadError;

    public Result<Unit> SetEnabled(bool enabled)
    {
        Changes.Add(enabled);
        if (enabled == FailWhenEnabled) return new Error("Startup.WriteFailed", "자동 실행 변경 실패");
        Enabled = enabled;
        return Result.Success();
    }
}

public sealed class InMemorySettingsRepository(AppSettings? initial = null) : ISettingsRepository
{
    public AppSettings? Saved { get; private set; } = initial;

    public int SaveCount { get; private set; }

    public Error? FailSaveWith { get; set; }

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Saved ?? AppSettings.Default);

    public Task<Result<Unit>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (FailSaveWith is not null)
        {
            return Task.FromResult(Result.Failure(FailSaveWith));
        }

        Saved = settings;
        SaveCount++;
        return Task.FromResult(Result.Success());
    }
}

public sealed class FakeTranslator : ITranslator
{
    public List<TranslationRequest> Calls { get; } = [];

    public string? DetectedLanguageCode { get; set; }

    public Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(request);
        var detected = request.Languages.Source.IsAutoDetect ? DetectedLanguageCode : null;
        return Task.FromResult(Result.Success(new TranslationResult(request.Text, $"[{request.Text}]", request.Languages, detected)));
    }
}

public sealed class FakeGlobalHotkey : IGlobalHotkey
{
    public HashSet<HotkeyGesture> Taken { get; } = [];

    public HotkeyGesture? Registered { get; private set; }

    public event EventHandler? Pressed;

    public Result<Unit> Register(HotkeyGesture gesture)
    {
        Unregister();
        if (Taken.Contains(gesture))
        {
            return Result.Failure(HotkeyErrors.AlreadyInUse(gesture));
        }

        Registered = gesture;
        return Result.Success();
    }

    public void Unregister() => Registered = null;

    public void Press() => Pressed?.Invoke(this, EventArgs.Empty);

    public void Dispose() => Unregister();
}

public sealed class FakeSelectionReader(string? selection) : ITextSelectionReader
{
    public int Calls { get; private set; }

    public Task<string?> ReadSelectionAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(selection);
    }
}

public sealed class FakeClipboard : IClipboard
{
    public string? Text { get; private set; }

    public Result<Unit> SetText(string text)
    {
        Text = text;
        return Result.Success();
    }
}
