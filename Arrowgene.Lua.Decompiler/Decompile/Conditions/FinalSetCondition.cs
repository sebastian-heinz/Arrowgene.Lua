using System;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Conditions;

/// <summary>
/// Port of unluac.decompile.condition.FinalSetCondition. Represents
/// the final-value reach of a conditional assignment chain: the
/// register written in the fall-through branch of a sequence of
/// TESTSET/TEST jumps. Its <see cref="Type"/> classifies how the
/// final value should be materialised (raw register, tracked value,
/// or a placeholder for later resolution).
/// </summary>
public class FinalSetCondition : Condition
{
    public enum Type
    {
        NONE,
        REGISTER,
        VALUE,
    }

    public int line;
    private readonly int register;
    public Type type;

    public FinalSetCondition(int line, int register)
    {
        this.line = line;
        this.register = register;
        type = Type.NONE;
        if (register < 0)
        {
            throw new InvalidOperationException();
        }
    }

    public Condition Inverse() => new NotCondition(this);

    public bool Invertible() => false;

    public int Register() => register;

    public bool IsRegisterTest() => false;

    public bool IsOrCondition() => false;

    public bool IsSplitable() => false;

    public Condition[] Split() => throw new InvalidOperationException();

    public Expression AsExpression(Registers r)
    {
        Expression expr;
        switch (type)
        {
            case Type.REGISTER:
                expr = r.GetExpression(register, line + 1);
                break;
            case Type.VALUE:
                expr = r.GetValue(register, line + 1);
                break;
            case Type.NONE:
            default:
                expr = ConstantExpression.CreateDouble(register + ((double)line) / 100.0);
                break;
        }
        if (expr == null)
        {
            throw new InvalidOperationException();
        }
        return expr;
    }

    public override string ToString() => "(" + register + ")";
}
