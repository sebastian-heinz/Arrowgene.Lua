namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.GlobalExpression. A reference to a
/// named global, carrying the underlying constant-pool name expression and
/// its constant index for declaration ordering.
/// </summary>
public class GlobalExpression : Expression
{
    private readonly ConstantExpression name;
    private readonly int index;

    public GlobalExpression(ConstantExpression name, int index)
        : base(PRECEDENCE_ATOMIC)
    {
        this.name = name;
        this.index = index;
    }

    public override void Walk(Walker w)
    {
        w.VisitExpression(this);
        name.Walk(w);
    }

    public override int GetConstantIndex() => index;

    public override bool IsDotChain() => true;

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print(name.AsName());
    }

    public override bool IsBrief() => true;
}
