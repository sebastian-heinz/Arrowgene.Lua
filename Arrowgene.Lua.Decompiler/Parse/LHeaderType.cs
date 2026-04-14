using System;
using System.Collections.Generic;
using System.IO;
using Arrowgene.Lua.Decompiler.Assemble;
using Arrowgene.Lua.Decompiler.Decompile;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.LHeaderType and its Lua-version subclasses
/// (50/51/52/53/54/55). Parses the per-chunk header block after the 0x1B4C7561
/// signature and builds an <see cref="LHeader"/>.
/// </summary>
public abstract class LHeaderType : BObjectType<LHeader>
{
    public static readonly LHeaderType TYPE50 = new LHeaderType50();
    public static readonly LHeaderType TYPE51 = new LHeaderType51();
    public static readonly LHeaderType TYPE52 = new LHeaderType52();
    public static readonly LHeaderType TYPE53 = new LHeaderType53();
    public static readonly LHeaderType TYPE54 = new LHeaderType54();
    public static readonly LHeaderType TYPE55 = new LHeaderType55();

    public static LHeaderType Get(Version.HeaderType type)
    {
        return type switch
        {
            Version.HeaderType.LUA50 => TYPE50,
            Version.HeaderType.LUA51 => TYPE51,
            Version.HeaderType.LUA52 => TYPE52,
            Version.HeaderType.LUA53 => TYPE53,
            Version.HeaderType.LUA54 => TYPE54,
            Version.HeaderType.LUA55 => TYPE55,
            _ => throw new InvalidOperationException(),
        };
    }

    protected static readonly byte[] luacTail = new byte[]
    {
        0x19, 0x93, 0x0D, 0x0A, 0x1A, 0x0A,
    };

    protected const int TEST_INTEGER = 0x5678;
    protected const double TEST_FLOAT = 370.5;
    protected const int TEST_INSTRUCTION = 0x12345678;

    protected sealed class LHeaderParseState
    {
        public BIntegerType integer;
        public BIntegerType vinteger;
        public BIntegerType sizeT;
        public LNumberType number;
        public LNumberType linteger;
        public LNumberType lfloat;

        public int format;
        public LHeader.LEndianness endianness;
        public bool endiannessSet;

        public int lNumberSize;
        public bool lNumberIntegrality;

        public int lIntegerSize;
        public int lFloatSize;

        public int sizeOp;
        public int sizeA;
        public int sizeB;
        public int sizeC;
    }

    public override LHeader Parse(LuaByteBuffer buffer, BHeader header)
    {
        Version version = header.version;
        LHeaderParseState s = new LHeaderParseState();
        ParseMain(buffer, header, s);
        BIntegerType vinteger = s.vinteger != null ? s.vinteger : s.integer;
        LBooleanType @bool = new LBooleanType();
        LStringType @string = version.GetLStringType();
        LConstantType constant = version.GetLConstantType();
        LAbsLineInfoType abslineinfo = new LAbsLineInfoType();
        LLocalType local = new LLocalType();
        LUpvalueType upvalue = version.GetLUpvalueType();
        LFunctionType function = version.GetLFunctionType();
        CodeExtract extract = new CodeExtract(header.version, s.sizeOp, s.sizeA, s.sizeB, s.sizeC);
        return new LHeader(
            s.format, s.endianness, s.integer, vinteger, s.sizeT,
            @bool, s.number, s.linteger, s.lfloat, @string, constant,
            abslineinfo, local, upvalue,
            function, extract);
    }

    public abstract List<Directive> GetDirectives();

    protected abstract void ParseMain(LuaByteBuffer buffer, BHeader header, LHeaderParseState s);

    protected void ParseFormat(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int format = 0xFF & buffer.Get();
        if (format != 0)
        {
            throw new InvalidOperationException("The input chunk reports a non-standard lua format: " + format);
        }
        s.format = format;
        if (header.debug)
        {
            Console.WriteLine("-- format: " + format);
        }
    }

    protected void WriteFormat(Stream @out, BHeader header, LHeader obj)
    {
        @out.WriteByte((byte)obj.format);
    }

    protected void ParseEndianness(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int endianness = 0xFF & buffer.Get();
        switch (endianness)
        {
            case 0:
                s.endianness = LHeader.LEndianness.BIG;
                s.endiannessSet = true;
                buffer.Order(LuaByteBuffer.ByteOrder.BigEndian);
                break;
            case 1:
                s.endianness = LHeader.LEndianness.LITTLE;
                s.endiannessSet = true;
                buffer.Order(LuaByteBuffer.ByteOrder.LittleEndian);
                break;
            default:
                throw new InvalidOperationException("The input chunk reports an invalid endianness: " + endianness);
        }
        if (header.debug)
        {
            Console.WriteLine("-- endianness: " + endianness + (endianness == 0 ? " (big)" : " (little)"));
        }
    }

