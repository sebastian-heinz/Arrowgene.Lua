namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.LocalVariable. A reference to a local
/// declaration, printed using the declared name.
/// </summary>
public class LocalVariable : Expression
{
    private readonly Declaration decl;

    public LocalVariable(Declaration decl)
        : base(PRECEDENCE_ATOMIC)
    {
        this.decl = decl;
    }

    public override void Walk(Walker w) => w.VisitExpression(this);

    public override int GetConstantIndex() => -1;

    public override bool IsDotChain() => true;

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print(decl.name);
    }

    public override bool IsBrief() => true;

    public override bool IsEnvironmentTable(Decompiler d)
    {
        return d.GetVersion().IsEnvironmentTable(decl.name);
    }
}
