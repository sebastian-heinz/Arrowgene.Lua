using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Targets;

/// <summary>
/// Port of unluac.decompile.target.TableTarget. An assignment destination
/// that resolves to a table-field store (<c>t.x = ...</c> or
/// <c>t[k] = ...</c>). Delegates printing to a transient
/// <see cref="TableReference"/> so the same shadowing/_ENV logic applies.
/// </summary>
public class TableTarget : Target
{
    private readonly Registers r;
    private readonly int line;
    private readonly Expression table;
    private readonly Expression index;

    public TableTarget(Registers r, int line, Expression table, Expression index)
    {
        this.r = r;
        this.line = line;
        this.table = table;
        this.index = index;
    }

    public override void Walk(Walker w)
    {
        table.Walk(w);
        index.Walk(w);
    }

    public override void Print(Decompiler d, Output @out, bool declare)
    {
        new TableReference(r, line, table, index).Print(d, @out);
    }

    public override void PrintMethod(Decompiler d, Output @out)
    {
        table.Print(d, @out);
        @out.Print(":");
        @out.Print(index.AsName());
    }

    public override bool IsFunctionName()
    {
        if (!index.IsIdentifier()) return false;
        if (!table.IsDotChain()) return false;
        return true;
    }

    public override bool BeginsWithParen()
    {
        return table.IsUngrouped() || table.BeginsWithParen();
    }
}
