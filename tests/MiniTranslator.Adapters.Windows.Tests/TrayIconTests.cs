using System.Buffers.Binary;

namespace MiniTranslator.Adapters.Windows.Tests;

public class TrayIconTests
{
    [Fact]
    public void Selects_entry_closest_to_requested_small_icon_size()
    {
        var ico = BuildIco((16, 16, 32, 10), (32, 32, 32, 20), (48, 48, 32, 30));

        var small = Win32TrayIcon.SelectBestIconImage(ico, 16, 16);
        var medium = Win32TrayIcon.SelectBestIconImage(ico, 24, 24);

        Assert.NotNull(small);
        Assert.Equal(10, small.Value.Size);
        Assert.NotNull(medium);
        Assert.Equal(20, medium.Value.Size);
    }

    [Fact]
    public void Rejects_empty_or_invalid_icon_headers()
    {
        Assert.Null(Win32TrayIcon.SelectBestIconImage([], 16, 16));
        Assert.Null(Win32TrayIcon.SelectBestIconImage([0, 0, 2, 0, 1, 0], 16, 16));
    }

    [Theory]
    [InlineData("pwsh", true)]
    [InlineData("WindowsTerminal", true)]
    [InlineData("notepad", false)]
    [InlineData("", false)]
    public void Span_terminal_lookup_matches_case_insensitively(string name, bool expected)
    {
        Assert.Equal(expected, TerminalProcesses.IsTerminalProcess(name.AsSpan()));
    }

    [Fact]
    public void Zero_process_id_is_never_treated_as_terminal()
    {
        Assert.False(ClipboardSelectionReader.IsTerminalForegroundProcess(0));
    }

    private static byte[] BuildIco(params (byte Width, byte Height, ushort Bits, int PayloadSize)[] entries)
    {
        var headerSize = 6 + entries.Length * 16;
        var totalSize = headerSize + entries.Sum(e => e.PayloadSize);
        var buffer = new byte[totalSize];

        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4, 2), (ushort)entries.Length);

        var offset = headerSize;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = buffer.AsSpan(6 + i * 16, 16);
            entry[0] = entries[i].Width;
            entry[1] = entries[i].Height;
            BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(6, 2), entries[i].Bits);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(8, 4), (uint)entries[i].PayloadSize);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(12, 4), (uint)offset);
            offset += entries[i].PayloadSize;
        }

        return buffer;
    }
}
