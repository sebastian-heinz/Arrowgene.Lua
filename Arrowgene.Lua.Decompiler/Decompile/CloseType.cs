namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.CloseType. Discriminates the closure-closing
/// instruction style emitted at the end of a block:
/// <list type="bullet">
///   <item><description><c>NONE</c> - no close instruction</description></item>
///   <item><description><c>CLOSE</c> - the legacy CLOSE opcode (Lua 5.2/5.3)</description></item>
///   <item><description><c>CLOSE54</c> - the 5.4+ rewrite</description></item>
///   <item><description><c>JMP</c> - close encoded in the JMP A operand (Lua 5.2)</description></item>
/// </list>
/// </summary>
public enum CloseType
{
    NONE,
    CLOSE,
    CLOSE54,
    JMP,
}
