using System;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.DecompilerException. Thrown when the decompiler or
/// validator finds malformed bytecode that it cannot interpret.
/// </summary>
public sealed class DecompilerException : Exception
{
    public DecompilerException(Function f, int line, string message)
        : base(f.FullDisassemblerName() + " " + line + ": " + message)
    {
    }
}
