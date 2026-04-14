using Arrowgene.Lua.Decompiler.Decompile.Conditions;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.RepeatBlock. A
/// <c>repeat ... until cond</c> loop. The <c>extendedRepeatScope</c>
/// flag keeps locals declared inside the body alive across the
/// until-expression for Lua versions where the condition can
/// legally reference them (matching the <c>local x = ... until x</c>
/// idiom). Walks its body before its condition because the
/// condition is evaluated at the tail.
/// </summary>
public class RepeatBlock : ContainerBlock
{
    private readonly Condition cond;
    private readonly bool extendedRepeatScope;
    private readonly int extendedScopeEnd;

    private Expression condexpr;

    public RepeatBlock(LFunction function, Condition cond, int begin, int end, CloseType closeType, int closeLine, bool extendedRepeatScope, int extendedScopeEnd)
        : base(function, begin, end, closeType, closeLine, 0)
    {
        this.cond = cond;
        this.extendedRepeatScope = extendedRepeatScope;
        this.extendedScopeEnd = extendedScopeEnd;
    }

    public override void Resolve(Registers r)
    {
        condexpr = cond.AsExpression(r);
    }

    public override void Walk(Walker w)
    {
        w.VisitStatement(this);
        foreach (Statement statement in statements)
        {
            statement.Walk(w);
        }
        condexpr.Walk(w);
    }

    public override int ScopeEnd() => extendedRepeatScope ? extendedScopeEnd : end - 1;

    public override bool Breakable() => true;

    public override bool HasHeader() => false;

    public override bool IsUnprotected() => false;

    public override int GetLoopback() => throw new System.InvalidOperationException();

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("repeat");
        @out.PrintLn();
        @out.Indent();
        Statement.PrintSequence(d, @out, statements);
        @out.Dedent();
        @out.Print("until ");
        condexpr.Print(d, @out);
    }
}
