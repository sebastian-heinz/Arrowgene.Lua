using System;
using System.Collections.Generic;
using System.IO;
using Arrowgene.Lua.Decompiler.Assemble;
using Arrowgene.Lua.Decompiler.Decompile;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.LFunctionType and its Lua-version subclasses
/// (50/51/52/53/54/55). Parses one compiled function (code, constants, upvalues,
/// locals, sub-functions, line info) and honours per-version quirks.
/// </summary>
public abstract class LFunctionType : BObjectType<LFunction>
{
    public static LFunctionType Get(Version.FunctionType type)
    {
        return type switch
        {
            Version.FunctionType.LUA50 => new LFunctionType50(),
            Version.FunctionType.LUA51 => new LFunctionType51(),
            Version.FunctionType.LUA52 => new LFunctionType52(),
            Version.FunctionType.LUA53 => new LFunctionType53(),
            Version.FunctionType.LUA54 => new LFunctionType54(),
            Version.FunctionType.LUA55 => new LFunctionType55(),
            _ => throw new InvalidOperationException(),
        };
    }

    protected sealed class LFunctionParseState
    {
        public LString name;
        public int lineBegin;
        public int lineEnd;
        public int lenUpvalues;
        public int lenParameter;
        public int vararg;
        public int maximumStackSize;
        public int length;
        public int[] code;
        public BList<LObject> constants;
        public BList<LFunction> functions;
        public BList<BInteger> lines;
        public BList<LAbsLineInfo> abslineinfo;
        public BList<LLocal> locals;
        public LUpvalue[] upvalues;
    }

