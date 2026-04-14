using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Decompile.Targets;

namespace Arrowgene.Lua.Decompiler.Decompile.Operations;

/// <summary>
/// Port of unluac.decompile.operation.TableSet. A SETTABLE/SETLIST effect.
/// When the target is a still-being-built <see cref="TableLiteral"/> with
/// debugging info, folds the entry into the literal; otherwise emits an
/// <see cref="Assignment"/> with a <see cref="TableTarget"/>.
/// </summary>
public class TableSet : Operation
{
    private readonly Expression table;
    private readonly Expression index;
    private readonly Expression value;
    private readonly bool isTable;
    private readonly int timestamp;

    public TableSet(int line, Expression table, Expression index, Expression value, bool isTable, int timestamp) : base(line)
    {
        this.table = table;
        this.index = index;
        this.value = value;
        this.isTable = isTable;
        this.timestamp = timestamp;
    }

    public override IList<Statement> Process(Registers r, Block block)
    {
        // .IsTableLiteral() is sufficient when there is debugging info
        if (!r.isNoDebug && table.IsTableLiteral() && (value.IsMultiple() || table.IsNewEntryAllowed()))
        {
            table.AddEntry(new TableLiteral.Entry(index, value, !isTable, timestamp));
            return new List<Statement>();
        }
        return new List<Statement> { new Assignment(new TableTarget(r, line, table, index), value, line) };
    }
}
