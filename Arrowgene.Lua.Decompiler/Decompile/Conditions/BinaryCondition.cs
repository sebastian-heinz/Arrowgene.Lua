using System;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Conditions;

/// <summary>
/// Port of unluac.decompile.condition.BinaryCondition. A predicate
/// over two operands with a Lua relational operator. Only EQ is
/// invertible via a re-flip of the internal <c>inverted</c> flag
/// (other operators wrap in a <see cref="NotCondition"/>). At
/// emission time the left and right operands may be transposed so
/// that the natural evaluation order matches the source: when both
/// operands are registers the one updated last is printed second;
/// when both are K-constants the constant-table index order
/// decides.
/// </summary>
public class BinaryCondition : Condition
{
    public enum Operator
    {
        EQ,
        LT,
        LE,
        GT,
        GE,
    }

    private static string OperatorToString(Operator op, bool inverted, bool transposed)
    {
        switch (op)
        {
            case Operator.EQ: return inverted ? "~=" : "==";
            case Operator.LT: return transposed ? ">" : "<";
            case Operator.LE: return transposed ? ">=" : "<=";
            case Operator.GT: return transposed ? "<" : ">";
            case Operator.GE: return transposed ? "<=" : ">=";
        }
        throw new InvalidOperationException();
    }

    private readonly Operator op;
    private readonly int line;
    private readonly Operand left;
    private readonly Operand right;
    private readonly bool inverted;

    public BinaryCondition(Operator op, int line, Operand left, Operand right)
        : this(op, line, left, right, false)
    {
    }

    private BinaryCondition(Operator op, int line, Operand left, Operand right, bool inverted)
    {
        this.op = op;
        this.line = line;
        this.left = left;
        this.right = right;
        this.inverted = inverted;
    }

    public Condition Inverse()
    {
        if (op == Operator.EQ)
        {
            return new BinaryCondition(op, line, left, right, !inverted);
        }
        return new NotCondition(this);
    }

    public bool Invertible() => op == Operator.EQ;

    public int Register() => -1;

    public bool IsRegisterTest() => false;

    public bool IsOrCondition() => false;

    public bool IsSplitable() => false;

    public Condition[] Split() => throw new InvalidOperationException();

    public Expression AsExpression(Registers r)
    {
        bool transpose = false;
        Expression leftExpression = left.AsExpression(r, line);
        Expression rightExpression = right.AsExpression(r, line);
        if (op != Operator.EQ || left.type == OperandType.K)
        {
            if (left.IsRegister(r) && right.IsRegister(r))
            {
                transpose = left.GetUpdated(r, line) > right.GetUpdated(r, line);
            }
            else
            {
                int rightIndex = rightExpression.GetConstantIndex();
                int leftIndex = leftExpression.GetConstantIndex();
                if (rightIndex != -1 && leftIndex != -1)
                {
                    if (left.type == OperandType.K && rightIndex == leftIndex)
                    {
                        transpose = true;
                    }
                    else
                    {
                        transpose = rightIndex < leftIndex;
                    }
                }
            }
        }
        string opstring = OperatorToString(op, inverted, transpose);
        Expression rtn = new BinaryExpression(
            opstring,
            !transpose ? leftExpression : rightExpression,
            !transpose ? rightExpression : leftExpression,
            Expression.PRECEDENCE_COMPARE,
            Expression.ASSOCIATIVITY_LEFT);
        return rtn;
    }

    public override string ToString()
    {
        return left + " " + OperatorToString(op, inverted, false) + " " + right;
    }
}
