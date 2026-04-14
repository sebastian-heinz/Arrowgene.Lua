namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LUpvalue.</summary>
public sealed class LUpvalue : BObject
{
    public bool instack;
    public int idx;

    public string name;
    public LString bname;
    /// <summary>Upvalue kind byte (Lua 5.4+). -1 means not present (Lua 5.0–5.3).</summary>
    public int kind = -1;

    public override bool Equals(object obj)
    {
        if (obj is not LUpvalue upvalue) return false;
        if (!(instack == upvalue.instack && idx == upvalue.idx && kind == upvalue.kind))
        {
            return false;
        }
        if (name == upvalue.name)
        {
            return true;
        }
        return name != null && name.Equals(upvalue.name);
    }

    public override int GetHashCode() => (name?.GetHashCode() ?? 0) ^ idx ^ kind;
}
