using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Statements;

namespace Arrowgene.Lua.Decompiler.Decompile.Operations;

/// <summary>
/// Port of unluac.decompile.operation.Operation. The abstract base for
/// every per-instruction effect the decompiler emits while walking the
/// bytecode: register stores, global stores, table stores, calls, returns,
/// nil loads. Each Operation knows the bytecode line that produced it and
/// can yield a list of <see cref="Statement"/>s when processed against a
/// <see cref="Registers"/> view and the enclosing <see cref="Block"/>.
/// </summary>
public abstract class Operation
{
    public readonly int line;

    protected Operation(int line)
    {
        this.line = line;
    }

    public abstract IList<Statement> Process(Registers r, Block block);
}
