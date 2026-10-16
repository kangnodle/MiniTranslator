using MiniTranslator.Domain.Common;

namespace MiniTranslator.Application.Abstractions;

public interface IClipboard
{
    Result<Unit> SetText(string text);
}
