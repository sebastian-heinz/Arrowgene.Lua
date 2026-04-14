using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;

namespace Arrowgene.Lua.Decompiler.Decompile.Operations;

/// <summary>
/// Port of unluac.decompile.operation.RegisterSet. A single-register
/// store. Always updates the registers' value tracking; only emits an
/// <see cref="Assignment"/> when the destination register is a live
/// local at this line.
/// </summary>
public class RegisterSet : Operation
{
    public readonly int register;
    public readonly Expression value;

    public RegisterSet(int line, int register, Expression value) : base(line)
    {
        this.register = register;
        this.value = value;
    }

    public override IList<Statement> Process(Registers r, Block block)
    {
        r.SetValue(register, line, value);
        if (r.IsAssignable(register, line))
        {
            return new List<Statement> { new Assignment(r.GetTarget(register, line), value, line) };
        }
        return new List<Statement>();
    }
}
