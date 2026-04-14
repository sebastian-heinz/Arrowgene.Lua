using Arrowgene.Lua.Decompiler.Decompile.Conditions;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.IfThenElseBlock. The <c>if ... then</c>
/// half of an if/else pair. It knows its <see cref="partner"/>
/// <see cref="ElseEndBlock"/> so the two can be emitted as one
/// fused construct, and compares-lesser than its partner so it is
/// always processed first. Suppresses its own trailing newline so
/// the partner can emit the <c>else</c> on the same line block.
/// </summary>
public class IfThenElseBlock : ContainerBlock
{
    private Condition cond;
    private readonly int elseTarget;
    public ElseEndBlock partner;

    private Expression condexpr;

    public IfThenElseBlock(LFunction function, Condition cond, int begin, int end, int elseTarget, CloseType closeType, int closeLine)
        : base(function, begin, end, closeType, closeLine, -1)
    {
        this.cond = cond;
        this.elseTarget = elseTarget;
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

    public override bool SuppressNewline() => true;

    public override int CompareTo(Block block)
    {
        if (block == partner)
        {
            return -1;
        }
        return base.CompareTo(block);
    }

    public override bool Breakable() => false;

    public override int ScopeEnd() => end - 2;

    public override bool HasHeader() => true;

    public override bool IsUnprotected() => true;

    public override int GetUnprotectedLine() => end - 1;

    public override int GetUnprotectedTarget() => elseTarget;

    public override int GetLoopback() => throw new System.InvalidOperationException();

    public override bool IsSplitable() => cond.IsSplitable();

    public override Block[] Split(int line, CloseType closeType)
    {
        Condition[] conds = cond.Split();
        cond = conds[0];
        return new Block[]
        {
            new IfThenElseBlock(function, conds[1], begin, line + 1, end - 1, closeType, line - 1),
            new ElseEndBlock(function, line + 1, end - 1, CloseType.NONE, -1),
        };
    }

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("if ");
        condexpr.Print(d, @out);
        @out.Print(" then");
        @out.PrintLn();
        @out.Indent();

        Statement.PrintSequence(d, @out, statements);

        @out.Dedent();

        // Handle the "empty else" case
        if (end == elseTarget)
        {
            @out.PrintLn("else");
            @out.PrintLn("end");
        }
    }
}
