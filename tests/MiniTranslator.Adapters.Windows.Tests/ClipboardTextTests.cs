using System.Runtime.InteropServices;

namespace MiniTranslator.Adapters.Windows.Tests;

public class ClipboardTextTests
{
    [Theory]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("한글 日本語 🙂")]
    public void Native_unicode_buffers_round_trip_without_using_the_system_clipboard(string text)
    {
        var memory = WpfClipboard.AllocateText(text);
        try
        {
            Assert.NotEqual(nint.Zero, memory);
            Assert.Equal(text, WpfClipboard.ReadUnicodeText(memory));
        }
        finally
        {
            NativeMethods.GlobalFree(memory);
        }
    }

    [Fact]
    public void Unterminated_text_is_rejected_within_the_allocated_buffer()
    {
        var memory = NativeMethods.GlobalAlloc(0x0002, 16);
        try
        {
            Assert.NotEqual(nint.Zero, memory);
            var pointer = NativeMethods.GlobalLock(memory);
            Assert.NotEqual(nint.Zero, pointer);
            try
            {
                var size = (int)NativeMethods.GlobalSize(memory);
                for (var offset = 0; offset < size; offset += sizeof(char))
                {
                    Marshal.WriteInt16(pointer, offset, 'a');
                }
            }
            finally
            {
                NativeMethods.GlobalUnlock(memory);
            }

            Assert.Null(WpfClipboard.ReadUnicodeText(memory));
        }
        finally
        {
            NativeMethods.GlobalFree(memory);
        }
    }

    [Fact]
    public void An_invalid_clipboard_handle_has_no_text() => Assert.Null(WpfClipboard.ReadUnicodeText(0));
}
