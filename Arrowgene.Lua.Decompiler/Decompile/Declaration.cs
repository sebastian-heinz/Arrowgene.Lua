using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Declaration. Tracks a single local variable's
/// name, lifetime (begin/end pcs), allocated register, to-be-closed flag,
/// and a few scope-classification booleans used by the for-loop and
/// named-vararg decoders.
/// </summary>
public class Declaration
{
    public readonly string name;
    public readonly int begin;
    public readonly int end;
    public int register;
    public bool tbc;

    /// <summary>
    /// Whether this is an invisible for-loop book-keeping variable.
    /// </summary>
    public bool forLoop = false;

    /// <summary>
    /// Whether this is an explicit for-loop declared variable.
    /// </summary>
    public bool forLoopExplicit = false;

    public bool namedVararg = false;

    public Declaration(LLocal local, Code code)
    {
        int adjust = 0;
        if (local.start >= 1)
        {
            Op op = code.Op(local.start);
            if (op == Op.MMBIN || op == Op.MMBINI || op == Op.MMBINK || op == Op.EXTRAARG)
            {
                adjust--;
            }
        }
        name = local.ToString();
        begin = local.start + adjust;
        end = local.end;
        tbc = false;
    }

    public Declaration(string name, int begin, int end)
    {
        this.name = name;
        this.begin = begin;
        this.end = end;
    }

    public bool IsInternalName() => !string.IsNullOrEmpty(name) && name[0] == '(';

    public bool IsSplitBy(int line, int begin, int end)
    {
        int scopeEnd = end - 1;
        if (begin == end) begin = begin - 1;
        return this.begin >= line && this.begin < begin
            || this.end >= line && this.end < begin
            || this.begin < begin && this.end >= begin && this.end < scopeEnd
            || this.begin >= begin && this.begin <= scopeEnd && this.end > scopeEnd;
    }
}
