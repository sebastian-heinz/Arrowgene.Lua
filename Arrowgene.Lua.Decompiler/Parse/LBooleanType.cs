using System;
using System.IO;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LBooleanType.</summary>
public sealed class LBooleanType : BObjectType<LBoolean>
{
    public override LBoolean Parse(LuaByteBuffer buffer, BHeader header)
    {
        int value = buffer.Get();
        if (((uint)value & 0xFFFFFFFEu) != 0)
        {
            throw new InvalidOperationException();
        }
        LBoolean b = value == 0 ? LBoolean.LFALSE : LBoolean.LTRUE;
        if (header.debug)
        {
            Console.WriteLine("-- parsed <boolean> " + b);
        }
        return b;
    }

    public override void Write(Stream @out, BHeader header, LBoolean obj)
    {
        int value = obj.Value() ? 1 : 0;
        @out.WriteByte((byte)value);
    }
}
