using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Decompile.Targets;

namespace Arrowgene.Lua.Decompiler.Decompile.Operations;

/// <summary>
/// Port of unluac.decompile.operation.GlobalSet. A SETGLOBAL instruction's
/// effect: emits a single <see cref="Assignment"/> with a
/// <see cref="GlobalTarget"/>.
/// </summary>
public class GlobalSet : Operation
{
    private readonly ConstantExpression global;
    private readonly Expression value;

    public GlobalSet(int line, ConstantExpression global, Expression value) : base(line)
    {
        this.global = global;
        this.value = value;
    }

    public override IList<Statement> Process(Registers r, Block block)
    {
        return new List<Statement> { new Assignment(new GlobalTarget(global), value, line) };
    }
}