    public override LFunction Parse(LuaByteBuffer buffer, BHeader header)
    {
        if (header.debug)
        {
            Console.WriteLine("-- beginning to parse function");
            Console.WriteLine("-- parsing name...start...end...upvalues...params...varargs...stack");
        }
        LFunctionParseState s = new LFunctionParseState();
        ParseMain(buffer, header, s);
        int[] lines = new int[s.lines.length.AsInt()];
        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = s.lines.Get(i).AsInt();
        }
        LAbsLineInfo[] abslineinfo = null;
        if (s.abslineinfo != null)
        {
            abslineinfo = s.abslineinfo.AsArray(new LAbsLineInfo[s.abslineinfo.length.AsInt()]);
        }
        LFunction lfunc = new LFunction(
            header,
            s.name,
            s.lineBegin,
            s.lineEnd,
            s.code,
            lines,
            abslineinfo,
            s.locals.AsArray(new LLocal[Math.Max(0, s.locals.length.AsInt())]),
            s.constants.AsArray(new LObject[Math.Max(0, s.constants.length.AsInt())]),
            s.upvalues,
            s.functions.AsArray(new LFunction[Math.Max(0, s.functions.length.AsInt())]),
            s.maximumStackSize,
            s.lenUpvalues,
            s.lenParameter,
            s.vararg);
        foreach (LFunction child in lfunc.functions)
        {
            child.parent = lfunc;
        }
        if (s.lines.length.AsInt() == 0 && s.locals.length.AsInt() == 0)
        {
            lfunc.stripped = true;
        }
        return lfunc;
    }

    public abstract List<Directive> GetDirectives();

    protected abstract void ParseMain(LuaByteBuffer buffer, BHeader header, LFunctionParseState s);

    /// <summary>
    /// Port of LFunctionType.align. Consumes alignment padding from the buffer
    /// so the next read lands on an m-byte boundary (upstream only uses 4).
    /// </summary>
    public static void Align(LuaByteBuffer buffer, int m)
    {
        int alignment = buffer.Position() % m;
        if (alignment > 0)
        {
            // TODO: allow non-zero alignment bytes?
            for (int i = 0; i < 4 - alignment; i++)
            {
                if (buffer.Get() != 0) throw new InvalidOperationException("Non-zero alignment byte");
            }
        }
    }

    protected void ParseCode(LuaByteBuffer buffer, BHeader header, LFunctionParseState s, bool align)
    {
        if (header.debug)
        {
            Console.WriteLine("-- beginning to parse bytecode list");
        }
        s.length = header.integer.Parse(buffer, header).AsInt();
        if (align) Align(buffer, 4);
        s.code = new int[s.length];
        for (int i = 0; i < s.length; i++)
        {
            s.code[i] = buffer.GetInt();
            if (header.debug)
            {
                int codepoint = s.code[i];
                CodeExtract ex = header.extractor;
                Op op = header.opmap.Get(ex.op.Extract(codepoint));
                Console.WriteLine("-- parsed codepoint " + codepoint.ToString("x"));
                if (op != null)
                {
                    Console.WriteLine("-- " + op.CodePointToString(0, (LFunction)null, codepoint, ex, (string)null, false));
                }
                else
                {
                    Console.WriteLine("-- " + Op.DefaultToString(0, (LFunction)null, codepoint, header.version, ex, false));
                }
            }
        }
    }

    protected void WriteCode(Stream @out, BHeader header, LFunction obj)
    {
        WriteCode(@out, header, obj, align: false);
    }

    protected void WriteCode(Stream @out, BHeader header, LFunction obj, bool align)
    {
        header.integer.Write(@out, header, new BInteger(obj.code.Length));
        if (align)
        {
            // Mirror ParseCode's Align(buffer, 4): pad zero bytes so the code
            // list starts on a 4-byte boundary of the output stream. Only used
            // by Lua 5.5.
            long pos = @out.Position;
            int alignment = (int)(pos % 4);
            if (alignment > 0)
            {
                for (int i = 0; i < 4 - alignment; i++)
                {
                    @out.WriteByte(0);
                }
            }
        }
        for (int i = 0; i < obj.code.Length; i++)
        {
            int codepoint = obj.code[i];
            if (header.lheader.endianness == LHeader.LEndianness.LITTLE)
            {
                @out.WriteByte((byte)(0xFF & codepoint));
                @out.WriteByte((byte)(0xFF & (codepoint >> 8)));
                @out.WriteByte((byte)(0xFF & (codepoint >> 16)));
                @out.WriteByte((byte)(0xFF & (codepoint >> 24)));
            }
            else
            {
                @out.WriteByte((byte)(0xFF & (codepoint >> 24)));
                @out.WriteByte((byte)(0xFF & (codepoint >> 16)));
                @out.WriteByte((byte)(0xFF & (codepoint >> 8)));
                @out.WriteByte((byte)(0xFF & codepoint));
            }
        }
    }

    protected void ParseConstants(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        if (header.debug)
        {
            Console.WriteLine("-- beginning to parse constants list");
        }
        s.constants = header.constant.ParseList(buffer, header);
        if (header.debug)
        {
            Console.WriteLine("-- beginning to parse functions list");
        }
        s.functions = header.function.ParseList(buffer, header);
    }

    protected void WriteConstants(Stream @out, BHeader header, LFunction obj)
    {
        header.constant.WriteList(@out, header, obj.constants);
        header.function.WriteList(@out, header, obj.functions);
    }

    protected void CreateUpvalues(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        s.upvalues = new LUpvalue[s.lenUpvalues];
        for (int i = 0; i < s.lenUpvalues; i++)
        {
            s.upvalues[i] = new LUpvalue();
        }
    }

    protected void ParseUpvalues(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        BList<LUpvalue> upvalues = header.upvalue.ParseList(buffer, header);
        s.lenUpvalues = upvalues.length.AsInt();
        s.upvalues = upvalues.AsArray(new LUpvalue[s.lenUpvalues]);
    }

    protected void WriteUpvalues(Stream @out, BHeader header, LFunction obj)
    {
        header.upvalue.WriteList(@out, header, obj.upvalues);
    }

    protected virtual void ParseDebug(LuaByteBuffer buffer, BHeader header, LFunctionParseState s, bool align)
    {
        if (header.debug)
        {
            Console.WriteLine("-- beginning to parse source lines list");
        }
        s.lines = header.integer.ParseList(buffer, header);
        if (header.debug)
        {
            Console.WriteLine("-- beginning to parse locals list");
        }
        s.locals = header.local.ParseList(buffer, header, header.version.locallengthmode.Get());
        ParseUpvalueNames(buffer, header, s);
    }

    protected void ParseUpvalueNames(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        if (header.debug)
        {
            Console.WriteLine("-- beginning to parse upvalue names list");
        }
        BList<LString> upvalueNames = header.@string.ParseList(
            buffer, header, header.version.upvaluelengthmode.Get(),
            new BInteger(s.lenUpvalues), false, 0);
        int limit = Math.Min(s.upvalues.Length, upvalueNames.length.AsInt());
        for (int i = 0; i < limit; i++)
        {
            s.upvalues[i].bname = upvalueNames.Get(i);
            s.upvalues[i].name = s.upvalues[i].bname.Deref();
        }
    }

    protected virtual void WriteDebug(Stream @out, BHeader header, LFunction obj)
    {
        header.integer.Write(@out, header, new BInteger(obj.lines.Length));
        for (int i = 0; i < obj.lines.Length; i++)
        {
            header.integer.Write(@out, header, new BInteger(obj.lines[i]));
        }
        header.local.WriteList(@out, header, obj.locals);
        int upvalueNameLength = 0;
        foreach (LUpvalue upvalue in obj.upvalues)
        {
            if (upvalue.bname != null && !ReferenceEquals(upvalue.bname, LString.NULL))
            {
                upvalueNameLength++;
            }
            else
            {
                break;
            }
        }
        header.integer.Write(@out, header, new BInteger(upvalueNameLength));
        for (int i = 0; i < upvalueNameLength; i++)
        {
            header.@string.Write(@out, header, obj.upvalues[i].bname);
        }
    }
}

