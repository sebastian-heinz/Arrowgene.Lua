using System;
using System.IO;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LUpvalueType with Lua 5.0 and 5.4 subclasses.</summary>
public abstract class LUpvalueType : BObjectType<LUpvalue>
{
    public static LUpvalueType Get(Version.UpvalueType type)
    {
        return type switch
        {
            Version.UpvalueType.LUA50 => new LUpvalueType50(),
            Version.UpvalueType.LUA54 => new LUpvalueType54(),
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>True when this upvalue format includes a <c>kind</c> byte (Lua 5.4+).</summary>
    public virtual bool HasKind => false;
}

internal sealed class LUpvalueType50 : LUpvalueType
{
    public override LUpvalue Parse(LuaByteBuffer buffer, BHeader header)
    {
        LUpvalue upvalue = new LUpvalue();
        upvalue.instack = buffer.Get() != 0;
        upvalue.idx = 0xFF & buffer.Get();
        return upvalue;
    }

    public override void Write(Stream @out, BHeader header, LUpvalue obj)
    {
        @out.WriteByte((byte)(obj.instack ? 1 : 0));
        @out.WriteByte((byte)obj.idx);
    }
}

internal sealed class LUpvalueType54 : LUpvalueType
{
    public override bool HasKind => true;

    public override LUpvalue Parse(LuaByteBuffer buffer, BHeader header)
    {
        LUpvalue upvalue = new LUpvalue();
        upvalue.instack = buffer.Get() != 0;
        upvalue.idx = 0xFF & buffer.Get();
        upvalue.kind = 0xFF & buffer.Get();
        return upvalue;
    }

    public override void Write(Stream @out, BHeader header, LUpvalue obj)
    {
        @out.WriteByte((byte)(obj.instack ? 1 : 0));
        @out.WriteByte((byte)obj.idx);
        @out.WriteByte((byte)obj.kind);
    }
}
