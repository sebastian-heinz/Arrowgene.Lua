using System;

namespace Arrowgene.Lua.Decompiler.Decompile.Targets;

/// <summary>
/// Port of unluac.decompile.target.Target. Abstract base for assignment
/// destinations: locals, globals, upvalues, and table-field stores.
/// </summary>
/// <remarks>
/// The C# namespace is pluralised (<c>Targets</c>) to avoid colliding with
/// the <see cref="Target"/> class name itself, which Java permits but C#
/// does not.
/// </remarks>
public abstract class Target
{
    public abstract void Walk(Walker w);

    public abstract void Print(Decompiler d, Output @out, bool declare);

    public abstract void PrintMethod(Decompiler d, Output @out);

    public virtual bool IsDeclaration(Declaration decl) => false;

    public virtual bool IsLocal() => false;

    public virtual string GetLocalName()
    {
        throw new InvalidOperationException();
    }

    public virtual int GetIndex()
    {
        throw new InvalidOperationException();
    }

    public virtual bool IsFunctionName() => true;

    public virtual bool BeginsWithParen() => false;
}
