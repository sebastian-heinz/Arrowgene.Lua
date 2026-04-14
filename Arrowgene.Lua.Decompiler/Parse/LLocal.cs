namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LLocal.</summary>
public sealed class LLocal : BObject
{
    public readonly LString name;
    public readonly int start;
    public readonly int end;

    /// <summary>Used by the decompiler for annotation.</summary>
    public bool forLoop = false;

    public LLocal(LString name, BInteger start, BInteger end)
    {
        this.name = name;
        this.start = start.AsInt();
        this.end = end.AsInt();
    }

    public override string ToString() => name.Deref();
}
