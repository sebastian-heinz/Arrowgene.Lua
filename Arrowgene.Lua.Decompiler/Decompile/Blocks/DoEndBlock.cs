using System;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.DoEndBlock. An explicit
/// <c>do ... end</c> region. Allows pre-declaration of locals so the
/// control-flow analyzer can pull declarations out of narrower scopes.
/// </summary>
public class DoEndBlock : ContainerBlock
{
    public DoEndBlock(LFunction function, int begin, int end)
        : base(function, begin, end, CloseType.NONE, -1, 1)
    {
    }

    public override bool Breakable() => false;

    public override bool HasHeader() => false;

    public override bool IsUnprotected() => false;

    public override bool AllowsPreDeclare() => true;

    public override int GetLoopback() => throw new InvalidOperationException();

    public override void Print(Decompiler d, Output @out)
    {
        @out.PrintLn("do");
        @out.Indent();
        Statement.PrintSequence(d, @out, statements);
        @out.Dedent();
        @out.Print("end");
    }
}
