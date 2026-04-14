using System;

namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.AssemblerException. Tags a parse-time error
/// with the source line on which it was discovered so the assembler
/// front-end can surface a human-readable location alongside the
/// underlying message.
/// </summary>
public sealed class AssemblerException : Exception
{
    internal AssemblerException(int line, string msg)
        : base($"line {line}: {msg}")
    {
    }
}
