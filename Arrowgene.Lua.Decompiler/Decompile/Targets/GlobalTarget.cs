using System;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Targets;

/// <summary>
/// Port of unluac.decompile.target.GlobalTarget. An assignment destination
/// that resolves to a named global, carrying the constant-pool name
/// expression for its identifier.
/// </summary>
public class GlobalTarget : Target
{
    private readonly Expression name;

    public GlobalTarget(ConstantExpression name)
    {
        this.name = name;
    }

    public override void Walk(Walker w)
    {
        name.Walk(w);
    }

    public override void Print(Decompiler d, Output @out, bool declare)
    {
        @out.Print(name.AsName());
    }

    public override void PrintMethod(Decompiler d, Output @out)
    {
        throw new InvalidOperationException();
    }
}