    protected void WriteEndianness(Stream @out, BHeader header, LHeader obj)
    {
        int value;
        switch (obj.endianness)
        {
            case LHeader.LEndianness.BIG:
                value = 0;
                break;
            case LHeader.LEndianness.LITTLE:
                value = 1;
                break;
            default:
                throw new InvalidOperationException();
        }
        @out.WriteByte((byte)value);
    }

    protected void ParseIntSize(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int intSize = 0xFF & buffer.Get();
        if (header.debug)
        {
            Console.WriteLine("-- int size: " + intSize);
        }
        s.integer = new BIntegerType50(true, intSize, header.version.allownegativeint.Get());
    }

    protected void WriteIntSize(Stream @out, BHeader header, LHeader obj)
    {
        @out.WriteByte((byte)obj.integer.GetSize());
    }

    protected void ParseSizeTSize(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int sizeTSize = 0xFF & buffer.Get();
        if (header.debug)
        {
            Console.WriteLine("-- size_t size: " + sizeTSize);
        }
        s.sizeT = new BIntegerType50(false, sizeTSize, false);
    }

    protected void WriteSizeTSize(Stream @out, BHeader header, LHeader obj)
    {
        @out.WriteByte((byte)obj.sizeT.GetSize());
    }

    protected void ParseInstructionSize(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int instructionSize = 0xFF & buffer.Get();
        if (header.debug)
        {
            Console.WriteLine("-- instruction size: " + instructionSize);
        }
        if (instructionSize != 4)
        {
            throw new InvalidOperationException("The input chunk reports an unsupported instruction size: " + instructionSize + " bytes");
        }
    }

    protected void WriteInstructionSize(Stream @out, BHeader header, LHeader obj)
    {
        @out.WriteByte(4);
    }

    protected void ParseNumberSize(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int lNumberSize = 0xFF & buffer.Get();
        if (header.debug)
        {
            Console.WriteLine("-- Lua number size: " + lNumberSize);
        }
        s.lNumberSize = lNumberSize;
    }

    protected void WriteNumberSize(Stream @out, BHeader header, LHeader obj)
    {
        @out.WriteByte((byte)obj.number.size);
    }

    protected void ParseNumberIntegrality(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int lNumberIntegralityCode = 0xFF & buffer.Get();
        if (header.debug)
        {
            Console.WriteLine("-- Lua number integrality code: " + lNumberIntegralityCode);
        }
        if (lNumberIntegralityCode > 1)
        {
            throw new InvalidOperationException("The input chunk reports an invalid code for lua number integrality: " + lNumberIntegralityCode);
        }
        s.lNumberIntegrality = (lNumberIntegralityCode == 1);
    }

    protected void WriteNumberIntegrality(Stream @out, BHeader header, LHeader obj)
    {
        @out.WriteByte((byte)(obj.number.integral ? 1 : 0));
    }

    protected void ParseIntegerSize(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int lIntegerSize = 0xFF & buffer.Get();
        if (header.debug)
        {
            Console.WriteLine("-- Lua integer size: " + lIntegerSize);
        }
        if (lIntegerSize < 2)
        {
            throw new InvalidOperationException("The input chunk reports an integer size that is too small: " + lIntegerSize);
        }
        s.lIntegerSize = lIntegerSize;
    }

    protected void ParseFloatSize(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int lFloatSize = 0xFF & buffer.Get();
        if (header.debug)
        {
            Console.WriteLine("-- Lua float size: " + lFloatSize);
        }
        s.lFloatSize = lFloatSize;
    }

