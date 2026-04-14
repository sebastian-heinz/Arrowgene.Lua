using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Conditions;

/// <summary>
/// Port of unluac.decompile.condition.FixedCondition. A condition
/// that always materialises to a fixed <see cref="Expression"/>
/// regardless of the register state. Used for synthetic TRUE
/// tests in the analyser's control-flow plumbing; cannot be
/// inverted and always reports -1 as its register.
/// </summary>
public class FixedCondition : Condition
{
    public static readonly FixedCondition TRUE = new FixedCondition(ConstantExpression.CreateBoolean(true));

    private readonly Expression expression;

    private FixedCondition(Expression expr)
    {
        expression = expr;
    }

    public Condition Inverse() => throw new System.InvalidOperationException();

    public bool Invertible() => false;

    public int Register() => -1;

    public bool IsRegisterTest() => false;

    public bool IsOrCondition() => false;

    public bool IsSplitable() => false;

    public Condition[] Split() => throw new System.InvalidOperationException();

    public Expression AsExpression(Registers r) => expression;
}
