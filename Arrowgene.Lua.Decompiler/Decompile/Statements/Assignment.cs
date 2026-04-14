using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Targets;

namespace Arrowgene.Lua.Decompiler.Decompile.Statements;

/// <summary>
/// Port of unluac.decompile.statement.Assignment. The decompiler's catch-all
/// "this register/global/table/upvalue is being written" statement. Carries
/// parallel target/value/line lists so adjacent SETs can be coalesced into a
/// single multi-target assignment, tracks declaration vs. plain assignment,
/// and applies the function-sugar transform (<c>local function f() ... end</c>
/// and <c>function t.f(...) ... end</c>) when a closure is being assigned to
/// a function-name target.
/// </summary>
public class Assignment : Statement
{
    private readonly List<Target> targets = new List<Target>(5);
    private readonly List<Expression> values = new List<Expression>(5);
    private readonly List<int> lines = new List<int>(5);

    private bool allnil = true;
    private bool declare = false;
    private bool globaldeclare = false;
    private int register = -1;

    public Assignment()
    {
    }

    public Assignment(Target target, Expression value, int line)
    {
        targets.Add(target);
        values.Add(value);
        lines.Add(line);
        allnil = allnil && value.IsNil();
    }

    public override void Walk(Walker w)
    {
        w.VisitStatement(this);
        foreach (Target target in targets)
        {
            target.Walk(w);
        }
        foreach (Expression expression in values)
        {
            expression.Walk(w);
        }
    }

    public override bool BeginsWithParen()
    {
        return !declare && targets[0].BeginsWithParen();
    }

    public Target GetFirstTarget() => targets[0];

    public Target GetLastTarget() => targets[targets.Count - 1];

    public Expression GetFirstValue() => values[0];

    public void ReplaceLastValue(Expression value)
    {
        values[values.Count - 1] = value;
    }

    public int GetFirstLine() => lines[0];

    public bool AssignsTarget(Declaration decl)
    {
        foreach (Target target in targets)
        {
            if (target.IsDeclaration(decl)) return true;
        }
        return false;
    }

    public int GetArity() => targets.Count;

    public void AddFirst(Target target, Expression value, int line)
    {
        targets.Insert(0, target);
        values.Insert(0, value);
        lines.Insert(0, line);
        allnil = allnil && value.IsNil();
    }

    public void AddLast(Target target, Expression value, int line)
    {
        if (targets.Contains(target))
        {
            int idx = targets.IndexOf(target);
            targets.RemoveAt(idx);
            value = values[idx];
            values.RemoveAt(idx);
            lines.RemoveAt(idx);
        }
        int index = targets.Count;
        targets.Add(target);
        values.Insert(index, value);
        lines.Insert(index, line);
        allnil = allnil && value.IsNil();
    }

    public bool HasExcess() => values.Count > targets.Count;

    public void AddExcessValue(Expression value, int line, int register)
    {
        values.Add(value);
        lines.Add(line);
        allnil = false; // Excess can't be implicit
        int firstRegister = register - (values.Count - 1);
        if (this.register != -1 && this.register != firstRegister)
        {
            throw new System.InvalidOperationException();
        }
        this.register = firstRegister;
    }

    public int GetRegister(int index)
    {
        if (index < 0 || index >= values.Count)
        {
            throw new System.IndexOutOfRangeException();
        }
        if (register == -1)
        {
            throw new System.InvalidOperationException();
        }
        return register + index;
    }

    public int GetLastRegister() => GetRegister(values.Count - 1);

    public Expression GetValue(int target)
    {
        int index = 0;
        foreach (Target t in targets)
        {
            if (t.IsLocal() && t.GetIndex() == target)
            {
                return values[index];
            }
            index++;
        }
        throw new System.InvalidOperationException();
    }

    public void ReplaceValue(int target, Expression value)
    {
        int index = 0;
        foreach (Target t in targets)
        {
            if (t.IsLocal() && t.GetIndex() == target)
            {
                values[index] = value;
                return;
            }
            index++;
        }
        throw new System.InvalidOperationException();
    }

    public bool AssignListEquals(IList<Declaration> decls)
    {
        if (decls.Count != targets.Count) return false;
        foreach (Target target in targets)
        {
            bool found = false;
            foreach (Declaration decl in decls)
            {
                if (target.IsDeclaration(decl))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    public void Declare()
    {
        declare = true;
    }

    public void GlobalDeclare()
    {
        globaldeclare = true;
    }

    public bool IsDeclaration() => declare;

    public bool Assigns(Declaration decl)
    {
        foreach (Target target in targets)
        {
            if (target.IsDeclaration(decl)) return true;
        }
        return false;
    }

    public bool CanDeclare(IList<Declaration> locals)
    {
        foreach (Target target in targets)
        {
            bool isNewLocal = false;
            foreach (Declaration decl in locals)
            {
                if (target.IsDeclaration(decl))
                {
                    isNewLocal = true;
                    break;
                }
            }
            if (!isNewLocal) return false;
        }
        return true;
    }

    public override void Print(Decompiler d, Output @out)
    {
        if (targets.Count == 0) return;

        bool functionSugar = false;
        if (targets.Count == 1 && values.Count == 1 && values[0].IsClosure() && targets[0].IsFunctionName())
        {
            // must avoid sugar when it's a declaration that shadows a used upvalue or global
            // must use sugar when it's a declaration that is used as an upvalue
            // (by default, better to use sugar)
            functionSugar = true;
            Expression closure = values[0];

            if (!declare)
            {
                // sugar is always okay (there is no difference)
            }
            else if (targets[0].IsLocal() && closure.IsUpvalueOf(targets[0].GetIndex()))
            {
                // sugar must be used
            }
            else if (targets[0].IsLocal() && closure.IsNameExternallyBound(targets[0].GetLocalName()))
            {
                functionSugar = false;
            }
        }
        if (functionSugar)
        {
            @out.Paragraph();
        }
        if (declare)
        {
            @out.Print("local ");
        }
        else if (globaldeclare)
        {
            @out.Print("global ");
        }
        if (!functionSugar)
        {
            targets[0].Print(d, @out, declare);
            for (int i = 1; i < targets.Count; i++)
            {
                @out.Print(", ");
                targets[i].Print(d, @out, declare);
            }
            if (!declare || !allnil)
            {
                @out.Print(" = ");

                var expressions = new LinkedList<Expression>();

                int size = values.Count;
                if (size >= 2 && values[size - 1].IsNil() && (lines[size - 1] == values[size - 1].GetConstantLine() || values[size - 1].GetConstantLine() == -1))
                {
                    foreach (Expression v in values)
                    {
                        expressions.AddLast(v);
                    }
                }
                else
                {
                    bool include = false;
                    for (int i = size - 1; i >= 0; i--)
                    {
                        Expression value = values[i];
                        if (include || !value.IsNil() || value.GetConstantIndex() != -1)
                        {
                            include = true;
                        }
                        if (include)
                        {
                            expressions.AddFirst(value);
                        }
                    }

                    if (expressions.Count == 0 && !declare)
                    {
                        foreach (Expression v in values)
                        {
                            expressions.AddLast(v);
                        }
                    }
                }

                var asList = new List<Expression>(expressions);
                Expression.PrintSequence(d, @out, asList, false, targets.Count > expressions.Count);
            }
        }
        else
        {
            values[0].PrintClosure(d, @out, targets[0]);
            @out.Paragraph();
        }
        if (comment != null)
        {
            @out.Print(" -- ");
            @out.Print(comment);
        }
    }
}
