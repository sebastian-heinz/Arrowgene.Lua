using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Conditions;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Operations;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.IfThenEndBlock. A one-armed
/// <c>if ... then ... end</c>. At process time it tries to recognise
/// the compile-time <c>a = b and c</c> / <c>a = b or c</c> idiom where
/// the if-body is a single assignment driven by the same condition
/// register, and if so folds the whole construct into a single
/// <see cref="Assignment"/> carrying a combined
/// <see cref="AndCondition"/>/<see cref="OrCondition"/> expression.
/// </summary>
public class IfThenEndBlock : ContainerBlock
{
    private readonly Condition cond;
    private readonly bool redirected;
    private readonly Registers r;

    private Expression condexpr;

    public IfThenEndBlock(LFunction function, Registers r, Condition cond, int begin, int end)
        : this(function, r, cond, begin, end, CloseType.NONE, -1, false)
    {
    }

    public IfThenEndBlock(LFunction function, Registers r, Condition cond, int begin, int end, CloseType closeType, int closeLine, bool redirected)
        : base(function, begin == end ? begin - 1 : begin, end, closeType, closeLine, -1)
    {
        this.r = r;
        this.cond = cond;
        this.redirected = redirected;
    }

    public override void Resolve(Registers r)
    {
        condexpr = cond.AsExpression(r);
    }

    public override void Walk(Walker w)
    {
        w.VisitStatement(this);
        condexpr.Walk(w);
        foreach (Statement statement in statements)
        {
            statement.Walk(w);
        }
    }

    public override bool Breakable() => false;

    public override bool HasHeader() => false;

    public override bool IsUnprotected() => false;

    public override int GetLoopback() => throw new System.InvalidOperationException();

    public override Operation Process(Decompiler d)
    {
        int test = cond.Register();
        if (!scopeUsed && !redirected && test >= 0 && r.GetUpdated(test, end - 1) >= begin && !d.GetNoDebug())
        {
            // Check for a single assignment
            Assignment assign = null;
            if (statements.Count == 1)
            {
                Statement stmt = statements[0];
                if (stmt is Assignment a)
                {
                    assign = a;
                    if (assign.GetArity() > 1)
                    {
                        int line = assign.GetFirstLine();
                        if (line >= begin && line < end)
                        {
                            assign = null;
                        }
                    }
                }
            }
            bool assignMatches = false;
            if (assign != null)
            {
                if (assign.HasExcess())
                {
                    assignMatches = assign.GetLastRegister() == test;
                }
                else
                {
                    assignMatches = assign.GetLastTarget().IsLocal() && assign.GetLastTarget().GetIndex() == test;
                }
            }
            if (assign != null && (cond.IsRegisterTest() || cond.IsOrCondition() || assign.IsDeclaration()) && assignMatches || statements.Count == 0)
            {
                FinalSetCondition finalset = new FinalSetCondition(end - 1, test);
                finalset.type = FinalSetCondition.Type.VALUE;
                Condition combined;

                if (cond.Invertible())
                {
                    combined = new OrCondition(cond.Inverse(), finalset);
                }
                else
                {
                    combined = new AndCondition(cond, finalset);
                }
                Assignment fassign;
                if (assign != null)
                {
                    fassign = assign;
                    fassign.ReplaceLastValue(combined.AsExpression(r));
                }
                else
                {
                    fassign = null;
                }
                Condition fcombined = combined;
                return new FoldedAssignmentOperation(end - 1, test, fassign, fcombined);
            }
        }
        return base.Process(d);
    }

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("if ");
        condexpr.Print(d, @out);
        @out.Print(" then");
        @out.PrintLn();
        @out.Indent();
        Statement.PrintSequence(d, @out, statements);
        @out.Dedent();
        @out.Print("end");
    }

    private sealed class FoldedAssignmentOperation : Operation
    {
        private readonly int _test;
        private readonly Assignment _fassign;
        private readonly Condition _fcombined;

        public FoldedAssignmentOperation(int line, int test, Assignment fassign, Condition fcombined) : base(line)
        {
            _test = test;
            _fassign = fassign;
            _fcombined = fcombined;
        }

        public override IList<Statement> Process(Registers r, Block block)
        {
            if (_fassign == null)
            {
                r.SetValue(_test, line, _fcombined.AsExpression(r));
                return new List<Statement>();
            }
            return new List<Statement> { _fassign };
        }
    }
}
