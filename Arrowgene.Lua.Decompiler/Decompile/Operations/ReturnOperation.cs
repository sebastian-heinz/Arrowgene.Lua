using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;

namespace Arrowgene.Lua.Decompiler.Decompile.Operations;

/// <summary>
/// Port of unluac.decompile.operation.ReturnOperation. A RETURN
/// instruction's effect: produces a single <see cref="Return"/>
/// statement carrying the configured value list.
/// </summary>
public class ReturnOperation : Operation
{
    private readonly Expression[] values;

    public ReturnOperation(int line, Expression value) : base(line)
    {
        values = new Expression[1];
        values[0] = value;
    }

    public ReturnOperation(int line, Expression[] values) : base(line)
    {
        this.values = values;
    }

    public override IList<Statement> Process(Registers r, Block block)
    {
        return new List<Statement> { new Return(values) };
    }
}
