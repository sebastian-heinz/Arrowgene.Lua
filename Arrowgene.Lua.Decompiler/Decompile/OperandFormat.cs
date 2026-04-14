namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.OperandFormat. Declarative description of a single
/// opcode operand: which bit-field it lives in, how it should be rendered, and
/// an optional numeric offset (used by jump offset adjustments).
/// </summary>
public sealed class OperandFormat
{
    public enum Field
    {
        A,
        B,
        C,
        k,
        vB,
        vC,
        Ax,
        sJ,
        Bx,
        sBx,
        x,
    }

    public enum Format
    {
        RAW,
        REGISTER,
        UPVALUE,
        REGISTER_K,
        REGISTER_K54,
        CONSTANT,
        CONSTANT_INTEGER,
        CONSTANT_STRING,
        FUNCTION,
        IMMEDIATE_INTEGER,
        IMMEDIATE_SIGNED_INTEGER,
        IMMEDIATE_FLOAT,
        JUMP,
        JUMP_NEGATIVE,
    }

    public readonly Field field;
    public readonly Format format;
    public readonly int offset;

    private OperandFormat(Field field, Format format) : this(field, format, 0) { }

    private OperandFormat(Field field, Format format, int offset)
    {
        this.field = field;
        this.format = format;
        this.offset = offset;
    }

    public static readonly OperandFormat A     = new OperandFormat(Field.A,  Format.RAW);
    public static readonly OperandFormat AR    = new OperandFormat(Field.A,  Format.REGISTER);
    public static readonly OperandFormat AU    = new OperandFormat(Field.A,  Format.UPVALUE);
    public static readonly OperandFormat B     = new OperandFormat(Field.B,  Format.RAW);
    public static readonly OperandFormat BR    = new OperandFormat(Field.B,  Format.REGISTER);
    public static readonly OperandFormat BRK   = new OperandFormat(Field.B,  Format.REGISTER_K);
    public static readonly OperandFormat BK    = new OperandFormat(Field.B,  Format.CONSTANT);
    public static readonly OperandFormat BKS   = new OperandFormat(Field.B,  Format.CONSTANT_STRING);
    public static readonly OperandFormat BI    = new OperandFormat(Field.B,  Format.IMMEDIATE_INTEGER);
    public static readonly OperandFormat BsI   = new OperandFormat(Field.B,  Format.IMMEDIATE_SIGNED_INTEGER);
    public static readonly OperandFormat BU    = new OperandFormat(Field.B,  Format.UPVALUE);
    public static readonly OperandFormat vB    = new OperandFormat(Field.vB, Format.RAW);
    public static readonly OperandFormat C     = new OperandFormat(Field.C,  Format.RAW);
    public static readonly OperandFormat CR    = new OperandFormat(Field.C,  Format.REGISTER);
    public static readonly OperandFormat CRK   = new OperandFormat(Field.C,  Format.REGISTER_K);
    public static readonly OperandFormat CRK54 = new OperandFormat(Field.C,  Format.REGISTER_K54);
    public static readonly OperandFormat CK    = new OperandFormat(Field.C,  Format.CONSTANT);
    public static readonly OperandFormat CKI   = new OperandFormat(Field.C,  Format.CONSTANT_INTEGER);
    public static readonly OperandFormat CKS   = new OperandFormat(Field.C,  Format.CONSTANT_STRING);
    public static readonly OperandFormat CI    = new OperandFormat(Field.C,  Format.IMMEDIATE_INTEGER);
    public static readonly OperandFormat CsI   = new OperandFormat(Field.C,  Format.IMMEDIATE_SIGNED_INTEGER);
    public static readonly OperandFormat vC    = new OperandFormat(Field.vC, Format.RAW);
    public static readonly OperandFormat k     = new OperandFormat(Field.k,  Format.RAW);
    public static readonly OperandFormat Ax    = new OperandFormat(Field.Ax, Format.RAW);
    public static readonly OperandFormat sJ    = new OperandFormat(Field.sJ, Format.JUMP);
    public static readonly OperandFormat Bx    = new OperandFormat(Field.Bx, Format.RAW);
    public static readonly OperandFormat BxK   = new OperandFormat(Field.Bx, Format.CONSTANT);
    public static readonly OperandFormat BxJ   = new OperandFormat(Field.Bx, Format.JUMP);
    public static readonly OperandFormat BxJ1  = new OperandFormat(Field.Bx, Format.JUMP, 1);
    public static readonly OperandFormat BxJn  = new OperandFormat(Field.Bx, Format.JUMP_NEGATIVE);
    public static readonly OperandFormat BxF   = new OperandFormat(Field.Bx, Format.FUNCTION);
    public static readonly OperandFormat sBxJ  = new OperandFormat(Field.sBx, Format.JUMP);
    public static readonly OperandFormat sBxI  = new OperandFormat(Field.sBx, Format.IMMEDIATE_INTEGER);
    public static readonly OperandFormat sBxF  = new OperandFormat(Field.sBx, Format.IMMEDIATE_FLOAT);
    public static readonly OperandFormat x     = new OperandFormat(Field.x,  Format.RAW);
}
