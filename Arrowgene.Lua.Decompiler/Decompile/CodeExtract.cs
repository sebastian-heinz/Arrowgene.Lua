using System;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.CodeExtract.
/// Carries the opcode/field bit layout for a Lua version and provides Field
/// objects that extract A/B/C/Bx/sBx/Ax/sJ operand values from a 32-bit
/// instruction word.
/// </summary>
public sealed class CodeExtract
{
    public const int BITFIELD_OPCODE = 1;
    public const int BITFIELD_A = 2;
    public const int BITFIELD_B = 4;
    public const int BITFIELD_C = 8;
    public const int BITFIELD_K = 16;

    public const int BITFIELD_BX = BITFIELD_B | BITFIELD_C | BITFIELD_K;
    public const int BITFIELD_AX = BITFIELD_A | BITFIELD_B | BITFIELD_C | BITFIELD_K;
    public const int BITFIELD_X = BITFIELD_OPCODE | BITFIELD_A | BITFIELD_B | BITFIELD_C | BITFIELD_K;

    public sealed class Field
    {
        public readonly int slot;
        public readonly int size;
        private readonly int shift;
        private readonly int mask;
        private readonly int offset;

        public Field(int slot, int size, int shift) : this(slot, size, shift, 0) { }

        public Field(int slot, int size, int shift, int offset)
        {
            this.slot = slot;
            this.size = size;
            this.shift = shift;
            this.mask = SizeToMask(size);
            this.offset = offset;
        }

        public int Extract(int codepoint)
        {
            // Java >>> unsigned right shift.
            return (int)(((uint)codepoint >> shift) & (uint)mask) - offset;
        }

        public bool Check(int x)
        {
            return ((x + offset) & ~mask) == 0;
        }

        public int Encode(int x)
        {
            return (x + offset) << shift;
        }

        public int Clear(int codepoint)
        {
            return codepoint & ~(mask << shift);
        }

        public int Max()
        {
            return mask - offset;
        }

        public int Mask()
        {
            return mask << shift;
        }

        public string DefaultName()
        {
            switch (slot)
            {
                case BITFIELD_A: return "a";
                case BITFIELD_B: return "b";
                case BITFIELD_C: return "c";
                case BITFIELD_K: return "k";
                default: throw new InvalidOperationException();
            }
        }
    }

    public readonly Field op;
    public readonly Field A;
    public readonly Field B;
    public readonly Field C;
    public readonly Field k;
    public readonly Field vB;
    public readonly Field vC;
    public readonly Field Ax;
    public readonly Field sJ;
    public readonly Field Bx;
    public readonly Field sBx;
    public readonly Field x;

    private readonly int rk_offset;

    public CodeExtract(Version version, int sizeOp, int sizeA, int sizeB, int sizeC)
    {
        switch (version.instructionformat.Get())
        {
            case Version.InstructionFormat.LUA50:
                op = new Field(BITFIELD_OPCODE, sizeOp, 0);
                A = new Field(BITFIELD_A, sizeA, sizeB + sizeC + sizeOp);
                B = new Field(BITFIELD_B, sizeB, sizeB + sizeOp);
                C = new Field(BITFIELD_C, sizeC, sizeOp);
                vB = null;
                vC = null;
                k = null;
                Ax = null;
                sJ = null;
                Bx = new Field(BITFIELD_BX, sizeB + sizeC, sizeOp);
                sBx = new Field(BITFIELD_BX, sizeB + sizeC, sizeOp, SizeToMask(sizeB + sizeC) / 2);
                x = new Field(BITFIELD_X, 32, 0);
                break;
            case Version.InstructionFormat.LUA51:
                op = new Field(BITFIELD_OPCODE, 6, 0);
                A = new Field(BITFIELD_A, 8, 6);
                B = new Field(BITFIELD_B, 9, 23);
                C = new Field(BITFIELD_C, 9, 14);
                vB = null;
                vC = null;
                k = null;
                Ax = new Field(BITFIELD_AX, 26, 6);
                sJ = null;
                Bx = new Field(BITFIELD_BX, 18, 14);
                sBx = new Field(BITFIELD_BX, 18, 14, 131071);
                x = new Field(BITFIELD_X, 32, 0);
                break;
            case Version.InstructionFormat.LUA54:
                op = new Field(BITFIELD_OPCODE, 7, 0);
                A = new Field(BITFIELD_A, 8, 7);
                B = new Field(BITFIELD_B, 8, 16);
                C = new Field(BITFIELD_C, 8, 24);
                vB = new Field(BITFIELD_B, 6, 16);
                vC = new Field(BITFIELD_C, 10, 22);
                k = new Field(BITFIELD_K, 1, 15);
                Ax = new Field(BITFIELD_AX, 25, 7);
                sJ = new Field(BITFIELD_AX, 25, 7, (1 << 24) - 1);
                Bx = new Field(BITFIELD_BX, 17, 15);
                sBx = new Field(BITFIELD_BX, 17, 15, (1 << 16) - 1);
                x = new Field(BITFIELD_X, 32, 0);
                break;
            default:
                throw new InvalidOperationException();
        }
        int? rkOff = version.rkoffset.Get();
        this.rk_offset = rkOff ?? -1;
    }

    public bool IsK(int field)
    {
        return field >= rk_offset;
    }

    public int GetK(int field)
    {
        return field - rk_offset;
    }

    public int EncodeK(int constant)
    {
        return constant + rk_offset;
    }

    public Field GetField(OperandFormat.Field f)
    {
        switch (f)
        {
            case OperandFormat.Field.A: return A;
            case OperandFormat.Field.B: return B;
            case OperandFormat.Field.C: return C;
            case OperandFormat.Field.k: return k;
            case OperandFormat.Field.vB: return vB;
            case OperandFormat.Field.vC: return vC;
            case OperandFormat.Field.Ax: return Ax;
            case OperandFormat.Field.sJ: return sJ;
            case OperandFormat.Field.Bx: return Bx;
            case OperandFormat.Field.sBx: return sBx;
            case OperandFormat.Field.x: return x;
            default: throw new InvalidOperationException("Unhandled field: " + f);
        }
    }

    public Field GetFieldForSlot(int slot)
    {
        if ((slot & BITFIELD_OPCODE) == 0) return op;
        if ((slot & BITFIELD_A) == 0) return A;
        if ((slot & BITFIELD_B) == 0) return B;
        if ((slot & BITFIELD_C) == 0) return C;
        if ((slot & BITFIELD_K) == 0) return k; // may be null, okay when checking last
        return null;
    }

    private static int SizeToMask(int size)
    {
        return (int)((1L << size) - 1);
    }
}
