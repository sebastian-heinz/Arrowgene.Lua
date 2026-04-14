using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Decompile.Targets;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.TForBlock. Generic
/// <c>for ... in ... do ... end</c> loop. Models the internal
/// iterator state (control function + control state + control
/// value, possibly plus an extra TBC slot in 5.4) and the explicit
/// user-visible loop variables as two separate register ranges
/// with their own scope extents. Three static factories
/// (<see cref="Make50"/>, <see cref="Make51"/>, <see cref="Make54"/>)
/// compute the per-version register layout and scope-end offsets.
/// </summary>
public class TForBlock : ContainerBlock
{
    protected readonly int internalRegisterFirst;
    protected readonly int internalRegisterLast;

    protected readonly int explicitRegisterFirst;
    protected readonly int explicitRegisterLast;

    protected readonly int internalScopeBegin;
    protected readonly int internalScopeEnd;

    protected readonly int explicitScopeBegin;
    protected readonly int explicitScopeEnd;

    protected readonly int innerScopeEnd;

    private Target[] targets;
    private Expression[] values;

    public static TForBlock Make50(LFunction function, int begin, int end, int register, int length, bool innerClose)
    {
        int innerScopeEnd = end - 3;
        if (innerClose)
        {
            innerScopeEnd--;
        }
        return new TForBlock(
            function, begin, end,
            CloseType.NONE, -1,
            register, register + 1, register + 2, register + 1 + length,
            begin - 1, end - 1,
            begin - 1, end - 1,
            innerScopeEnd
        );
    }

    public static TForBlock Make51(LFunction function, int begin, int end, int register, int length, bool forvarClose, bool innerClose)
    {
        int explicitScopeEnd = end - 3;
        int innerScopeEnd = end - 3;
        if (function.header.version.closesemantics.Get() == Version.CloseSemantics.JUMP)
        {
            if (forvarClose)
            {
                innerScopeEnd--;
            }
        }
        else
        {
            if (forvarClose)
            {
                explicitScopeEnd--;
                innerScopeEnd--;
            }
            if (innerClose)
            {
                innerScopeEnd--;
            }
        }
        return new TForBlock(
            function, begin, end,
            CloseType.NONE, -1,
            register, register + 2, register + 3, register + 2 + length,
            begin - 2, end - 1,
            begin - 1, explicitScopeEnd,
            innerScopeEnd
        );
    }

    public static TForBlock Make54(
        LFunction function, int begin, int end, int register, int length,
        CloseType closeType, int closeLine,
        bool forvarClose, bool closeIsInScope,
        int controlCount)
    {
        int internalScopeEnd = end - 1;
        int explicitScopeEnd = end - 3;
        int innerScopeEnd = end - 3;
        if (forvarClose)
        {
            innerScopeEnd--;
            if (!closeIsInScope)
            {
                explicitScopeEnd--;
            }
        }
        if (closeIsInScope)
        {
            internalScopeEnd++;
        }
        return new TForBlock(
            function, begin, end,
            closeType, closeLine,
            register, register + controlCount - 1,
            register + controlCount, register + controlCount + length - 1,
            begin - 2, internalScopeEnd,
            begin - 1, explicitScopeEnd,
            innerScopeEnd
        );
    }

    public TForBlock(LFunction function, int begin, int end,
        CloseType closeType, int closeLine,
        int internalRegisterFirst, int internalRegisterLast,
        int explicitRegisterFirst, int explicitRegisterLast,
        int internalScopeBegin, int internalScopeEnd,
        int explicitScopeBegin, int explicitScopeEnd,
        int innerScopeEnd
    ) : base(function, begin, end, closeType, closeLine, -1)
    {
        this.internalRegisterFirst = internalRegisterFirst;
        this.internalRegisterLast = internalRegisterLast;
        this.explicitRegisterFirst = explicitRegisterFirst;
        this.explicitRegisterLast = explicitRegisterLast;
        this.internalScopeBegin = internalScopeBegin;
        this.internalScopeEnd = internalScopeEnd;
        this.explicitScopeBegin = explicitScopeBegin;
        this.explicitScopeEnd = explicitScopeEnd;
        this.innerScopeEnd = innerScopeEnd;
    }

    public List<Target> GetTargets(Registers r)
    {
        List<Target> targets = new List<Target>(explicitRegisterLast - explicitRegisterFirst + 1);
        for (int register = explicitRegisterFirst; register <= explicitRegisterLast; register++)
        {
            targets.Add(r.GetTarget(register, begin - 1));
        }
        return targets;
    }

    public void HandleVariableDeclarations(Registers r)
    {
        for (int register = internalRegisterFirst; register <= internalRegisterLast; register++)
        {
            r.SetInternalLoopVariable(register, internalScopeBegin, internalScopeEnd);
        }
        for (int register = explicitRegisterFirst; register <= explicitRegisterLast; register++)
        {
            r.SetExplicitLoopVariable(register, explicitScopeBegin, explicitScopeEnd);
        }
    }

    public override void Resolve(Registers r)
    {
        List<Target> t = GetTargets(r);
        List<Expression> v = new List<Expression>(3);
        for (int register = internalRegisterFirst; register <= internalRegisterLast; register++)
        {
            Expression value = r.GetValue(register, begin - 1);
            v.Add(value);
            if (value.IsMultiple()) break;
        }

        targets = t.ToArray();
        values = v.ToArray();
    }

    public override void Walk(Walker w)
    {
        w.VisitStatement(this);
        foreach (Expression expression in values)
        {
            expression.Walk(w);
        }
        foreach (Statement statement in statements)
        {
            statement.Walk(w);
        }
    }

    public override int ScopeEnd() => innerScopeEnd;

    public override bool Breakable() => true;

    public override bool HasHeader() => true;

    public override bool IsUnprotected() => false;

    public override int GetLoopback() => throw new System.InvalidOperationException();

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("for ");
        targets[0].Print(d, @out, false);
        for (int i = 1; i < targets.Length; i++)
        {
            @out.Print(", ");
            targets[i].Print(d, @out, false);
        }
        @out.Print(" in ");
        values[0].Print(d, @out);
        for (int i = 1; i < values.Length; i++)
        {
            @out.Print(", ");
            values[i].Print(d, @out);
        }
        @out.Print(" do");
        @out.PrintLn();
        @out.Indent();
        Statement.PrintSequence(d, @out, statements);
        @out.Dedent();
        @out.Print("end");
    }
}
