using System;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.OuterBlock. The implicit top-level
/// block wrapping an entire function body. Always contains a trailing
/// compiler-emitted RETURN instruction that is stripped before
/// printing. The scope-end point is version-sensitive: Lua 5.0/5.1
/// closes the outer scope one instruction earlier than later versions.
/// </summary>
public class OuterBlock : ContainerBlock
{
    public OuterBlock(LFunction function, int length)
        : base(function, 0, length + 1, CloseType.NONE, -1, -2)
    {
    }

    public override bool Breakable() => false;

    public override bool HasHeader() => false;

    public override bool IsUnprotected() => false;

    public override int GetLoopback() => throw new InvalidOperationException();

    public override int ScopeEnd()
    {
        return (end - 1) + function.header.version.outerblockscopeadjustment.Get().Value;
    }

    public override void Print(Decompiler d, Output @out)
    {
        // extra return statement
        int last = statements.Count - 1;
        if (last < 0 || !(statements[last] is Return))
        {
            throw new InvalidOperationException(statements[last].ToString());
        }
        statements.RemoveAt(last);
        Statement.PrintSequence(d, @out, statements);
    }
}