internal class LFunctionType50 : LFunctionType
{
    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        s.name = header.@string.Parse(buffer, header);
        s.lineBegin = header.integer.Parse(buffer, header).AsInt();
        s.lineEnd = 0;
        s.lenUpvalues = 0xFF & buffer.Get();
        CreateUpvalues(buffer, header, s);
        s.lenParameter = 0xFF & buffer.Get();
        s.vararg = 0xFF & buffer.Get();
        s.maximumStackSize = 0xFF & buffer.Get();
        ParseDebug(buffer, header, s, false);
        ParseConstants(buffer, header, s);
        ParseCode(buffer, header, s, false);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.SOURCE,
            Directive.LINEDEFINED,
            Directive.NUMPARAMS,
            Directive.IS_VARARG,
            Directive.MAXSTACKSIZE,
        };
    }

    public override void Write(Stream @out, BHeader header, LFunction obj)
    {
        header.@string.Write(@out, header, obj.name);
        header.integer.Write(@out, header, new BInteger(obj.linedefined));
        @out.WriteByte((byte)obj.numUpvalues);
        @out.WriteByte((byte)obj.numParams);
        @out.WriteByte((byte)obj.vararg);
        @out.WriteByte((byte)obj.maximumStackSize);
        WriteDebug(@out, header, obj);
        WriteConstants(@out, header, obj);
        WriteCode(@out, header, obj);
    }
}

internal class LFunctionType51 : LFunctionType
{
    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        s.name = header.@string.Parse(buffer, header);
        s.lineBegin = header.integer.Parse(buffer, header).AsInt();
        s.lineEnd = header.integer.Parse(buffer, header).AsInt();
        s.lenUpvalues = 0xFF & buffer.Get();
        CreateUpvalues(buffer, header, s);
        s.lenParameter = 0xFF & buffer.Get();
        s.vararg = 0xFF & buffer.Get();
        s.maximumStackSize = 0xFF & buffer.Get();
        ParseCode(buffer, header, s, false);
        ParseConstants(buffer, header, s);
        ParseDebug(buffer, header, s, false);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.SOURCE,
            Directive.LINEDEFINED,
            Directive.LASTLINEDEFINED,
            Directive.NUMPARAMS,
            Directive.IS_VARARG,
            Directive.MAXSTACKSIZE,
        };
    }

    public override void Write(Stream @out, BHeader header, LFunction obj)
    {
        header.@string.Write(@out, header, obj.name);
        header.integer.Write(@out, header, new BInteger(obj.linedefined));
        header.integer.Write(@out, header, new BInteger(obj.lastlinedefined));
        @out.WriteByte((byte)obj.numUpvalues);
        @out.WriteByte((byte)obj.numParams);
        @out.WriteByte((byte)obj.vararg);
        @out.WriteByte((byte)obj.maximumStackSize);
        WriteCode(@out, header, obj);
        WriteConstants(@out, header, obj);
        WriteDebug(@out, header, obj);
    }
}

