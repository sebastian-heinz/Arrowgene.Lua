namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.AssemblerLabel. A named jump target gathered
/// from a <c>.label</c> directive together with the index in the
/// in-progress code list at which it was declared. The fixup pass
/// resolves every pending JumpFixup to the matching label by name.
/// </summary>
internal sealed class AssemblerLabel
{
    public string name;
    public int code_index;
}
