using System;
using System.Collections.Generic;
using System.IO;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.BObjectType.
/// </summary>
public abstract class BObjectType<T> where T : BObject
{
    public abstract T Parse(LuaByteBuffer buffer, BHeader header);

    public abstract void Write(Stream @out, BHeader header, T obj);

    public BList<T> ParseList(LuaByteBuffer buffer, BHeader header)
    {
        return ParseList(buffer, header, Version.ListLengthMode.STRICT, null, false, 0);
    }

    public BList<T> ParseListAlign(LuaByteBuffer buffer, BHeader header, bool align, int alignment)
    {
        return ParseList(buffer, header, Version.ListLengthMode.STRICT, null, align, alignment);
    }

    public BList<T> ParseList(LuaByteBuffer buffer, BHeader header, Version.ListLengthMode mode)
    {
        return ParseList(buffer, header, mode, null, false, 0);
    }

    public BList<T> ParseList(LuaByteBuffer buffer, BHeader header, Version.ListLengthMode mode,
                              BInteger knownLength, bool align, int alignment)
    {
        BInteger length = header.integer.Parse(buffer, header);
        if (align && length.Signum() > 0) LFunctionType.Align(buffer, alignment);
        switch (mode)
        {
            case Version.ListLengthMode.STRICT:
                break;
            case Version.ListLengthMode.ALLOW_NEGATIVE:
                if (length.Signum() < 0) length = new BInteger(0);
                break;
            case Version.ListLengthMode.IGNORE:
                if (knownLength == null) throw new InvalidOperationException();
                if (length.Signum() != 0) length = knownLength;
                break;
        }
        return ParseList(buffer, header, length);
    }

    public BList<T> ParseList(LuaByteBuffer buffer, BHeader header, BInteger length)
    {
        List<T> values = new List<T>();
        length.Iterate(() =>
        {
            values.Add(Parse(buffer, header));
        });
        return new BList<T>(length, values);
    }

    public void WriteList(Stream @out, BHeader header, T[] array)
    {
        header.integer.Write(@out, header, new BInteger(array.Length));
        foreach (T obj in array)
        {
            Write(@out, header, obj);
        }
    }

    /// <summary>
    /// Mirrors <see cref="ParseListAlign"/>: after writing the length, if the
    /// list is non-empty and <paramref name="align"/> is true, pad zero bytes
    /// to the next <paramref name="alignment"/>-byte boundary in the output
    /// stream. Used by Lua 5.5 to align the <c>abslineinfo</c> list.
    /// </summary>
    public void WriteListAlign(Stream @out, BHeader header, T[] array, bool align, int alignment)
    {
        header.integer.Write(@out, header, new BInteger(array.Length));
        if (align && array.Length > 0)
        {
            long pos = @out.Position;
            int mod = (int)(pos % alignment);
            if (mod > 0)
            {
                for (int i = 0; i < alignment - mod; i++)
                {
                    @out.WriteByte(0);
                }
            }
        }
        foreach (T obj in array)
        {
            Write(@out, header, obj);
        }
    }

    public void WriteList(Stream @out, BHeader header, BList<T> blist)
    {
        header.integer.Write(@out, header, blist.length);
        IEnumerator<T> it = blist.GetEnumerator();
        while (it.MoveNext())
        {
            Write(@out, header, it.Current);
        }
    }
}
