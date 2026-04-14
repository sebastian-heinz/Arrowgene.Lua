using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Conditions;

/// <summary>
/// Port of unluac.decompile.condition.TestCondition. A one-sided
/// test of a single register for truthiness, emitted for the
/// <c>TEST</c>/<c>TESTSET</c> opcodes. Not directly invertible;
/// its inverse is always wrapped in a <see cref="NotCondition"/>.
/// </summary>
public class TestCondition : Condition
{
    private readonly int line;
    private readonly int register;

    public TestCondition(int line, int register)
    {
        this.line = line;
        this.register = register;
    }

    public Condition Inverse() => new NotCondition(this);

    public bool Invertible() => false;

    public int Register() => register;

    public bool IsRegisterTest() => true;

    public bool IsOrCondition() => false;

    public bool IsSplitable() => false;

    public Condition[] Split() => throw new System.InvalidOperationException();

    public Expression AsExpression(Registers r) => r.GetExpression(register, line);

    public override string ToString() => "(" + register + ")";
}
