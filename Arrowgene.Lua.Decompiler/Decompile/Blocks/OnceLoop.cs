using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.OnceLoop. A synthetic container
/// used when the control-flow analyzer needs a breakable frame but
/// no real loop. Prints as <c>repeat ... until true</c> so that
/// <c>break</c> statements inside it remain legal Lua.
/// </summary>
public class OnceLoop : ContainerBlock
{
    public OnceLoop(LFunction function, int begin, int end)
        : base(function, begin, end, CloseType.NONE, -1, 0)
    {
    }

    public override bool Breakable() => true;

    public override bool HasHeader() => false;

    public override bool IsUnprotected() => false;

    public override int GetLoopback() => begin;

    public override void Print(Decompiler d, Output @out)
    {
        @out.PrintLn("repeat");
        @out.Indent();
        Statement.PrintSequence(d, @out, statements);
        @out.Dedent();
        @out.Print("until true");
    }
}
