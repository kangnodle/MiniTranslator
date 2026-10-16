using MiniTranslator.Domain.Common;

namespace MiniTranslator.Application.Abstractions;

/// <summary>Current user's login startup registration, stored by the operating system.</summary>
public interface IStartupRegistration
{
    Result<bool> ReadEnabled();

    Result<Unit> SetEnabled(bool enabled);
}
