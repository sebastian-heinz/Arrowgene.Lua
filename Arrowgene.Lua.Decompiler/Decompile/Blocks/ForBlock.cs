using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Decompile.Targets;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.ForBlock. Abstract intermediate
/// base for numeric for-loops (<c>for i = a, b, c do ... end</c>).
/// Owns the shared registers, variable-count, forvarPreClose flag,
/// and the common printer that elides the <c>, step</c> clause when
/// the step is the integer constant 1. Subclasses decide how to
/// bind the loop variables in the register table.
/// </summary>
public abstract class ForBlock : ContainerBlock
{
    protected readonly int register;
    protected readonly int varCount;
    protected readonly bool forvarPreClose;

    protected Target target;
    protected Expression start;
    protected Expression stop;
    protected Expression step;

    protected ForBlock(LFunction function, int begin, int end, int register, int varCount, CloseType closeType, int closeLine, bool forvarPreClose)
        : base(function, begin, end, closeType, closeLine, -1)
    {
        this.register = register;
        this.varCount = varCount;
        this.forvarPreClose = forvarPreClose;
    }

    public abstract void HandleVariableDeclarations(Registers r);

    public override void Walk(Walker w)
    {
        w.VisitStatement(this);
        start.Walk(w);
        stop.Walk(w);
        step.Walk(w);
        foreach (Statement statement in statements)
        {
            statement.Walk(w);
        }
    }

    public override int ScopeEnd()
    {
        int scopeEnd = end - 2;
        if (forvarPreClose) scopeEnd--;
        if (usingClose && (closeType == CloseType.CLOSE)) scopeEnd--;
        return scopeEnd;
    }

    public override bool Breakable() => true;

    public override bool HasHeader() => true;

    public override bool IsUnprotected() => false;

    public override int GetLoopback() => throw new System.InvalidOperationException();

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("for ");
        target.Print(d, @out, false);
        @out.Print(" = ");
        start.Print(d, @out);
        @out.Print(", ");
        stop.Print(d, @out);
        if (!step.IsInteger() || step.AsInteger() != 1)
        {
            @out.Print(", ");
            step.Print(d, @out);
        }
        @out.Print(" do");
        @out.PrintLn();
        @out.Indent();
        Statement.PrintSequence(d, @out, statements);
        @out.Dedent();
        @out.Print("end");
    }
}
