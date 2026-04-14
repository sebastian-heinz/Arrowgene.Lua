using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;

namespace Arrowgene.Lua.Decompiler.Decompile.Operations;

/// <summary>
/// Port of unluac.decompile.operation.CallOperation. Wraps a
/// <see cref="FunctionCall"/> emitted from a CALL/TAILCALL instruction
/// whose result is discarded, producing a single
/// <see cref="FunctionCallStatement"/>.
/// </summary>
public class CallOperation : Operation
{
    private readonly FunctionCall call;

    public CallOperation(int line, FunctionCall call) : base(line)
    {
        this.call = call;
    }

    public override IList<Statement> Process(Registers r, Block block)
    {
        return new List<Statement> { new FunctionCallStatement(call) };
    }
}
