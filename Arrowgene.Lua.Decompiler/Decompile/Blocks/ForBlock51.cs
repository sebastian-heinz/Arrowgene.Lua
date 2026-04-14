using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.ForBlock51. Lua 5.1+ numeric
/// for-loop layout: three internal control registers
/// (start/limit/step) followed by <c>varCount</c> explicit user
/// variables. Scope-end adjustments depend on whether a CLOSE is
/// emitted pre-loop or post-loop and on the version-specific close
/// semantics (<c>DEFAULT</c> vs <c>LUA54</c>).
/// </summary>
public class ForBlock51 : ForBlock
{
    protected bool forvarPostClose;
    protected bool closeIsInScope;

    public ForBlock51(LFunction function, int begin, int end, int register, int varCount, CloseType closeType, int closeLine, bool forvarPreClose, bool forvarPostClose, bool closeIsInScope)
        : base(function, begin, end, register, varCount, closeType, closeLine, forvarPreClose)
    {
        this.forvarPostClose = forvarPostClose;
        this.closeIsInScope = closeIsInScope;
    }

    public override void Resolve(Registers r)
    {
        target = r.GetTarget(register + varCount, begin - 1);
        start = r.GetValue(register, begin - 1);
        stop = r.GetValue(register + 1, begin - 1);
        step = r.GetValue(register + 2, begin - 1);
    }

    public override void HandleVariableDeclarations(Registers r)
    {
        int implicitEnd = end - 1;
        if (forvarPostClose) implicitEnd++;
        for (int i = 0; i < varCount; i++)
        {
            r.SetInternalLoopVariable(register + i, begin - 2, implicitEnd);
        }
        int explicitEnd = end - 2;
        if (forvarPreClose)
        {
            Version.CloseSemantics closeSemantics = r.GetVersion().closesemantics.Get();
            if (closeSemantics == Version.CloseSemantics.DEFAULT) explicitEnd--;
            else if (closeSemantics == Version.CloseSemantics.LUA54 && !closeIsInScope) explicitEnd--;
        }
        r.SetExplicitLoopVariable(register + varCount, begin - 1, explicitEnd);
    }
}
