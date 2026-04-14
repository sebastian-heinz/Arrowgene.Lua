using System;
using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Targets;

namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.Expression. The abstract base class
/// for every value-producing AST node the decompiler emits: literals,
/// variables, arithmetic, table accesses, calls, closures, varargs, and so
/// on. Concrete subclasses live alongside in this namespace and override
/// <see cref="Walk"/>, <see cref="Print"/>, and <see cref="GetConstantIndex"/>.
/// </summary>
/// <remarks>
/// The C# namespace is pluralised (<c>Expressions</c>) to avoid colliding
/// with the <see cref="Expression"/> class name itself. A handful of
/// virtuals (<c>BeginsWithParen</c>, <c>IsNil</c>, etc.) default to
/// <c>false</c>; subclasses opt in.
/// </remarks>
public abstract class Expression
{
    public const int PRECEDENCE_OR = 1;
    public const int PRECEDENCE_AND = 2;
    public const int PRECEDENCE_COMPARE = 3;
    public const int PRECEDENCE_BOR = 4;
    public const int PRECEDENCE_BXOR = 5;
    public const int PRECEDENCE_BAND = 6;
    public const int PRECEDENCE_SHIFT = 7;
    public const int PRECEDENCE_CONCAT = 8;
    public const int PRECEDENCE_ADD = 9;
    public const int PRECEDENCE_MUL = 10;
    public const int PRECEDENCE_UNARY = 11;
    public const int PRECEDENCE_POW = 12;
    public const int PRECEDENCE_ATOMIC = 13;

    public const int ASSOCIATIVITY_NONE = 0;
    public const int ASSOCIATIVITY_LEFT = 1;
    public const int ASSOCIATIVITY_RIGHT = 2;

    public readonly int Precedence;

    protected Expression(int precedence)
    {
        Precedence = precedence;
    }

    public abstract void Walk(Walker w);

    public abstract void Print(Decompiler d, Output @out);

    public virtual void PrintBraced(Decompiler d, Output @out) => Print(d, @out);

    public virtual void PrintMultiple(Decompiler d, Output @out) => Print(d, @out);

    /// <summary>
    /// Determines the index of the last-declared constant in this expression.
    /// If there is no constant in the expression, return -1.
    /// </summary>
    public abstract int GetConstantIndex();

    public virtual int GetConstantLine() => -1;

    public virtual bool IsNameExternallyBound(string id)
    {
        throw new InvalidOperationException();
    }

    public virtual bool BeginsWithParen() => false;
    public virtual bool IsNil() => false;
    public virtual bool IsClosure() => false;
    public virtual bool IsConstant() => false;

    /// <summary>
    /// An ungrouped expression is one that needs to be enclosed in parentheses
    /// before it can be dereferenced. This doesn't apply to multiply-valued
    /// expressions as those will be given parentheses automatically when
    /// converted to a single value. e.g. (a+b).c; ("asdf"):gsub()
    /// </summary>
    public virtual bool IsUngrouped() => false;

    /// <summary>Only supported for closures.</summary>
    public virtual bool IsUpvalueOf(int register)
    {
        throw new InvalidOperationException();
    }

    public virtual bool IsBoolean() => false;
    public virtual bool IsInteger() => false;

    public virtual int AsInteger()
    {
        throw new InvalidOperationException();
    }

    public virtual bool IsString() => false;
    public virtual bool IsIdentifier() => false;

    /// <summary>
    /// Determines if this can be part of a function name. Is it of the form:
    /// {Name . } Name
    /// </summary>
    public virtual bool IsDotChain() => false;

    public virtual int ClosureUpvalueLine()
    {
        throw new InvalidOperationException();
    }

    public virtual void PrintClosure(Decompiler d, Output @out, Target name)
    {
        throw new InvalidOperationException();
    }

    public virtual string AsName()
    {
        throw new InvalidOperationException();
    }

    public virtual bool IsTableLiteral() => false;

    public virtual bool IsNewEntryAllowed()
    {
        throw new InvalidOperationException();
    }

    public virtual void AddEntry(TableLiteral.Entry entry)
    {
        throw new InvalidOperationException();
    }

    /// <summary>Whether the expression has more than one return stored into registers.</summary>
    public virtual bool IsMultiple() => false;

    public virtual bool IsMemberAccess() => false;

    public virtual Expression GetTable()
    {
        throw new InvalidOperationException();
    }

    public virtual string GetField()
    {
        throw new InvalidOperationException();
    }

    public virtual bool IsBrief() => false;

    public virtual bool IsEnvironmentTable(Decompiler d) => false;

