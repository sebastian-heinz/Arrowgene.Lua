using System;

namespace Arrowgene.Lua.Decompiler.Decompile.Targets;

/// <summary>
/// Port of unluac.decompile.target.UpvalueTarget. An assignment destination
/// that resolves to a captured upvalue, identified by its declared name.
/// </summary>
public class UpvalueTarget : Target
{
    private readonly string name;

    public UpvalueTarget(string name)
    {
        this.name = name;
    }

    public override void Walk(Walker w) { }

    public override void Print(Decompiler d, Output @out, bool declare)
    {
        @out.Print(name);
    }

    public override void PrintMethod(Decompiler d, Output @out)
    {
        throw new InvalidOperationException();
    }
}
