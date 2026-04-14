using System;
using System.IO;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LLocalType.</summary>
public sealed class LLocalType : BObjectType<LLocal>
{
    public override LLocal Parse(LuaByteBuffer buffer, BHeader header)
    {
        LString name = header.@string.Parse(buffer, header);
        BInteger start = header.integer.Parse(buffer, header);
        BInteger end = header.integer.Parse(buffer, header);
        if (header.debug)
        {
            Console.WriteLine("-- parsing local, name: " + name + " from " + start.AsInt() + " to " + end.AsInt());
        }
        return new LLocal(name, start, end);
    }

    public override void Write(Stream @out, BHeader header, LLocal obj)
    {
        header.@string.Write(@out, header, obj.name);
        header.integer.Write(@out, header, new BInteger(obj.start));
        header.integer.Write(@out, header, new BInteger(obj.end));
    }
}
