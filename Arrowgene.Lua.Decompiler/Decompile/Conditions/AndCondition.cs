using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Conditions;

/// <summary>
/// Port of unluac.decompile.condition.AndCondition. Combines two
/// sub-conditions under short-circuit <c>and</c> semantics. Inverts
/// via De Morgan into an <see cref="OrCondition"/> when the right
/// operand is itself invertible; otherwise wraps in a
/// <see cref="NotCondition"/>.
/// </summary>
public class AndCondition : Condition
{
    private readonly Condition left;
    private readonly Condition right;

    public AndCondition(Condition left, Condition right)
    {
        this.left = left;
        this.right = right;
    }

    public Condition Inverse()
    {
        if (Invertible())
        {
            return new OrCondition(left.Inverse(), right.Inverse());
        }
        return new NotCondition(this);
    }

    public bool Invertible() => right.Invertible();

    public int Register() => right.Register();

    public bool IsRegisterTest() => false;

    public bool IsOrCondition() => false;

    public bool IsSplitable() => true;

    public Condition[] Split() => new Condition[] { left, right };

    public Expression AsExpression(Registers r)
    {
        return new BinaryExpression("and", left.AsExpression(r), right.AsExpression(r), Expression.PRECEDENCE_AND, Expression.ASSOCIATIVITY_NONE);
    }

    public override string ToString() => left + " and " + right;
}
