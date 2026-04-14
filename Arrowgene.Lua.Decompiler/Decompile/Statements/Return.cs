using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Statements;

/// <summary>
/// Port of unluac.decompile.statement.Return. The <c>return ...</c>
/// statement, with an optional list of return values. When promoted to
/// non-tail position the statement wraps itself in a <c>do ... end</c>
/// (since Lua's parser only accepts <c>return</c> at the end of a block).
/// </summary>
public class Return : Statement
{
    private readonly Expression[] values;

    public Return()
    {
        values = new Expression[0];
    }

    public Return(Expression value)
    {
        values = new Expression[1];
        values[0] = value;
    }

    public Return(Expression[] values)
    {
        this.values = values;
    }

    public override void Walk(Walker w)
    {
        w.VisitStatement(this);
        foreach (Expression expression in values)
        {
            expression.Walk(w);
        }
    }

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("do ");
        PrintTail(d, @out);
        @out.Print(" end");
    }

    public override void PrintTail(Decompiler d, Output @out)
    {
        @out.Print("return");
        if (values.Length > 0)
        {
            @out.Print(" ");
            var rtns = new List<Expression>(values.Length);
            foreach (Expression value in values)
            {
                rtns.Add(value);
            }
            Expression.PrintSequence(d, @out, rtns, false, true);
        }
    }
}
