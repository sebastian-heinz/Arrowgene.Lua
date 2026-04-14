using System.Numerics;

namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.AssemblerConstant. A pending entry from the
/// <c>.constant</c> directive carrying both the discriminator and the
/// raw value (boolean, double, BigInteger, string, or NaN bit pattern)
/// before it gets converted to a versioned <c>LObject</c> at write time.
/// </summary>
internal sealed class AssemblerConstant
{
    public enum AssemblerConstantType
    {
        NIL,
        BOOLEAN,
        NUMBER,
        INTEGER,
        FLOAT,
        STRING,
        LONGSTRING,
        NAN,
    }

    public string name;
    public AssemblerConstantType type;

    public bool booleanValue;
    public double numberValue;
    public string stringValue;
    public BigInteger integerValue;
    public long nanValue;
}
