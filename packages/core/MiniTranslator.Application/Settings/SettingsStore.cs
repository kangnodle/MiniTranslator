using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Application.Settings;

/// <summary>
/// Single source of truth for the current <see cref="AppSettings"/>.
/// Changes are expressed as pure functions (<c>AppSettings → AppSettings</c>) and persisted before being published.
/// </summary>
public sealed class SettingsStore(ISettingsRepository repository)
{
    private readonly SemaphoreSlim _updates = new(1, 1);

    public AppSettings Current { get; private set; } = AppSettings.Default;

    public event EventHandler<AppSettings>? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Current = await repository.LoadAsync(cancellationToken);
    }

    public async Task<Result<AppSettings>> UpdateAsync(
        Func<AppSettings, AppSettings> update,
        CancellationToken cancellationToken = default)
    {
        await _updates.WaitAsync(cancellationToken);
        try
        {
            var next = update(Current);
            if (next == Current)
            {
                return next;
            }

            var saved = await repository.SaveAsync(next, cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error;
            }

            Current = next;
            Changed?.Invoke(this, next);
            return next;
        }
        finally
        {
            _updates.Release();
        }
    }
}
