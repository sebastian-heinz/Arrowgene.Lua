namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.UnaryExpression. Wraps a single-operand
/// operator (negate, not, length, bitwise-not). Parenthesises its operand if
/// the operand has lower precedence than the unary operator.
/// </summary>
public class UnaryExpression : Expression
{
    private readonly string op;
    private readonly Expression expression;

    public UnaryExpression(string op, Expression expression, int precedence)
        : base(precedence)
    {
        this.op = op;
        this.expression = expression;
    }

    public override void Walk(Walker w)
    {
        w.VisitExpression(this);
        expression.Walk(w);
    }

    public override bool IsUngrouped() => true;

    public override int GetConstantIndex() => expression.GetConstantIndex();

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print(op);
        if (Precedence > expression.Precedence) @out.Print("(");
        expression.Print(d, @out);
        if (Precedence > expression.Precedence) @out.Print(")");
    }
}
