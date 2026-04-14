using System.IO;
using System.Text;

namespace Arrowgene.Lua.Decompiler.Util;

public static class FileUtils
{
    /// <summary>
    /// Opens a text file with BOM detection. Returns a Stream that yields UTF-8 bytes
    /// regardless of whether the file is UTF-8, UTF-16LE, or UTF-16BE.
    /// </summary>
    public static Stream CreateSmartTextFileReader(string path)
    {
        FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        byte[] header = new byte[2];
        int headerLength = 0;
        while (headerLength < header.Length)
        {
            int n = fs.Read(header, headerLength, header.Length - headerLength);
            if (n == 0) break;
            headerLength += n;
        }
        if (headerLength >= 2 && header[0] == 0xff && header[1] == 0xfe)
        {
            fs.Position = 2;
            return new ReencodingStream(fs, Encoding.Unicode);
        }
        else if (headerLength >= 2 && header[0] == 0xfe && header[1] == 0xff)
        {
            fs.Position = 2;
            return new ReencodingStream(fs, Encoding.BigEndianUnicode);
        }
        else
        {
            fs.Position = 0;
            return fs;
        }
    }

    /// <summary>
    /// Wraps a non-UTF-8 Stream and re-emits its contents as UTF-8 bytes one byte at a time.
    /// Used for transparent UTF-16 → UTF-8 conversion.
    /// </summary>
    private sealed class ReencodingStream : Stream
    {
        private readonly StreamReader _reader;
        private readonly byte[] _pending;
        private int _pendingPos;
        private int _pendingLen;

        public ReencodingStream(Stream raw, Encoding encoding)
        {
            _reader = new StreamReader(raw, encoding, false, 1024, false);
            _pending = new byte[8];
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new System.NotSupportedException();
        public override long Position { get => throw new System.NotSupportedException(); set => throw new System.NotSupportedException(); }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();
        public override void SetLength(long value) => throw new System.NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();

        public override int ReadByte()
        {
            if (_pendingPos < _pendingLen)
            {
                return _pending[_pendingPos++];
            }
            int ch = _reader.Read();
            if (ch < 0) return -1;
            if (ch <= 0x7F) return ch;
            // Encode this char (plus low surrogate if needed) to UTF-8 and stash into _pending.
            char[] chars;
            if (char.IsHighSurrogate((char)ch))
            {
                int low = _reader.Read();
                chars = low >= 0 ? new[] { (char)ch, (char)low } : new[] { (char)ch };
            }
            else
            {
                chars = new[] { (char)ch };
            }
            _pendingLen = Encoding.UTF8.GetBytes(chars, 0, chars.Length, _pending, 0);
            _pendingPos = 0;
            return _pending[_pendingPos++];
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int produced = 0;
            while (produced < count)
            {
                int b = ReadByte();
                if (b < 0) break;
                buffer[offset + produced++] = (byte)b;
            }
            return produced;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _reader.Dispose();
            base.Dispose(disposing);
        }
    }
}
