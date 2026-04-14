using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Conditions;

/// <summary>
/// Port of unluac.decompile.condition.OrCondition. Combines two
/// sub-conditions under short-circuit <c>or</c> semantics. Unlike
/// <see cref="AndCondition"/>, an <c>or</c> is never splittable at
/// the decompiler level: the compiled branch flow merges at the
/// outer level.
/// </summary>
public class OrCondition : Condition
{
    private readonly Condition left;
    private readonly Condition right;

    public OrCondition(Condition left, Condition right)
    {
        this.left = left;
        this.right = right;
    }

    public Condition Inverse()
    {
        if (Invertible())
        {
            return new AndCondition(left.Inverse(), right.Inverse());
        }
        return new NotCondition(this);
    }

    public bool Invertible() => right.Invertible();

    public int Register() => right.Register();

    public bool IsRegisterTest() => false;

    public bool IsOrCondition() => true;

    public bool IsSplitable() => false;

    public Condition[] Split() => throw new System.InvalidOperationException();

    public Expression AsExpression(Registers r)
    {
        return new BinaryExpression("or", left.AsExpression(r), right.AsExpression(r), Expression.PRECEDENCE_OR, Expression.ASSOCIATIVITY_NONE);
    }

    public override string ToString() => "(" + left + " or " + right + ")";
}
