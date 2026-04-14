using System;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.Break. A degenerate block that emits a
/// <c>break</c> statement. Carries its jump <see cref="target"/> so the
/// control-flow handler can link it to the enclosing loop. As the tail
/// statement of a block it prints as bare <c>break</c>; otherwise it is
/// wrapped as <c>do break end</c> to preserve statement semantics.
/// </summary>
public class Break : Block
{
    public readonly int target;

    public Break(LFunction function, int line, int target) : base(function, line, line, 2)
    {
        this.target = target;
    }

    public override void Walk(Walker w) => w.VisitStatement(this);

    public override void AddStatement(Statement statement) => throw new InvalidOperationException();

    public override bool IsContainer() => false;

    public override bool IsEmpty() => true;

    public override bool Breakable() => false;

    public override bool HasHeader() => false;

    // Actually, it is unprotected, but not really a block
    public override bool IsUnprotected() => false;

    public override int GetLoopback() => throw new InvalidOperationException();

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("do break end");
        if (comment != null) @out.Print(" -- " + comment);
    }

    public override void PrintTail(Decompiler d, Output @out)
    {
        @out.Print("break");
        if (comment != null) @out.Print(" -- " + comment);
    }
}
