using Arrowgene.Lua.Decompiler.Decompile.Conditions;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.WhileBlock. Abstract intermediate
/// base shared by the Lua 5.0 and 5.1+ while-loop variants. Holds
/// the condition expression and the common <c>while ... do ... end</c>
/// printer; subclasses differ only in how they compute scope bounds
/// and expose jump/unprotected metadata to the control-flow handler.
/// </summary>
public abstract class WhileBlock : ContainerBlock
{
    protected Condition cond;

    private Expression condexpr;

    protected WhileBlock(LFunction function, Condition cond, int begin, int end, CloseType closeType, int closeLine)
        : base(function, begin, end, closeType, closeLine, -1)
    {
        this.cond = cond;
    }

    public override void Resolve(Registers r)
    {
        condexpr = cond.AsExpression(r);
    }

    public override void Walk(Walker w)
    {
        w.VisitStatement(this);
        condexpr.Walk(w);
        foreach (Statement statement in statements)
        {
            statement.Walk(w);
        }
    }

    public override bool Breakable() => true;

    public override bool HasHeader() => true;

    public override int GetLoopback() => throw new System.InvalidOperationException();

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("while ");
        condexpr.Print(d, @out);
        @out.Print(" do");
        @out.PrintLn();
        @out.Indent();
        Statement.PrintSequence(d, @out, statements);
        @out.Dedent();
        @out.Print("end");
    }
}
