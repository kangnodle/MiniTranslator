using MiniTranslator.Domain.Common;

namespace MiniTranslator.Application.Abstractions;

public interface IStartupRegistration
{
    Result<bool> ReadEnabled();

    Result<Unit> SetEnabled(bool enabled);
}
