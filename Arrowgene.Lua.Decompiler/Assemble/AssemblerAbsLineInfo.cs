namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.AssemblerAbsLineInfo. Pending entry from
/// the <c>.abslineinfo</c> directive: a (pc, line) pair that, in
/// Lua 5.4+, anchors the relative line-info delta sequence.
/// </summary>
internal sealed class AssemblerAbsLineInfo
{
    public int pc;
    public int line;
}