    protected void TestInt(LuaByteBuffer buffer, BHeader header, LHeaderParseState s, int size, int testSize, int test)
    {
        byte[] endianness = new byte[size];
        buffer.Get(endianness);
        byte[] testle = new byte[size];
        if (size < 2) throw new InvalidOperationException();
        for (int i = 0; i < testSize; i++)
        {
            testle[i] = (byte)(0xFF & test);
            test >>= 8;
        }
        for (int i = testSize; i < size; i++)
        {
            testle[i] = test >= 0 ? (byte)0 : (byte)0xFF;
        }
        LHeader.LEndianness resultendian;
        if (BytesEqual(endianness, testle))
        {
            resultendian = LHeader.LEndianness.LITTLE;
            buffer.Order(LuaByteBuffer.ByteOrder.LittleEndian);
        }
        else
        {
            // swap byte order
            for (int i = 0; i < size - i - 1; i++)
            {
                byte b = testle[i];
                testle[i] = testle[size - i - 1];
                testle[size - i - 1] = b;
            }
            if (BytesEqual(endianness, testle))
            {
                resultendian = LHeader.LEndianness.BIG;
                buffer.Order(LuaByteBuffer.ByteOrder.BigEndian);
            }
            else
            {
                throw new InvalidOperationException("The input chunk reports an invalid endianness: " + BytesToDebugString(endianness));
            }
        }
        if (!s.endiannessSet)
        {
            s.endianness = resultendian;
            s.endiannessSet = true;
        }
        else if (s.endianness != resultendian)
        {
            throw new InvalidOperationException("Inconsistent endianness");
        }
    }

    private static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private static string BytesToDebugString(byte[] bytes)
    {
        // Mirror Java's Arrays.toString(byte[]) output "[a, b, c]" (signed bytes).
        System.Text.StringBuilder sb = new System.Text.StringBuilder("[");
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append((sbyte)bytes[i]);
        }
        sb.Append(']');
        return sb.ToString();
    }

    protected void ParseIntSize55(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        int lIntSize = 0xFF & buffer.Get();
        TestInt(buffer, header, s, lIntSize, 2, -TEST_INTEGER);
        s.integer = new BIntegerTypeWrapper(s.sizeT, false, lIntSize);
        s.vinteger = new BIntegerType50(true, lIntSize, true);
    }

    protected void ParseNumberFormat53(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        TestInt(buffer, header, s, s.lIntegerSize, 2, TEST_INTEGER);

        s.linteger = new LNumberType(s.lIntegerSize, true, LNumberType.NumberMode.MODE_INTEGER);
        s.lfloat = new LNumberType(s.lFloatSize, false, LNumberType.NumberMode.MODE_FLOAT);
        double floatcheck = s.lfloat.Parse(buffer, header).Value();
        if (floatcheck != s.lfloat.Convert(TEST_FLOAT))
        {
            throw new InvalidOperationException("The input chunk is using an unrecognized floating point format: " + floatcheck);
        }
    }

    protected void ParseNumberFormat55(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        s.lIntegerSize = 0xFF & buffer.Get();
        TestInt(buffer, header, s, s.lIntegerSize, 2, -TEST_INTEGER);
        s.lFloatSize = 0xFF & buffer.Get();
        s.linteger = new LNumberTypeWrapper(new BIntegerTypeWrapper(s.sizeT, true, s.lIntegerSize), s.lIntegerSize);
        s.lfloat = new LNumberType(s.lFloatSize, false, LNumberType.NumberMode.MODE_FLOAT);
        double floatcheck = s.lfloat.Parse(buffer, header).Value();
        if (floatcheck != s.lfloat.Convert(-TEST_FLOAT))
        {
            throw new InvalidOperationException("The input chunk is using an unrecognized floating point format: " + floatcheck);
        }
    }

    protected void ParseExtractor(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        s.sizeOp = 0xFF & buffer.Get();
        s.sizeA = 0xFF & buffer.Get();
        s.sizeB = 0xFF & buffer.Get();
        s.sizeC = 0xFF & buffer.Get();
        if (header.debug)
        {
            Console.WriteLine("-- Lua opcode extractor sizeOp: " + s.sizeOp + ", sizeA: " + s.sizeA + ", sizeB: " + s.sizeB + ", sizeC: " + s.sizeC);
        }
    }

    protected void WriteExtractor(Stream @out, BHeader header, LHeader obj)
    {
        @out.WriteByte((byte)obj.extractor.op.size);
        @out.WriteByte((byte)obj.extractor.A.size);
        @out.WriteByte((byte)obj.extractor.B.size);
        @out.WriteByte((byte)obj.extractor.C.size);
    }

    protected void ParseTail(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        for (int i = 0; i < luacTail.Length; i++)
        {
            if (buffer.Get() != luacTail[i])
            {
                throw new InvalidOperationException("The input file does not have the header tail of a valid Lua file (it may be corrupted).");
            }
        }
    }

    protected void WriteTail(Stream @out, BHeader header, LHeader obj)
    {
        for (int i = 0; i < luacTail.Length; i++)
        {
            @out.WriteByte(luacTail[i]);
        }
    }
}

