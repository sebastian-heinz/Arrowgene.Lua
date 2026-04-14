namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.AssemblerLocal. Pending entry from the
/// <c>.local</c> directive: a debug-only local declaration with a
/// name and the [begin, end) program-counter range over which the
/// local is in scope.
/// </summary>
internal sealed class AssemblerLocal
{
    public string name;
    public int begin;
    public int end;
}
