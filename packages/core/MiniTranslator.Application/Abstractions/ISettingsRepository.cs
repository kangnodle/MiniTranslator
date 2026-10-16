using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Application.Abstractions;

/// <summary>Persists <see cref="AppSettings"/>.</summary>
public interface ISettingsRepository
{
    /// <summary>Loads saved settings, or <see cref="AppSettings.Default"/> when none exist or they are unreadable.</summary>
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task<Result<Unit>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
