using Arrowgene.Lua.Decompiler.Decompile.Conditions;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.WhileBlock50. Lua 5.0's while-loop
/// shape: the condition is tested at the top of the loop and the
/// compiler emits a separate enter-target jump, so scope-end comes
/// from <c>enterTarget - 1</c> and the block is never unprotected.
/// </summary>
public class WhileBlock50 : WhileBlock
{
    private readonly int enterTarget;

    public WhileBlock50(LFunction function, Condition cond, int begin, int end, int enterTarget, CloseType closeType, int closeLine)
        : base(function, cond, begin, end, closeType, closeLine)
    {
        this.enterTarget = enterTarget;
    }

    public override int ScopeEnd() => enterTarget - 1;

    public override bool IsUnprotected() => false;
}
