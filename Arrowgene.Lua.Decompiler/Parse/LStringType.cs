using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.LStringType and its Lua-version subclasses (50/53/54/55).
/// </summary>
public abstract class LStringType : BObjectType<LString>
{
    public static LStringType Get(Version.StringType type)
    {
        return type switch
        {
            Version.StringType.LUA50 => new LStringType50(),
            Version.StringType.LUA53 => new LStringType53(),
            Version.StringType.LUA54 => new LStringType54(),
            Version.StringType.LUA55 => new LStringType55(),
            _ => throw new InvalidOperationException(),
        };
    }

    // Java's ThreadLocal<StringBuilder> is overkill here; unluac uses a single
    // parser thread in practice. A per-instance StringBuilder matches behavior.
    protected readonly StringBuilder b = new StringBuilder();
}

internal sealed class LStringType50 : LStringType
{
    public override LString Parse(LuaByteBuffer buffer, BHeader header)
    {
        BInteger sizeT = header.sizeT.Parse(buffer, header);
        b.Length = 0;
        sizeT.Iterate(() =>
        {
            b.Append((char)(0xFF & buffer.Get()));
        });
        if (b.Length == 0)
        {
            return LString.NULL;
        }
        char last = b[b.Length - 1];
        b.Length--;
        string s = b.ToString();
        if (header.debug)
        {
            Console.WriteLine("-- parsed <string> \"" + s + "\"");
        }
        return new LString(s, last);
    }

    public override void Write(Stream @out, BHeader header, LString str)
    {
        int len = str.value.Length;
        if (ReferenceEquals(str, LString.NULL))
        {
            header.sizeT.Write(@out, header, header.sizeT.Create(0));
        }
        else
        {
            header.sizeT.Write(@out, header, header.sizeT.Create(len + 1));
            for (int i = 0; i < len; i++)
            {
                @out.WriteByte((byte)str.value[i]);
            }
            @out.WriteByte(0);
        }
    }
}

internal sealed class LStringType53 : LStringType
{
    public override LString Parse(LuaByteBuffer buffer, BHeader header)
    {
        BInteger sizeT;
        int size = 0xFF & buffer.Get();
        if (size == 0)
        {
            return LString.NULL;
        }
        if (size == 0xFF)
        {
            sizeT = header.sizeT.Parse(buffer, header);
        }
        else
        {
            sizeT = new BInteger(size);
        }
        b.Length = 0;
        bool first = true;
        sizeT.Iterate(() =>
        {
            if (!first)
            {
                b.Append((char)(0xFF & buffer.Get()));
            }
            else
            {
                first = false;
            }
        });
        string s = b.ToString();
        if (header.debug)
        {
            Console.WriteLine("-- parsed <string> \"" + s + "\"");
        }
        return new LString(s);
    }

    public override void Write(Stream @out, BHeader header, LString str)
    {
        if (ReferenceEquals(str, LString.NULL))
        {
            @out.WriteByte(0);
        }
        else
        {
            int len = str.value.Length + 1;
            if (len < 0xFF)
            {
                @out.WriteByte((byte)len);
            }
            else
            {
                @out.WriteByte(0xFF);
                header.sizeT.Write(@out, header, header.sizeT.Create(len));
            }
            for (int i = 0; i < str.value.Length; i++)
            {
                @out.WriteByte((byte)str.value[i]);
            }
        }
    }
}

internal sealed class LStringType54 : LStringType
{
    public override LString Parse(LuaByteBuffer buffer, BHeader header)
    {
        BInteger sizeT = header.sizeT.Parse(buffer, header);
        if (sizeT.AsInt() == 0)
        {
            return LString.NULL;
        }
        b.Length = 0;
        bool first = true;
        sizeT.Iterate(() =>
        {
            if (!first)
            {
                b.Append((char)(0xFF & buffer.Get()));
            }
            else
            {
                first = false;
            }
        });
        string s = b.ToString();
        if (header.debug)
        {
            Console.WriteLine("-- parsed <string> \"" + s + "\"");
        }
        return new LString(s);
    }

    public override void Write(Stream @out, BHeader header, LString str)
    {
        if (ReferenceEquals(str, LString.NULL))
        {
            header.sizeT.Write(@out, header, header.sizeT.Create(0));
        }
        else
        {
            header.sizeT.Write(@out, header, header.sizeT.Create(str.value.Length + 1));
            for (int i = 0; i < str.value.Length; i++)
            {
                @out.WriteByte((byte)str.value[i]);
            }
        }
    }
}

internal sealed class LStringType55 : LStringType
{
    private readonly List<LString> _saved = new List<LString>();
    // Write-side dedup index. Mirrors _saved but built up as Write is called:
    // the first time a given string is emitted, it gets an index; subsequent
    // occurrences are encoded as sizeT=0 followed by (index + 1), matching
    // luac 5.5's string-constant dedup on the wire.
    private readonly Dictionary<LString, int> _writeIndex = new Dictionary<LString, int>();

    public override LString Parse(LuaByteBuffer buffer, BHeader header)
    {
        BInteger sizeT = header.sizeT.Parse(buffer, header);
        if (sizeT.AsInt() == 0)
        {
            BInteger idx = header.integer.Parse(buffer, header); // TODO: should be unsigned
            if (idx.Signum() == 0) return LString.NULL;
            return _saved[idx.AsInt() - 1];
        }
        b.Length = 0;
        sizeT.Iterate(() =>
        {
            b.Append((char)(0xFF & buffer.Get()));
        });
        char last = b[b.Length - 1];
        b.Length--;
        string s = b.ToString();
        if (header.debug)
        {
            Console.WriteLine("-- parsed <string> \"" + s + "\"");
        }
        LString result = new LString(s, last);
        _saved.Add(result);
        return result;
    }

    public override void Write(Stream @out, BHeader header, LString str)
    {
        if (ReferenceEquals(str, LString.NULL))
        {
            header.sizeT.Write(@out, header, header.sizeT.Create(0));
            header.integer.Write(@out, header, header.integer.Create(0));
            return;
        }
        if (_writeIndex.TryGetValue(str, out int idx))
        {
            // Already emitted once: reference by (index + 1). A zero index is
            // reserved for NULL, so stored indices start at 1 on the wire.
            header.sizeT.Write(@out, header, header.sizeT.Create(0));
            header.integer.Write(@out, header, header.integer.Create(idx + 1));
            return;
        }
        // First occurrence: emit sizeT (var-length) = value.Length + 1, then
        // the value bytes, then the trailing terminator byte captured by Parse
        // as LString.terminator.
        header.sizeT.Write(@out, header, header.sizeT.Create(str.value.Length + 1));
        for (int i = 0; i < str.value.Length; i++)
        {
            @out.WriteByte((byte)str.value[i]);
        }
        @out.WriteByte((byte)str.terminator);
        _writeIndex[str] = _writeIndex.Count;
    }
}
