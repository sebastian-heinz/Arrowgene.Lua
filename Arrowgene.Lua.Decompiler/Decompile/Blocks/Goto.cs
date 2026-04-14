using System;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.Goto. A degenerate block emitting a
/// <c>goto lbl_N</c> statement. Declares a header so the output writer
/// keeps it on its own line inside a containing block.
/// </summary>
public class Goto : Block
{
    public readonly int target;

    public Goto(LFunction function, int line, int target) : base(function, line, line, 2)
    {
        this.target = target;
    }

    public override void Walk(Walker w) => w.VisitStatement(this);

    public override void AddStatement(Statement statement) => throw new InvalidOperationException();

    public override bool IsContainer() => false;

    public override bool IsEmpty() => true;

    public override bool Breakable() => false;

    public override bool HasHeader() => true;

    // Actually, it is unprotected, but not really a block
    public override bool IsUnprotected() => false;

    public override int GetLoopback() => throw new InvalidOperationException();

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("goto lbl_" + target);
    }
}
