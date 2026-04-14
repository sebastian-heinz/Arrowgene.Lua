namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.AssemblerUpvalue. Pending entry from the
/// <c>.upvalue</c> directive describing one upvalue slot of a function:
/// debug name, source index in the parent function's stack or upvalue
/// table, and the in-stack flag distinguishing the two cases.
/// </summary>
internal sealed class AssemblerUpvalue
{
    public string name;
    public int index;
    public bool instack;
    // Lua 5.4+ upvalue kind (VDKREG=0, RDKCONST=1, RDKTOCLOSE=2, RDKCTC=3).
    // Encoded only by the Lua 5.4 upvalue writer; earlier versions ignore it.
    public int kind;
}
