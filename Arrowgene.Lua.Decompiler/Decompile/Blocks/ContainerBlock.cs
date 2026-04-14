using System.Collections.Generic;
using System;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.ContainerBlock. An intermediate base
/// for every block that owns a list of child statements (do/end, if,
/// while, repeat, for, the outer function body, ...). Provides the
/// shared statement-accumulator and close-line plumbing.
/// </summary>
public abstract class ContainerBlock : Block
{
    protected readonly List<Statement> statements;
    protected readonly CloseType closeType;
    protected readonly int closeLine;
    protected bool usingClose;

    protected ContainerBlock(LFunction function, int begin, int end, CloseType closeType, int closeLine, int priority)
        : base(function, begin, end, priority)
    {
        this.closeType = closeType;
        this.closeLine = closeLine;
        usingClose = false;
        int capacity = Math.Max(4, end - begin + 1);
        statements = new List<Statement>(capacity);
    }

    public override void Walk(Walker w)
    {
        w.VisitStatement(this);
        foreach (Statement statement in statements)
        {
            statement.Walk(w);
        }
    }

    public override bool IsContainer() => begin < end;

    public override bool IsEmpty() => statements.Count == 0;

    public override void AddStatement(Statement statement)
    {
        statements.Add(statement);
    }

    public override bool HasCloseLine() => closeType != CloseType.NONE;

    public override int GetCloseLine()
    {
        if (closeType == CloseType.NONE)
        {
            throw new InvalidOperationException();
        }
        return closeLine;
    }

    public override CloseType GetCloseType() => closeType;

    public override void UseClose()
    {
        usingClose = true;
    }
}
