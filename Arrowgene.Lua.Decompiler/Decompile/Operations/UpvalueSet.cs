using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Decompile.Targets;

namespace Arrowgene.Lua.Decompiler.Decompile.Operations;

/// <summary>
/// Port of unluac.decompile.operation.UpvalueSet. A SETUPVAL instruction's
/// effect: emits an <see cref="Assignment"/> targeting the named upvalue.
/// </summary>
public class UpvalueSet : Operation
{
    private readonly UpvalueTarget target;
    private readonly Expression value;

    public UpvalueSet(int line, string upvalue, Expression value) : base(line)
    {
        target = new UpvalueTarget(upvalue);
        this.value = value;
    }

    public override IList<Statement> Process(Registers r, Block block)
    {
        return new List<Statement> { new Assignment(target, value, line) };
    }
}