internal sealed class LHeaderType50 : LHeaderType
{
    private const double TEST_NUMBER = 3.14159265358979323846E7;

    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        s.format = 0;
        ParseEndianness(buffer, header, s);
        ParseIntSize(buffer, header, s);
        ParseSizeTSize(buffer, header, s);
        ParseInstructionSize(buffer, header, s);
        ParseExtractor(buffer, header, s);
        ParseNumberSize(buffer, header, s);
        LNumberType lfloat = new LNumberType(s.lNumberSize, false, LNumberType.NumberMode.MODE_NUMBER);
        LNumberType linteger = new LNumberType(s.lNumberSize, true, LNumberType.NumberMode.MODE_NUMBER);
        buffer.Mark();
        double floatcheck = lfloat.Parse(buffer, header).Value();
        buffer.Reset();
        double intcheck = linteger.Parse(buffer, header).Value();
        if (floatcheck == lfloat.Convert(TEST_NUMBER))
        {
            s.number = lfloat;
        }
        else if (intcheck == linteger.Convert(TEST_NUMBER))
        {
            s.number = linteger;
        }
        else
        {
            throw new InvalidOperationException("The input chunk is using an unrecognized number format: " + intcheck);
        }
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.ENDIANNESS,
            Directive.INT_SIZE,
            Directive.SIZE_T_SIZE,
            Directive.INSTRUCTION_SIZE,
            Directive.SIZE_OP,
            Directive.SIZE_A,
            Directive.SIZE_B,
            Directive.SIZE_C,
            Directive.NUMBER_FORMAT,
        };
    }

    public override void Write(Stream @out, BHeader header, LHeader obj)
    {
        WriteEndianness(@out, header, obj);
        WriteIntSize(@out, header, obj);
        WriteSizeTSize(@out, header, obj);
        WriteInstructionSize(@out, header, obj);
        WriteExtractor(@out, header, obj);
        WriteNumberSize(@out, header, obj);
        obj.number.Write(@out, header, obj.number.Create(TEST_NUMBER));
    }
}

internal sealed class LHeaderType51 : LHeaderType
{
    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        ParseFormat(buffer, header, s);
        ParseEndianness(buffer, header, s);
        ParseIntSize(buffer, header, s);
        ParseSizeTSize(buffer, header, s);
        ParseInstructionSize(buffer, header, s);
        ParseNumberSize(buffer, header, s);
        ParseNumberIntegrality(buffer, header, s);
        s.number = new LNumberType(s.lNumberSize, s.lNumberIntegrality, LNumberType.NumberMode.MODE_NUMBER);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.FORMAT,
            Directive.ENDIANNESS,
            Directive.INT_SIZE,
            Directive.SIZE_T_SIZE,
            Directive.INSTRUCTION_SIZE,
            Directive.NUMBER_FORMAT,
        };
    }

    public override void Write(Stream @out, BHeader header, LHeader obj)
    {
        WriteFormat(@out, header, obj);
        WriteEndianness(@out, header, obj);
        WriteIntSize(@out, header, obj);
        WriteSizeTSize(@out, header, obj);
        WriteInstructionSize(@out, header, obj);
        WriteNumberSize(@out, header, obj);
        WriteNumberIntegrality(@out, header, obj);
    }
}

internal sealed class LHeaderType52 : LHeaderType
{
    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        ParseFormat(buffer, header, s);
        ParseEndianness(buffer, header, s);
        ParseIntSize(buffer, header, s);
        ParseSizeTSize(buffer, header, s);
        ParseInstructionSize(buffer, header, s);
        ParseNumberSize(buffer, header, s);
        ParseNumberIntegrality(buffer, header, s);
        ParseTail(buffer, header, s);
        s.number = new LNumberType(s.lNumberSize, s.lNumberIntegrality, LNumberType.NumberMode.MODE_NUMBER);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.FORMAT,
            Directive.ENDIANNESS,
            Directive.INT_SIZE,
            Directive.SIZE_T_SIZE,
            Directive.INSTRUCTION_SIZE,
            Directive.NUMBER_FORMAT,
        };
    }

    public override void Write(Stream @out, BHeader header, LHeader obj)
    {
        WriteFormat(@out, header, obj);
        WriteEndianness(@out, header, obj);
        WriteIntSize(@out, header, obj);
        WriteSizeTSize(@out, header, obj);
        WriteInstructionSize(@out, header, obj);
        WriteNumberSize(@out, header, obj);
        WriteNumberIntegrality(@out, header, obj);
        WriteTail(@out, header, obj);
    }
}

