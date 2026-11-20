namespace MiniTranslator.Application.Abstractions;

public interface ITranslatorSession
{
    Task ReleaseSessionAsync(CancellationToken cancellationToken = default);
}
