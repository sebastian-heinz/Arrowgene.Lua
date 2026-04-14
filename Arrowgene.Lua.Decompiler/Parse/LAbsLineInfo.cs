namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LAbsLineInfo.</summary>
public sealed class LAbsLineInfo : LObject
{
    public readonly int pc;
    public readonly int line;

    public LAbsLineInfo(int pc, int line)
    {
        this.pc = pc;
        this.line = line;
    }

    public override bool Equals(object o)
    {
        if (o is LAbsLineInfo other)
        {
            return pc == other.pc && line == other.line;
        }
        return false;
    }

    public override int GetHashCode() => pc ^ line;
}
