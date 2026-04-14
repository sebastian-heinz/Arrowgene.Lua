using Arrowgene.Lua.Decompiler.Decompile.Conditions;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.WhileBlock51. Lua 5.1+ while-loop
/// shape: ends in a JMP back to the loop head so the block is
/// unprotected. Exposes the jump target for the control-flow
/// handler's redirect pass and supports splitting along a
/// split-ready condition, which degrades the loop into a nested
/// if/else frame plus a continuation.
/// </summary>
public class WhileBlock51 : WhileBlock
{
    private readonly int unprotectedTarget;

    public WhileBlock51(LFunction function, Condition cond, int begin, int end, int unprotectedTarget, CloseType closeType, int closeLine)
        : base(function, cond, begin, end, closeType, closeLine)
    {
        this.unprotectedTarget = unprotectedTarget;
    }

    public override int ScopeEnd() => end - 2;

    public override bool IsUnprotected() => true;

    public override int GetUnprotectedLine() => end - 1;

    public override int GetUnprotectedTarget() => unprotectedTarget;

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
}
