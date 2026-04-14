using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Conditions;

/// <summary>
/// Port of unluac.decompile.condition.ConstantCondition. A
/// compile-time boolean test with a remembered register: the
/// register identifies the source slot for later assignment
/// coalescing, while <c>AsExpression</c> always materialises the
/// fixed boolean value. Invertible by flipping the value.
/// </summary>
public class ConstantCondition : Condition
{
    private readonly int register;
    private readonly bool value;

    public ConstantCondition(int register, bool value)
    {
        this.register = register;
        this.value = value;
    }

    public Condition Inverse() => new ConstantCondition(register, !value);

    public bool Invertible() => true;

    public int Register() => register;

    public bool IsRegisterTest() => false;

    public bool IsOrCondition() => false;

    public bool IsSplitable() => false;

    public Condition[] Split() => throw new System.InvalidOperationException();

    public Expression AsExpression(Registers r) => ConstantExpression.CreateBoolean(value);
}