internal class LFunctionType52 : LFunctionType
{
    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        s.lineBegin = header.integer.Parse(buffer, header).AsInt();
        s.lineEnd = header.integer.Parse(buffer, header).AsInt();
        s.lenParameter = 0xFF & buffer.Get();
        s.vararg = 0xFF & buffer.Get();
        s.maximumStackSize = 0xFF & buffer.Get();
        ParseCode(buffer, header, s, false);
        ParseConstants(buffer, header, s);
        ParseUpvalues(buffer, header, s);
        s.name = header.@string.Parse(buffer, header);
        ParseDebug(buffer, header, s, false);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.LINEDEFINED,
            Directive.LASTLINEDEFINED,
            Directive.NUMPARAMS,
            Directive.IS_VARARG,
            Directive.MAXSTACKSIZE,
            Directive.SOURCE,
        };
    }

    public override void Write(Stream @out, BHeader header, LFunction obj)
    {
        header.integer.Write(@out, header, new BInteger(obj.linedefined));
        header.integer.Write(@out, header, new BInteger(obj.lastlinedefined));
        @out.WriteByte((byte)obj.numParams);
        @out.WriteByte((byte)obj.vararg);
        @out.WriteByte((byte)obj.maximumStackSize);
        WriteCode(@out, header, obj);
        WriteConstants(@out, header, obj);
        WriteUpvalues(@out, header, obj);
        header.@string.Write(@out, header, obj.name);
        WriteDebug(@out, header, obj);
    }
}

internal class LFunctionType53 : LFunctionType
{
    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        s.name = header.@string.Parse(buffer, header); // TODO: psource
        s.lineBegin = header.integer.Parse(buffer, header).AsInt();
        s.lineEnd = header.integer.Parse(buffer, header).AsInt();
        s.lenParameter = 0xFF & buffer.Get();
        s.vararg = 0xFF & buffer.Get();
        s.maximumStackSize = 0xFF & buffer.Get();
        ParseCode(buffer, header, s, false);
        s.constants = header.constant.ParseList(buffer, header);
        ParseUpvalues(buffer, header, s);
        s.functions = header.function.ParseList(buffer, header);
        ParseDebug(buffer, header, s, false);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.SOURCE,
            Directive.LINEDEFINED,
            Directive.LASTLINEDEFINED,
            Directive.NUMPARAMS,
            Directive.IS_VARARG,
            Directive.MAXSTACKSIZE,
        };
    }

    public override void Write(Stream @out, BHeader header, LFunction obj)
    {
        header.@string.Write(@out, header, obj.name);
        header.integer.Write(@out, header, new BInteger(obj.linedefined));
        header.integer.Write(@out, header, new BInteger(obj.lastlinedefined));
        @out.WriteByte((byte)obj.numParams);
        @out.WriteByte((byte)obj.vararg);
        @out.WriteByte((byte)obj.maximumStackSize);
        WriteCode(@out, header, obj);
        header.constant.WriteList(@out, header, obj.constants);
        WriteUpvalues(@out, header, obj);
        header.function.WriteList(@out, header, obj.functions);
        WriteDebug(@out, header, obj);
    }
}

internal class LFunctionType54 : LFunctionType
{
    protected override void ParseDebug(LuaByteBuffer buffer, BHeader header, LFunctionParseState s, bool align)
    {
        // TODO: process line info correctly
        // TODO: support other alignments
        s.lines = new BIntegerType50(false, 1, false).ParseList(buffer, header);
        s.abslineinfo = header.abslineinfo.ParseListAlign(buffer, header, align, 4);
        s.locals = header.local.ParseList(buffer, header);
        ParseUpvalueNames(buffer, header, s);
    }

