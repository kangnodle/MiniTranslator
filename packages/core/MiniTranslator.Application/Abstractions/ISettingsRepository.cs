using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Application.Abstractions;

public interface ISettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task<Result<Unit>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
