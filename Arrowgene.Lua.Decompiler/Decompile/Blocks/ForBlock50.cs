using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.ForBlock50. Lua 5.0's numeric
/// for-loop layout: a single explicit control register and the
/// limit/step preloaded into the next two registers. The explicit
/// loop variable lives at <c>register</c>; the limit and step are
/// hidden internals.
/// </summary>
public class ForBlock50 : ForBlock
{
    public ForBlock50(LFunction function, int begin, int end, int register, CloseType closeType, int closeLine)
        : base(function, begin, end, register, 2, closeType, closeLine, false)
    {
    }

    public override void Resolve(Registers r)
    {
        target = r.GetTarget(register, begin - 1);
        start = r.GetValue(register, begin - 2);
        stop = r.GetValue(register + 1, begin - 1);
        step = r.GetValue(register + 2, begin - 1);
    }

    public override void HandleVariableDeclarations(Registers r)
    {
        r.SetExplicitLoopVariable(register, begin - 1, end - 1);
        r.SetInternalLoopVariable(register + 1, begin - 1, end - 1);
        r.SetInternalLoopVariable(register + 2, begin - 1, end - 1);
    }
}
