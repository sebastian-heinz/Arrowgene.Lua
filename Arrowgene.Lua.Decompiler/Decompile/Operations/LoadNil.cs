using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;

namespace Arrowgene.Lua.Decompiler.Decompile.Operations;

/// <summary>
/// Port of unluac.decompile.operation.LoadNil. A LOADNIL/LOADNIL52
/// instruction's effect: assigns nil to a contiguous range of registers.
/// Coalesces adjacent live-local destinations into a single declaration
/// when they share the same scope-end (matching the Lua compiler's
/// <c>local x, y, z = nil</c> emission pattern).
/// </summary>
public class LoadNil : Operation
{
    public readonly int registerFirst;
    public readonly int registerLast;

    public LoadNil(int line, int registerFirst, int registerLast) : base(line)
    {
        this.registerFirst = registerFirst;
        this.registerLast = registerLast;
    }

    public override IList<Statement> Process(Registers r, Block block)
    {
        var assignments = new List<Statement>(registerLast - registerFirst + 1);
        Expression nil = ConstantExpression.CreateNil(line);
        Assignment declare = null;
        int scopeEnd = -1;
        for (int register = registerFirst; register <= registerLast; register++)
        {
            if (r.IsAssignable(register, line))
            {
                scopeEnd = r.GetDeclaration(register, line).end;
            }
        }
        for (int register = registerFirst; register <= registerLast; register++)
        {
            r.SetValue(register, line, nil);
            if (r.IsAssignable(register, line)
                && r.GetDeclaration(register, line).end == scopeEnd
                && register >= block.closeRegister)
            {
                if (r.GetDeclaration(register, line).begin == line)
                {
                    if (declare == null)
                    {
                        declare = new Assignment();
                        assignments.Add(declare);
                    }
                    declare.AddLast(r.GetTarget(register, line), nil, line);
                }
                else
                {
                    assignments.Add(new Assignment(r.GetTarget(register, line), nil, line));
                }
            }
        }
        return assignments;
    }
}
