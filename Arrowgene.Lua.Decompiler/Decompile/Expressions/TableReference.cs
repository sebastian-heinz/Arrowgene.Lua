using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Expressions;

/// <summary>
/// Port of unluac.decompile.expression.TableReference. Represents either a
/// <c>t.x</c> / <c>t[k]</c> indexing or, when the table is the implicit
/// <c>_ENV</c> upvalue/local, a bare global access (with shadowing checks).
/// </summary>
public class TableReference : Expression
{
    private readonly Registers r;
    private readonly int line;
    private readonly Expression table;
    private readonly Expression index;

    public TableReference(Registers r, int line, Expression table, Expression index)
        : base(PRECEDENCE_ATOMIC)
    {
        this.r = r;
        this.line = line;
        this.table = table;
        this.index = index;
    }

    public override void Walk(Walker w)
    {
        w.VisitExpression(this);
        table.Walk(w);
        index.Walk(w);
    }

    public override int GetConstantIndex()
    {
        int t = table.GetConstantIndex();
        int i = index.GetConstantIndex();
        return t > i ? t : i;
    }

    private static bool IsUpvalueOf(LFunction function, string id)
    {
        for (int i = 0; i < function.upvalues.Length; i++)
        {
            LUpvalue upvalue = function.upvalues[i];
            if (upvalue.name == id)
            {
                return true;
            }
        }
        return false;
    }

    public override void Print(Decompiler d, Output @out)
    {
        bool isGlobal = table.IsEnvironmentTable(d) && index.IsIdentifier();
        if (isGlobal)
        {
            string name = index.AsName();
            if (r.IsLocalName(name, line) || IsUpvalueOf(d.function, name) || d.boundNames.Contains(name))
            {
                // _ENV lookup reference is shadowed; need explicit _ENV
                isGlobal = false;
            }
        }
        if (!isGlobal)
        {
            if (table.IsUngrouped())
            {
                @out.Print("(");
                table.Print(d, @out);
                @out.Print(")");
            }
            else
            {
                table.Print(d, @out);
            }
        }
        if (index.IsIdentifier())
        {
            if (!isGlobal)
            {
                @out.Print(".");
            }
            @out.Print(index.AsName());
        }
        else
        {
            @out.Print("[");
            index.PrintBraced(d, @out);
            @out.Print("]");
        }
    }

    public override bool IsDotChain() => index.IsIdentifier() && table.IsDotChain();

    public override bool IsMemberAccess() => index.IsIdentifier();

    public override bool BeginsWithParen() => table.IsUngrouped() || table.BeginsWithParen();

    public override Expression GetTable() => table;

    public override string GetField() => index.AsName();
}
