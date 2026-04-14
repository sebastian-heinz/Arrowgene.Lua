using System;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Conditions;

/// <summary>
/// Port of unluac.decompile.condition.Condition. Represents the
/// predicate driving a conditional jump. The concrete subclasses
/// (BinaryCondition, TestCondition, AndCondition, OrCondition,
/// NotCondition, FinalSetCondition, FixedCondition, ConstantCondition)
/// land in follow-up commits; this commit exposes the interface so
/// block subclasses can reference it in fields and signatures.
/// </summary>
public interface Condition
{
    Condition Inverse();

    bool Invertible();

    int Register();

    bool IsRegisterTest();

    bool IsOrCondition();

    bool IsSplitable();

    Condition[] Split();

    Expression AsExpression(Registers r);

    string ToString();
}

/// <summary>
/// Port of unluac.decompile.condition.Condition.OperandType. Shared
/// operand classifier for register / RK constant / K constant / raw
/// immediate / raw float operand forms used across the condition
/// subpackage.
/// </summary>
public enum OperandType
{
    R,
    RK,
    K,
    I,
    F,
}

/// <summary>
/// Port of unluac.decompile.condition.Condition.Operand. A tagged
/// operand that can resolve itself to an <see cref="Expression"/>
/// once bound to a <see cref="Registers"/> context and line.
/// </summary>
public sealed class Operand
{
    public readonly OperandType type;
    public readonly int value;

    public Operand(OperandType type, int value)
    {
        this.type = type;
        this.value = value;
    }

    public Expression AsExpression(Registers r, int line)
    {
        switch (type)
        {
            case OperandType.R: return r.GetExpression(value, line);
            case OperandType.RK: return r.GetKExpression(value, line);
            case OperandType.K: return r.GetFunction().GetConstantExpression(value);
            case OperandType.I: return ConstantExpression.CreateInteger(value);
            case OperandType.F: return ConstantExpression.CreateDouble((double)value);
            default: throw new InvalidOperationException();
        }
    }

    public bool IsRegister(Registers r)
    {
        switch (type)
        {
            case OperandType.R: return true;
            case OperandType.RK: return !r.IsKConstant(value);
            case OperandType.K: return false;
            case OperandType.I: return false;
            case OperandType.F: return false;
            default: throw new InvalidOperationException();
        }
    }

    public int GetUpdated(Registers r, int line)
    {
        switch (type)
        {
            case OperandType.R: return r.GetUpdated(value, line);
            case OperandType.RK:
                if (r.IsKConstant(value)) throw new InvalidOperationException();
                return r.GetUpdated(value, line);
            default: throw new InvalidOperationException();
        }
    }
}
