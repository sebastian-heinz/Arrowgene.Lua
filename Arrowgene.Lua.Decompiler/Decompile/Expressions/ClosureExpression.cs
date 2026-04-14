using System;
using Arrowgene.Lua.Decompiler.Decompile.Targets;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.ClosureExpression. Wraps a child
/// <see cref="LFunction"/> together with the outer <see cref="Decompiler"/>
/// that owns its sub-decompiler, plus the bytecode line of the parent's
/// <c>CLOSURE</c> instruction (used by upvalue declaration tracking).
/// </summary>
/// <remarks>
/// The two print paths (<see cref="Print"/> and <see cref="PrintClosure"/>)
/// require the target subpackage (<c>VariableTarget</c>, <c>TableTarget</c>),
/// the indentation-aware <see cref="Output"/>, and the recursive
/// <c>Decompiler.Decompile</c>/<c>Print</c> entry points - none of which
/// have been ported yet, so they currently throw. The non-printing
/// methods (<see cref="Walk"/>, <see cref="IsClosure"/>,
/// <see cref="IsUpvalueOf"/>, <see cref="IsNameExternallyBound"/>,
/// <see cref="ClosureUpvalueLine"/>) are fully ported.
/// </remarks>
public class ClosureExpression : Expression
{
    private readonly LFunction function;
    private readonly int upvalueLine;
    private readonly Decompiler d;

    public ClosureExpression(LFunction function, Decompiler d, int upvalueLine)
        : base(PRECEDENCE_ATOMIC)
    {
        this.function = function;
        this.d = d;
        this.upvalueLine = upvalueLine;
    }

    public override void Walk(Walker w) => w.VisitExpression(this);

    public override int GetConstantIndex() => -1;

    public override bool IsClosure() => true;

    public override bool IsUngrouped() => true;

    public override bool IsUpvalueOf(int register)
    {
        for (int i = 0; i < function.upvalues.Length; i++)
        {
            LUpvalue upvalue = function.upvalues[i];
            if (upvalue.instack && upvalue.idx == register)
            {
                return true;
            }
        }
        return false;
    }

    public override bool IsNameExternallyBound(string id)
    {
        foreach (LUpvalue upvalue in function.upvalues)
        {
            if (upvalue.name == id)
            {
                return true;
            }
        }
        if (function.header.version.HasGlobalSupport() && d.IsNameGlobal(id))
        {
            return true;
        }
        return false;
    }

    public override int ClosureUpvalueLine() => upvalueLine;

    public override void Print(Decompiler outer, Output @out)
    {
        @out.Print("function");
        PrintMain(@out, true);
    }

    public override void PrintClosure(Decompiler outer, Output @out, Target name)
    {
        @out.Print("function ");
        if (function.numParams >= 1 && d.declList[0].name.Equals("self") && name is TableTarget)
        {
            name.PrintMethod(outer, @out);
            PrintMain(@out, false);
        }
        else
        {
            name.Print(outer, @out, false);
            PrintMain(@out, true);
        }
    }

    private void PrintMain(Output @out, bool includeFirst)
    {
        Decompiler.State result = d.Decompile();
        @out.Print("(");
        int start = includeFirst ? 0 : 1;
        if (function.numParams > start)
        {
            new VariableTarget(d.declList[start]).Print(d, @out, false);
            for (int i = start + 1; i < function.numParams; i++)
            {
                @out.Print(", ");
                new VariableTarget(d.declList[i]).Print(d, @out, false);
            }
        }
        if (function.vararg != 0)
        {
            Declaration namedVararg = null;
            if (function.header.version.varargtype.Get() == Version.VarArgType.NAMED &&
                function.numParams < d.declList.Length)
            {
                Declaration candidate = d.declList[function.numParams];
                if (candidate != null && candidate.namedVararg && !candidate.IsInternalName())
                {
                    namedVararg = candidate;
                }
            }

            if (function.numParams > start)
            {
                @out.Print(", ...");
            }
            else
            {
                @out.Print("...");
            }

            if (namedVararg != null)
            {
                new VariableTarget(namedVararg).Print(d, @out, false);
            }
        }
        @out.Print(")");
        @out.PrintLn();
        @out.Indent();
        d.Print(result, @out);
        @out.Dedent();
        @out.Print("end");
    }
}