    protected override void WriteDebug(Stream @out, BHeader header, LFunction obj)
    {
        header.integer.Write(@out, header, new BInteger(obj.lines.Length));
        for (int i = 0; i < obj.lines.Length; i++)
        {
            @out.WriteByte((byte)obj.lines[i]);
        }
        header.abslineinfo.WriteList(@out, header, obj.abslineinfo);
        header.local.WriteList(@out, header, obj.locals);
        int upvalueNameLength = 0;
        foreach (LUpvalue upvalue in obj.upvalues)
        {
            if (upvalue.bname != null && !ReferenceEquals(upvalue.bname, LString.NULL))
            {
                upvalueNameLength++;
            }
            else
            {
                break;
            }
        }
        header.integer.Write(@out, header, new BInteger(upvalueNameLength));
        for (int i = 0; i < upvalueNameLength; i++)
        {
            header.@string.Write(@out, header, obj.upvalues[i].bname);
        }
    }

    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        s.name = header.@string.Parse(buffer, header);
        s.lineBegin = header.integer.Parse(buffer, header).AsInt();
        s.lineEnd = header.integer.Parse(buffer, header).AsInt();
        s.lenParameter = 0xFF & buffer.Get();
        s.vararg = 0xFF & buffer.Get();
        s.maximumStackSize = 0xFF & buffer.Get();
        ParseCode(buffer, header, s, false);
        s.constants = header.constant.ParseList(buffer, header);
        ParseUpvalues(buffer, header, s);
        s.functions = header.function.ParseList(buffer, header);
        ParseDebug(buffer, header, s, false);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.SOURCE,
            Directive.LINEDEFINED,
            Directive.LASTLINEDEFINED,
            Directive.NUMPARAMS,
            Directive.IS_VARARG,
            Directive.MAXSTACKSIZE,
        };
    }

    public override void Write(Stream @out, BHeader header, LFunction obj)
    {
        header.@string.Write(@out, header, obj.name);
        header.integer.Write(@out, header, new BInteger(obj.linedefined));
        header.integer.Write(@out, header, new BInteger(obj.lastlinedefined));
        @out.WriteByte((byte)obj.numParams);
        @out.WriteByte((byte)obj.vararg);
        @out.WriteByte((byte)obj.maximumStackSize);
        WriteCode(@out, header, obj);
        header.constant.WriteList(@out, header, obj.constants);
        WriteUpvalues(@out, header, obj);
        header.function.WriteList(@out, header, obj.functions);
        WriteDebug(@out, header, obj);
    }
}

internal sealed class LFunctionType55 : LFunctionType54
{
    protected override void WriteDebug(Stream @out, BHeader header, LFunction obj)
    {
        header.integer.Write(@out, header, new BInteger(obj.lines.Length));
        for (int i = 0; i < obj.lines.Length; i++)
        {
            @out.WriteByte((byte)obj.lines[i]);
        }
        // Lua 5.5 pads to a 4-byte boundary before a non-empty abslineinfo
        // list; mirror ParseListAlign in ParseDebug.
        header.abslineinfo.WriteListAlign(@out, header, obj.abslineinfo, true, 4);
        header.local.WriteList(@out, header, obj.locals);
        int upvalueNameLength = 0;
        foreach (LUpvalue upvalue in obj.upvalues)
        {
            if (upvalue.bname != null && !ReferenceEquals(upvalue.bname, LString.NULL))
            {
                upvalueNameLength++;
            }
            else
            {
                break;
            }
        }
        header.integer.Write(@out, header, new BInteger(upvalueNameLength));
        for (int i = 0; i < upvalueNameLength; i++)
        {
            header.@string.Write(@out, header, obj.upvalues[i].bname);
        }
    }

    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LFunctionParseState s)
    {
        s.lineBegin = header.integer.Parse(buffer, header).AsInt();
        s.lineEnd = header.integer.Parse(buffer, header).AsInt();
        s.lenParameter = 0xFF & buffer.Get();
        s.vararg = 0xFF & buffer.Get();
        s.maximumStackSize = 0xFF & buffer.Get();
        ParseCode(buffer, header, s, true);
        s.constants = header.constant.ParseList(buffer, header);
        ParseUpvalues(buffer, header, s);
        s.functions = header.function.ParseList(buffer, header);
        s.name = header.@string.Parse(buffer, header);
        ParseDebug(buffer, header, s, true);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.SOURCE,
            Directive.LINEDEFINED,
            Directive.LASTLINEDEFINED,
            Directive.NUMPARAMS,
            Directive.IS_VARARG,
            Directive.MAXSTACKSIZE,
        };
    }

    public override void Write(Stream @out, BHeader header, LFunction obj)
    {
        header.integer.Write(@out, header, new BInteger(obj.linedefined));
        header.integer.Write(@out, header, new BInteger(obj.lastlinedefined));
        @out.WriteByte((byte)obj.numParams);
        @out.WriteByte((byte)obj.vararg);
        @out.WriteByte((byte)obj.maximumStackSize);
        WriteCode(@out, header, obj, align: true);
        header.constant.WriteList(@out, header, obj.constants);
        WriteUpvalues(@out, header, obj);
        header.function.WriteList(@out, header, obj.functions);
        header.@string.Write(@out, header, obj.name);
        WriteDebug(@out, header, obj);
    }
}
