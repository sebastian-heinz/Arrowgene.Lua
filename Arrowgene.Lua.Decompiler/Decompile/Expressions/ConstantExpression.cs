using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.ConstantExpression. A reference to a
/// constant pool entry (or a synthesised inline literal). Carries the wrapped
/// <see cref="Constant"/>, the constant pool index for declaration ordering,
/// the source line for line-info-driven splits, and a precomputed
/// <c>identifier</c> flag that lets the decompiler skip
/// <see cref="Constant.IsIdentifier"/> at print time.
/// </summary>
public class ConstantExpression : Expression
{
    private readonly Constant constant;
    private readonly bool identifier;
    private readonly int index;
    private readonly int line;

    public static ConstantExpression CreateNil(int line)
    {
        return new ConstantExpression(new Constant(LNil.NIL), false, -1, line);
    }

    public static ConstantExpression CreateBoolean(bool v)
    {
        return new ConstantExpression(new Constant(v ? LBoolean.LTRUE : LBoolean.LFALSE), false, -1);
    }

    public static ConstantExpression CreateInteger(int i)
    {
        return new ConstantExpression(new Constant(i), false, -1);
    }

    public static ConstantExpression CreateDouble(double x)
    {
        return new ConstantExpression(new Constant(x), false, -1);
    }

    private static int GetPrecedence(Constant constant)
    {
        if (constant.IsNumber() && constant.IsNegative())
        {
            return PRECEDENCE_UNARY;
        }
        return PRECEDENCE_ATOMIC;
    }

    public ConstantExpression(Constant constant, bool identifier, int index)
        : this(constant, identifier, index, -1)
    {
    }

    private ConstantExpression(Constant constant, bool identifier, int index, int line)
        : base(GetPrecedence(constant))
    {
        this.constant = constant;
        this.identifier = identifier;
        this.index = index;
        this.line = line;
    }

    public override void Walk(Walker w) => w.VisitExpression(this);

    public override int GetConstantIndex() => index;

    public override int GetConstantLine() => line;

    public override void Print(Decompiler d, Output @out)
    {
        constant.Print(d, @out, false);
    }

    public override void PrintBraced(Decompiler d, Output @out)
    {
        constant.Print(d, @out, true);
    }

    public override bool IsConstant() => true;

    public override bool IsUngrouped() => true;

    public override bool IsNil() => constant.IsNil();

    public override bool IsBoolean() => constant.IsBoolean();

    public override bool IsInteger() => constant.IsInteger();

    public override int AsInteger() => constant.AsInteger();

    public override bool IsString() => constant.IsString();

    public override bool IsIdentifier() => identifier;

    public override string AsName() => constant.AsName();

    public override bool IsBrief() => !constant.IsString() || constant.AsName().Length <= 10;
}
