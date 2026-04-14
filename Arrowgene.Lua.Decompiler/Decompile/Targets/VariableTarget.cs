using System;

namespace Arrowgene.Lua.Decompiler.Decompile.Targets;

/// <summary>
/// Port of unluac.decompile.target.VariableTarget. An assignment destination
/// that resolves to a local declaration. Prints the declared name plus a
/// <c>&lt;close&gt;</c> annotation when the local is to-be-closed and being
/// declared (Lua 5.4+).
/// </summary>
public class VariableTarget : Target
{
    public readonly Declaration decl;

    public VariableTarget(Declaration decl)
    {
        this.decl = decl;
    }

    public override void Walk(Walker w) { }

    public override void Print(Decompiler d, Output @out, bool declare)
    {
        @out.Print(decl.name);
        if (declare && decl.tbc)
        {
            @out.Print(" <close>");
        }
    }

    public override void PrintMethod(Decompiler d, Output @out)
    {
        throw new InvalidOperationException();
    }

    public override bool IsDeclaration(Declaration decl) => this.decl == decl;

    public override bool IsLocal() => true;

    public override string GetLocalName() => decl.name;

    public override int GetIndex() => decl.register;

    public override bool Equals(object obj)
    {
        if (obj is VariableTarget t)
        {
            return decl == t.decl;
        }
        return false;
    }

    public override int GetHashCode() => decl?.GetHashCode() ?? 0;
}
