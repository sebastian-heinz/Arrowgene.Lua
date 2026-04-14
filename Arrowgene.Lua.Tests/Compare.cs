using Arrowgene.Lua.Decompiler;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Tests;

/// <summary>
/// Port of unluac.test.Compare — deep structural comparison of two
/// compiled Lua functions (LFunction trees). Line numbers are excluded
/// in NORMAL mode; FULL mode checks everything including debug info.
/// </summary>
public class Compare
{
    public enum Mode
    {
        NORMAL,
        FULL,
    }

    private readonly Mode _mode;

    public Compare(Mode mode)
    {
        _mode = mode;
    }

    /// <summary>
    /// Determines if two files of Lua bytecode are structurally equal
    /// (except possibly for line numbers in NORMAL mode).
    /// </summary>
    public bool BytecodeEqual(string file1, string file2)
    {
        LFunction? main1 = FileToFunction(file1);
        LFunction? main2 = FileToFunction(file2);
        if (main1 == null || main2 == null) return false;
        return FunctionEqual(main1, main2);
    }

    public bool FunctionEqual(LFunction f1, LFunction f2)
    {
        if (f1.maximumStackSize != f2.maximumStackSize) return false;
        if (f1.numParams != f2.numParams) return false;
        if (f1.numUpvalues != f2.numUpvalues) return false;
        if (f1.vararg != f2.vararg) return false;

        if (f1.code.Length != f2.code.Length) return false;
        for (int i = 0; i < f1.code.Length; i++)
        {
            if (f1.code[i] != f2.code[i]) return false;
        }

        if (f1.constants.Length != f2.constants.Length) return false;
        for (int i = 0; i < f1.constants.Length; i++)
        {
            if (!ObjectEqual(f1.constants[i], f2.constants[i])) return false;
        }

        if (f1.locals.Length != f2.locals.Length) return false;
        for (int i = 0; i < f1.locals.Length; i++)
        {
            if (!LocalEqual(f1.locals[i], f2.locals[i])) return false;
        }

        if (f1.upvalues.Length != f2.upvalues.Length) return false;
        for (int i = 0; i < f1.upvalues.Length; i++)
        {
            if (!f1.upvalues[i].Equals(f2.upvalues[i])) return false;
        }

        if (f1.functions.Length != f2.functions.Length) return false;
        for (int i = 0; i < f1.functions.Length; i++)
        {
            if (!FunctionEqual(f1.functions[i], f2.functions[i])) return false;
        }

        if (_mode == Mode.FULL)
        {
            if (!f1.name.Equals(f2.name)) return false;
            if (f1.linedefined != f2.linedefined) return false;
            if (f1.lastlinedefined != f2.lastlinedefined) return false;

            if (f1.lines.Length != f2.lines.Length) return false;
            for (int i = 0; i < f1.lines.Length; i++)
            {
                if (f1.lines[i] != f2.lines[i]) return false;
            }

            if ((f1.abslineinfo == null) != (f2.abslineinfo == null)) return false;
            if (f1.abslineinfo != null)
            {
                if (f1.abslineinfo.Length != f2.abslineinfo.Length) return false;
                for (int i = 0; i < f1.abslineinfo.Length; i++)
                {
                    if (!f1.abslineinfo[i].Equals(f2.abslineinfo[i])) return false;
                }
            }
        }

        return true;
    }

    public bool ObjectEqual(LObject o1, LObject o2)
    {
        return o1.Equals(o2);
    }

    public bool LocalEqual(LLocal l1, LLocal l2)
    {
        if (l1.start != l2.start) return false;
        if (l1.end != l2.end) return false;
        if (!l1.name.Equals(l2.name)) return false;
        return true;
    }

    public static LFunction? FileToFunction(string filename)
    {
        try
        {
            using FileStream file = new FileStream(filename, FileMode.Open, FileAccess.Read);
            LuaByteBuffer buffer = LuaByteBuffer.ReadAll(file);
            buffer.Order(LuaByteBuffer.LITTLE_ENDIAN);
            buffer.Position(0);
            BHeader header = new BHeader(buffer, new Configuration());
            return header.main;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
