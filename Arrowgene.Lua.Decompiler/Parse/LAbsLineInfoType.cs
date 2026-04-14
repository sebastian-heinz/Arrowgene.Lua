using System.IO;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LAbsLineInfoType.</summary>
public sealed class LAbsLineInfoType : BObjectType<LAbsLineInfo>
{
    public override LAbsLineInfo Parse(LuaByteBuffer buffer, BHeader header)
    {
        int pc = header.vinteger.Parse(buffer, header).AsInt();
        int line = header.vinteger.Parse(buffer, header).AsInt();
        return new LAbsLineInfo(pc, line);
    }

    public override void Write(Stream @out, BHeader header, LAbsLineInfo obj)
    {
        header.vinteger.Write(@out, header, new BInteger(obj.pc));
        header.vinteger.Write(@out, header, new BInteger(obj.line));
    }
}
