using Arrowgene.Lua.Decompiler.Decompile;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.LHeader. Data holder for everything parsed from a
/// compiled chunk's on-wire header.
/// </summary>
public sealed class LHeader : BObject
{
    public enum LEndianness
    {
        BIG,
        LITTLE,
    }

    public readonly int format;
    public readonly LEndianness endianness;
    public readonly BIntegerType integer;
    public readonly BIntegerType vinteger;
    public readonly BIntegerType sizeT;
    public readonly LBooleanType @bool;
    public readonly LNumberType number;
    public readonly LNumberType linteger;
    public readonly LNumberType lfloat;
    public readonly LStringType @string;
    public readonly LConstantType constant;
    public readonly LAbsLineInfoType abslineinfo;
    public readonly LLocalType local;
    public readonly LUpvalueType upvalue;
    public readonly LFunctionType function;
    public readonly CodeExtract extractor;

    public LHeader(int format, LEndianness endianness, BIntegerType integer, BIntegerType vinteger,
                   BIntegerType sizeT, LBooleanType @bool, LNumberType number, LNumberType linteger,
                   LNumberType lfloat, LStringType @string, LConstantType constant,
                   LAbsLineInfoType abslineinfo, LLocalType local, LUpvalueType upvalue,
                   LFunctionType function, CodeExtract extractor)
    {
        this.format = format;
        this.endianness = endianness;
        this.integer = integer;
        this.vinteger = vinteger;
        this.sizeT = sizeT;
        this.@bool = @bool;
        this.number = number;
        this.linteger = linteger;
        this.lfloat = lfloat;
        this.@string = @string;
        this.constant = constant;
        this.abslineinfo = abslineinfo;
        this.local = local;
        this.upvalue = upvalue;
        this.function = function;
        this.extractor = extractor;
    }
}