internal sealed class LHeaderType53 : LHeaderType
{
    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        ParseFormat(buffer, header, s);
        ParseTail(buffer, header, s);
        ParseIntSize(buffer, header, s);
        ParseSizeTSize(buffer, header, s);
        ParseInstructionSize(buffer, header, s);
        ParseIntegerSize(buffer, header, s);
        ParseFloatSize(buffer, header, s);
        ParseNumberFormat53(buffer, header, s);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.FORMAT,
            Directive.INT_SIZE,
            Directive.SIZE_T_SIZE,
            Directive.INSTRUCTION_SIZE,
            Directive.INTEGER_FORMAT,
            Directive.FLOAT_FORMAT,
            Directive.ENDIANNESS,
        };
    }

    public override void Write(Stream @out, BHeader header, LHeader obj)
    {
        WriteFormat(@out, header, obj);
        WriteTail(@out, header, obj);
        WriteIntSize(@out, header, obj);
        WriteSizeTSize(@out, header, obj);
        WriteInstructionSize(@out, header, obj);
        @out.WriteByte((byte)header.linteger.size);
        @out.WriteByte((byte)header.lfloat.size);
        header.linteger.Write(@out, header, header.linteger.Create((double)TEST_INTEGER));
        header.lfloat.Write(@out, header, header.lfloat.Create(TEST_FLOAT));
    }
}

internal sealed class LHeaderType54 : LHeaderType
{
    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        ParseFormat(buffer, header, s);
        ParseTail(buffer, header, s);
        ParseInstructionSize(buffer, header, s);
        ParseIntegerSize(buffer, header, s);
        ParseFloatSize(buffer, header, s);
        ParseNumberFormat53(buffer, header, s);
        s.integer = new BIntegerType54(true);
        s.sizeT = s.integer;
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.FORMAT,
            Directive.INSTRUCTION_SIZE,
            Directive.INTEGER_FORMAT,
            Directive.FLOAT_FORMAT,
            Directive.ENDIANNESS,
        };
    }

    public override void Write(Stream @out, BHeader header, LHeader obj)
    {
        WriteFormat(@out, header, obj);
        WriteTail(@out, header, obj);
        WriteInstructionSize(@out, header, obj);
        @out.WriteByte((byte)header.linteger.size);
        @out.WriteByte((byte)header.lfloat.size);
        header.linteger.Write(@out, header, header.linteger.Create((double)TEST_INTEGER));
        header.lfloat.Write(@out, header, header.lfloat.Create(TEST_FLOAT));
    }
}

internal sealed class LHeaderType55 : LHeaderType
{
    protected override void ParseMain(LuaByteBuffer buffer, BHeader header, LHeaderParseState s)
    {
        s.sizeT = new BIntegerType54(false);
        ParseFormat(buffer, header, s);
        ParseTail(buffer, header, s);
        ParseIntSize55(buffer, header, s);
        ParseInstructionSize(buffer, header, s);
        TestInt(buffer, header, s, 4, 4, TEST_INSTRUCTION);
        ParseNumberFormat55(buffer, header, s);
    }

    public override List<Directive> GetDirectives()
    {
        return new List<Directive>
        {
            Directive.FORMAT,
            Directive.INT_SIZE,
            Directive.INSTRUCTION_SIZE,
            Directive.INTEGER_FORMAT,
            Directive.FLOAT_FORMAT,
            Directive.ENDIANNESS,
        };
    }

    public override void Write(Stream @out, BHeader header, LHeader obj)
    {
        WriteFormat(@out, header, obj);
        WriteTail(@out, header, obj);
        WriteIntSize(@out, header, obj);
        header.vinteger.Write(@out, header, header.vinteger.Create(-TEST_INTEGER));
        WriteInstructionSize(@out, header, obj);
        // Instruction size is always 4, independent of int_size; write with an
        // inline fixed-size helper rather than via vinteger (which is sized to
        // int_size, potentially != 4).
        new BIntegerType50(true, 4, true).Write(@out, header, new BInteger(TEST_INSTRUCTION));
        @out.WriteByte((byte)header.linteger.size);
        // The header stores the test integer in a fixed-width encoding
        // (lIntegerSize bytes), distinct from the variable-length encoding
        // used for integer *constants* in the function body (which is why
        // header.linteger may be an LNumberTypeWrapper over a variable-length
        // BIntegerTypeWrapper). Emit fixed-size bytes directly.
        new BIntegerType50(true, header.linteger.size, true)
            .Write(@out, header, new BInteger(-TEST_INTEGER));
        @out.WriteByte((byte)header.lfloat.size);
        header.lfloat.Write(@out, header, header.lfloat.Create(-TEST_FLOAT));
    }
}
