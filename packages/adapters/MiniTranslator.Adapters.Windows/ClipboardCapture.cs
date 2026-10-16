namespace MiniTranslator.Adapters.Windows;

internal static class ClipboardCapture
{
    public static bool IsExpected(
        nint sourceWindow, nint foregroundWindow,
        uint sourceProcessId, uint ownerProcessId,
        uint expectedSequence, uint currentSequence) =>
        sourceWindow != 0 && sourceWindow == foregroundWindow
        && sourceProcessId != 0 && sourceProcessId == ownerProcessId
        && expectedSequence == currentSequence;
}
