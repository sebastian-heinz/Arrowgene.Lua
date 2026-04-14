using System.Collections.Generic;

namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.FunctionCall. Wraps a callee
/// expression plus its argument list and emits either a normal call
/// (<c>f(a, b)</c>) or a method-style call (<c>obj:m(a)</c>) when the
/// callee is a member access whose table matches the first argument.
/// </summary>
public class FunctionCall : Expression
{
    private readonly Expression function;
    private readonly Expression[] arguments;
    private readonly bool multiple;

    public FunctionCall(Expression function, Expression[] arguments, bool multiple)
        : base(PRECEDENCE_ATOMIC)
    {
        this.function = function;
        this.arguments = arguments;
        this.multiple = multiple;
    }

    public override void Walk(Walker w)
    {
        w.VisitExpression(this);
        function.Walk(w);
        foreach (Expression expression in arguments)
        {
            expression.Walk(w);
        }
    }

    public override int GetConstantIndex()
    {
        int index = function.GetConstantIndex();
        foreach (Expression argument in arguments)
        {
            int a = argument.GetConstantIndex();
            if (a > index) index = a;
        }
        return index;
    }

    public override bool IsMultiple() => multiple;

    public override void PrintMultiple(Decompiler d, Output @out)
    {
        if (!multiple) @out.Print("(");
        Print(d, @out);
        if (!multiple) @out.Print(")");
    }

    private bool IsMethodCall()
    {
        return function.IsMemberAccess() && arguments.Length > 0 && function.GetTable() == arguments[0];
    }

    public override bool BeginsWithParen()
    {
        if (IsMethodCall())
        {
            Expression obj = function.GetTable();
            return obj.IsUngrouped() || obj.BeginsWithParen();
        }
        return function.IsUngrouped() || function.BeginsWithParen();
    }

    public override void Print(Decompiler d, Output @out)
    {
        var args = new List<Expression>(arguments.Length);
        if (IsMethodCall())
        {
            Expression obj = function.GetTable();
            if (obj.IsUngrouped())
            {
                @out.Print("(");
                obj.Print(d, @out);
                @out.Print(")");
            }
            else
            {
                obj.Print(d, @out);
            }
            @out.Print(":");
            @out.Print(function.GetField());
            for (int i = 1; i < arguments.Length; i++)
            {
                args.Add(arguments[i]);
            }
        }
        else
        {
            if (function.IsUngrouped())
            {
                @out.Print("(");
                function.Print(d, @out);
                @out.Print(")");
            }
            else
            {
                function.Print(d, @out);
            }
            for (int i = 0; i < arguments.Length; i++)
            {
                args.Add(arguments[i]);
            }
        }
        @out.Print("(");
        PrintSequence(d, @out, args, false, true);
        @out.Print(")");
    }
}
