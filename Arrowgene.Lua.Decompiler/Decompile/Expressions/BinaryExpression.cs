namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.BinaryExpression. Wraps a binary
/// operator (arithmetic, comparison, logical, concat) and its two operand
/// expressions, computing parenthesisation from precedence + associativity.
/// </summary>
public class BinaryExpression : Expression
{
    private readonly string op;
    private readonly Expression left;
    private readonly Expression right;
    private readonly int associativity;

    public static BinaryExpression ReplaceRight(BinaryExpression template, Expression replacement)
    {
        return new BinaryExpression(template.op, template.left, replacement, template.Precedence, template.associativity);
    }

    public BinaryExpression(string op, Expression left, Expression right, int precedence, int associativity)
        : base(precedence)
    {
        this.op = op;
        this.left = left;
        this.right = right;
        this.associativity = associativity;
    }

    public override void Walk(Walker w)
    {
        w.VisitExpression(this);
        left.Walk(w);
        right.Walk(w);
    }

    public override bool IsUngrouped() => !BeginsWithParen();

    public override int GetConstantIndex()
    {
        int l = left.GetConstantIndex();
        int r = right.GetConstantIndex();
        return l > r ? l : r;
    }

    public override bool BeginsWithParen() => LeftGroup() || left.BeginsWithParen();

    public override void Print(Decompiler d, Output @out)
    {
        bool leftGroup = LeftGroup();
        bool rightGroup = RightGroup();
        if (leftGroup) @out.Print("(");
        left.Print(d, @out);
        if (leftGroup) @out.Print(")");
        @out.Print(" ");
        @out.Print(op);
        @out.Print(" ");
        if (rightGroup) @out.Print("(");
        right.Print(d, @out);
        if (rightGroup) @out.Print(")");
    }

    public string GetOp() => op;

    private bool LeftGroup()
    {
        return Precedence > left.Precedence || (Precedence == left.Precedence && associativity == ASSOCIATIVITY_RIGHT);
    }

    private bool RightGroup()
    {
        return Precedence > right.Precedence || (Precedence == right.Precedence && associativity == ASSOCIATIVITY_LEFT);
    }
}
