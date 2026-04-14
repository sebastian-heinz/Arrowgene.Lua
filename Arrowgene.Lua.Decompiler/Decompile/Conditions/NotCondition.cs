using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Conditions;

/// <summary>
/// Port of unluac.decompile.condition.NotCondition. Logical negation
/// of an arbitrary inner condition. Prints as <c>not (...)</c>.
/// Always invertible (its inverse is the original condition).
/// </summary>
public class NotCondition : Condition
{
    private readonly Condition cond;

    public NotCondition(Condition cond)
    {
        this.cond = cond;
    }

    public Condition Inverse() => cond;

    public bool Invertible() => true;

    public int Register() => cond.Register();

    public bool IsRegisterTest() => cond.IsRegisterTest();

    public bool IsOrCondition() => false;

    public bool IsSplitable() => false;

    public Condition[] Split() => throw new System.InvalidOperationException();

    public Expression AsExpression(Registers r)
    {
        return new UnaryExpression("not ", cond.AsExpression(r), Expression.PRECEDENCE_UNARY);
    }

    public override string ToString() => "not (" + cond + ")";
}
