namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.LFunction. A compiled Lua function: bytecode, constants,
/// locals, upvalues, nested functions, debug line info, and the metadata needed
/// to decompile/disassemble it.
/// </summary>
public sealed class LFunction : BObject
{
    public BHeader header;
    public LString name;
    public int linedefined;
    public int lastlinedefined;
    public LFunction parent;
    public int[] code;
    public int[] lines;
    public LAbsLineInfo[] abslineinfo;
    public LLocal[] locals;
    public LObject[] constants;
    public LUpvalue[] upvalues;
    public LFunction[] functions;
    public int maximumStackSize;
    public int numUpvalues;
    public int numParams;
    public int vararg;
    public bool stripped;
    public int level;

    public LFunction(BHeader header, LString name, int linedefined, int lastlinedefined,
                     int[] code, int[] lines, LAbsLineInfo[] abslineinfo, LLocal[] locals,
                     LObject[] constants, LUpvalue[] upvalues, LFunction[] functions,
                     int maximumStackSize, int numUpValues, int numParams, int vararg)
    {
        this.header = header;
        this.name = name;
        this.linedefined = linedefined;
        this.lastlinedefined = lastlinedefined;
        this.code = code;
        this.lines = lines;
        this.abslineinfo = abslineinfo;
        this.locals = locals;
        this.constants = constants;
        this.upvalues = upvalues;
        this.functions = functions;
        this.maximumStackSize = maximumStackSize;
        this.numUpvalues = numUpValues;
        this.numParams = numParams;
        this.vararg = vararg;
        this.stripped = false;
    }

    // Parameterless constructor used by placeholder construction paths during porting.
    internal LFunction() { }

    public void SetLevel(int level)
    {
        this.level = level;
        if (functions == null) return;
        foreach (LFunction f in functions)
        {
            f?.SetLevel(level + 1);
        }
    }
}
