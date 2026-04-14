using System;
using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Conditions;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Operations;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.SetBlock. A synthetic block the
/// analyser wraps around a TESTSET/TESTSET54 chain so the final
/// conditional-set instruction can be folded back into an
/// <see cref="Assignment"/> whose value is the combined
/// <see cref="Condition"/> expression. Has no walkable body of its
/// own; its <see cref="AddStatement"/> just captures the single
/// assignment to rewrite.
/// </summary>
public class SetBlock : Block
{
    public readonly int target;
    private Assignment assign;
    public readonly Condition cond;
    private readonly Registers r;
    private bool finalize = false;

    public SetBlock(LFunction function, Condition cond, int target, int line, int begin, int end, Registers r)
        : base(function, begin, end, 2)
    {
        if (begin == end) throw new InvalidOperationException();
        this.target = target;
        this.cond = cond;
        this.r = r;
        if (target == -1)
        {
            throw new InvalidOperationException();
        }
    }

    public override void Walk(Walker w) => throw new InvalidOperationException();

    public override void AddStatement(Statement statement)
    {
        if (!finalize && statement is Assignment a)
        {
            assign = a;
        }
    }

    public override bool HasHeader() => true;

    public override bool IsUnprotected() => false;

    public override int GetLoopback() => throw new InvalidOperationException();

    public override void Print(Decompiler d, Output @out)
    {
        if (assign != null && assign.GetFirstTarget() != null)
        {
            Assignment assignOut = new Assignment(assign.GetFirstTarget(), GetValue(), assign.GetFirstLine());
            assignOut.Print(d, @out);
        }
        else
        {
            throw new InvalidOperationException();
        }
    }

    public override bool Breakable() => false;

    public override bool IsContainer() => true;

    public override bool IsEmpty() => true;

    public void UseAssignment(Assignment assign)
    {
        this.assign = assign;
    }

    public Expression GetValue() => cond.AsExpression(r);

    public override Operation Process(Decompiler d)
    {
        if (ControlFlowHandler.verbose)
        {
            Console.Write("set expression: ");
            cond.AsExpression(r).Print(d, new Output());
            Console.WriteLine();
        }
        if (assign != null)
        {
            assign.ReplaceValue(target, GetValue());
            return new AssignFoldOperation(end - 1, assign);
        }
        return new AssignEmitOperation(end - 1, target, cond, r);
    }

    private sealed class AssignFoldOperation : Operation
    {
        private readonly Assignment _assign;

        public AssignFoldOperation(int line, Assignment assign) : base(line)
        {
            _assign = assign;
        }

        public override IList<Statement> Process(Registers r, Block block)
        {
            return new List<Statement> { _assign };
        }
    }

    private sealed class AssignEmitOperation : Operation
    {
        private readonly int _target;
        private readonly Condition _cond;
        private readonly Registers _r;

        public AssignEmitOperation(int line, int target, Condition cond, Registers r) : base(line)
        {
            _target = target;
            _cond = cond;
            _r = r;
        }

        public override IList<Statement> Process(Registers r, Block block)
        {
            if (r.IsLocal(_target, line))
            {
                return new List<Statement>
                {
                    new Assignment(r.GetTarget(_target, line), _cond.AsExpression(r), line),
                };
            }
            r.SetValue(_target, line, _cond.AsExpression(r));
            return new List<Statement>();
        }
    }
}
