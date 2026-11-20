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

    [Fact]
    public void Oversized_unicode_buffers_are_rejected()
    {
        var memory = WpfClipboard.AllocateText(new string('a', 33));
        try
        {
            Assert.NotEqual(nint.Zero, memory);
            Assert.Null(WpfClipboard.ReadUnicodeText(memory, maxLength: 32));
            Assert.Equal(new string('a', 33), WpfClipboard.ReadUnicodeText(memory, maxLength: 33));
        }
        finally
        {
            NativeMethods.GlobalFree(memory);
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(42u)]
    public void Native_dword_reader_round_trips_allocated_values(uint value)
    {
        var memory = WpfClipboard.AllocateDword(value);
        try
        {
            Assert.Equal(value, WpfClipboard.ReadDword(memory));
        }
        finally
        {
            NativeMethods.GlobalFree(memory);
        }
    }

    [Fact]
    public void Native_dword_reader_rejects_null_and_undersized_buffers()
    {
        Assert.Null(WpfClipboard.ReadDword(0));

        var small = NativeMethods.GlobalAlloc(0x0002, 2);
        try
        {
            Assert.Null(WpfClipboard.ReadDword(small));
        }
        finally
        {
            NativeMethods.GlobalFree(small);
        }
    }

    [Fact]
    public void Native_dword_buffer_round_trips()
    {
        var memory = WpfClipboard.AllocateDword(0);
        try
        {
            Assert.NotEqual(nint.Zero, memory);
            var pointer = NativeMethods.GlobalLock(memory);
            Assert.NotEqual(nint.Zero, pointer);
            try
            {
                Assert.Equal(0, Marshal.ReadInt32(pointer));
            }
            finally
            {
                NativeMethods.GlobalUnlock(memory);
            }
        }
        finally
        {
            NativeMethods.GlobalFree(memory);
        }
    }

    [Theory]
    [InlineData(WpfClipboard.ExcludeFromMonitorFormat, true)]
    [InlineData(WpfClipboard.ClipboardViewerIgnoreFormat, true)]
    [InlineData("Text", false)]
    public void Privacy_exclusion_formats_are_detected(string format, bool expected)
    {
        Assert.Equal(expected, WpfClipboard.HasPrivacyExclusionFormat([format]));
    }

    [Theory]
    [InlineData(WpfClipboard.CanIncludeInHistoryFormat, 0, true)]
    [InlineData(WpfClipboard.CanIncludeInHistoryFormat, 1, false)]
    [InlineData(WpfClipboard.CanUploadToCloudFormat, 0, true)]
    [InlineData(WpfClipboard.CanUploadToCloudFormat, 1, false)]
    public void History_and_cloud_exclusion_flags_check_zero_value(string format, int flag, bool expected)
    {
        var bytes = BitConverter.GetBytes(flag);
        Assert.Equal(expected, WpfClipboard.HasPrivacyExclusionFormat([format], _ => new MemoryStream(bytes)));
    }

    [Fact]
    public void Windows_adapter_locks_native_dll_search_path_to_system32()
    {
        var attribute = typeof(NativeMethods).Assembly
            .GetCustomAttributes(typeof(DefaultDllImportSearchPathsAttribute), inherit: false)
            .OfType<DefaultDllImportSearchPathsAttribute>()
            .Single();

        Assert.Equal(DllImportSearchPath.System32, attribute.Paths);
    }
}
