namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.UpvalueExpression. A reference to a
/// captured upvalue by name.
/// </summary>
public class UpvalueExpression : Expression
{
    private readonly string name;

    public UpvalueExpression(string name)
        : base(PRECEDENCE_ATOMIC)
    {
        this.name = name;
    }

    public override void Walk(Walker w) => w.VisitExpression(this);

    public override int GetConstantIndex() => -1;

    public override bool IsDotChain() => true;

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print(name);
    }

    public override bool IsBrief() => true;

    public override bool IsEnvironmentTable(Decompiler d)
    {
        return d.GetVersion().IsEnvironmentTable(name);
    }
}