    // -------------------------------------------------------------------
    // Static helpers
    // -------------------------------------------------------------------

    public enum BinaryOperation
    {
        CONCAT,
        ADD,
        SUB,
        MUL,
        DIV,
        IDIV,
        MOD,
        POW,
        BAND,
        BOR,
        BXOR,
        SHL,
        SHR,
        OR,
        AND,
    }

    public enum UnaryOperation
    {
        UNM,
        NOT,
        LEN,
        BNOT,
    }

    public static (string op, int precedence, int associativity) Info(BinaryOperation op) => op switch
    {
        BinaryOperation.CONCAT => ("..", PRECEDENCE_CONCAT, ASSOCIATIVITY_RIGHT),
        BinaryOperation.ADD    => ("+",  PRECEDENCE_ADD,    ASSOCIATIVITY_LEFT),
        BinaryOperation.SUB    => ("-",  PRECEDENCE_ADD,    ASSOCIATIVITY_LEFT),
        BinaryOperation.MUL    => ("*",  PRECEDENCE_MUL,    ASSOCIATIVITY_LEFT),
        BinaryOperation.DIV    => ("/",  PRECEDENCE_MUL,    ASSOCIATIVITY_LEFT),
        BinaryOperation.IDIV   => ("//", PRECEDENCE_MUL,    ASSOCIATIVITY_LEFT),
        BinaryOperation.MOD    => ("%",  PRECEDENCE_MUL,    ASSOCIATIVITY_LEFT),
        BinaryOperation.POW    => ("^",  PRECEDENCE_POW,    ASSOCIATIVITY_RIGHT),
        BinaryOperation.BAND   => ("&",  PRECEDENCE_BAND,   ASSOCIATIVITY_LEFT),
        BinaryOperation.BOR    => ("|",  PRECEDENCE_BOR,    ASSOCIATIVITY_LEFT),
        BinaryOperation.BXOR   => ("~",  PRECEDENCE_BXOR,   ASSOCIATIVITY_LEFT),
        BinaryOperation.SHL    => ("<<", PRECEDENCE_SHIFT,  ASSOCIATIVITY_LEFT),
        BinaryOperation.SHR    => (">>", PRECEDENCE_SHIFT,  ASSOCIATIVITY_LEFT),
        BinaryOperation.OR     => ("or", PRECEDENCE_OR,     ASSOCIATIVITY_NONE),
        BinaryOperation.AND    => ("and",PRECEDENCE_AND,    ASSOCIATIVITY_NONE),
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    public static string Info(UnaryOperation op) => op switch
    {
        UnaryOperation.UNM  => "-",
        UnaryOperation.NOT  => "not ",
        UnaryOperation.LEN  => "#",
        UnaryOperation.BNOT => "~",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    public static BinaryExpression Make(BinaryOperation op, Expression left, Expression right)
    {
        return Make(op, left, right, false);
    }

    public static BinaryExpression Make(BinaryOperation op, Expression left, Expression right, bool flip)
    {
        if (flip)
        {
            (left, right) = (right, left);
        }
        var (sym, precedence, associativity) = Info(op);
        return new BinaryExpression(sym, left, right, precedence, associativity);
    }

    public static UnaryExpression Make(UnaryOperation op, Expression expression)
    {
        return new UnaryExpression(Info(op), expression, PRECEDENCE_UNARY);
    }

    /// <summary>
    /// Prints out a sequence of expressions with commas, optionally
    /// handling multiple expressions and return value adjustment.
    /// </summary>
    public static void PrintSequence(Decompiler d, Output @out, IList<Expression> exprs, bool linebreak, bool multiple)
    {
        int n = exprs.Count;
        int i = 1;
        foreach (Expression expr in exprs)
        {
            bool last = (i == n);
            if (expr.IsMultiple())
            {
                last = true;
            }
            if (last)
            {
                if (multiple)
                {
                    expr.PrintMultiple(d, @out);
                }
                else
                {
                    expr.Print(d, @out);
                }
                break;
            }
            else
            {
                expr.Print(d, @out);
                @out.Print(",");
                if (linebreak)
                {
                    @out.PrintLn();
                }
                else
                {
                    @out.Print(" ");
                }
            }
            i++;
        }
    }

    protected static void PrintUnary(Decompiler d, Output @out, string op, Expression expression)
    {
        @out.Print(op);
        expression.Print(d, @out);
    }

    protected static void PrintBinary(Decompiler d, Output @out, string op, Expression left, Expression right)
    {
        left.Print(d, @out);
        @out.Print(" ");
        @out.Print(op);
        @out.Print(" ");
        right.Print(d, @out);
    }
}
