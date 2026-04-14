using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;

namespace Arrowgene.Lua.Decompiler.Decompile.Operations;

/// <summary>
/// Port of unluac.decompile.operation.MultipleRegisterSet. A range of
/// registers all set to the same expression value (used for multi-return
/// CALL/VARARG destinations). Coalesces live locals into a single
/// <see cref="Assignment"/>.
/// </summary>
public class MultipleRegisterSet : Operation
{
    public readonly int registerFirst;
    public readonly int registerLast;
    public readonly Expression value;

    public MultipleRegisterSet(int line, int registerFirst, int registerLast, Expression value) : base(line)
    {
        this.registerFirst = registerFirst;
        this.registerLast = registerLast;
        this.value = value;
    }

    public override IList<Statement> Process(Registers r, Block block)
    {
        int count = 0;
        Assignment assignment = new Assignment();
        for (int register = registerFirst; register <= registerLast; register++)
        {
            r.SetValue(register, line, value);
            if (r.IsAssignable(register, line))
            {
                assignment.AddLast(r.GetTarget(register, line), value, line);
                count++;
            }
        }
        if (count > 0)
        {
            return new List<Statement> { assignment };
        }
        return new List<Statement>();
    }
}
