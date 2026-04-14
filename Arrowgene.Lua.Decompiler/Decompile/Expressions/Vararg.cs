namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.Vararg. The "..." literal. The
/// constructor's <c>length</c> argument is intentionally unused (it matches
/// upstream, which discards it as well); only the <c>multiple</c> flag is
/// retained, since a vararg adjusted to a single value must be parenthesised.
/// </summary>
public class Vararg : Expression
{
    private readonly bool multiple;

    public Vararg(int length, bool multiple)
        : base(PRECEDENCE_ATOMIC)
    {
        this.multiple = multiple;
    }

    public override void Walk(Walker w) => w.VisitExpression(this);

    public override int GetConstantIndex() => -1;

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print(multiple ? "..." : "(...)");
    }

    public override void PrintMultiple(Decompiler d, Output @out)
    {
        @out.Print(multiple ? "..." : "(...)");
    }

    public override bool IsMultiple() => multiple;
}
