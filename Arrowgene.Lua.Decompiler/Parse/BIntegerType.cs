using System;
using System.IO;
using System.Numerics;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.BIntegerType. Provides two concrete subclasses:
/// <c>BIntegerType50</c> (fixed-size signed/unsigned big- or little-endian reads)
/// and <c>BIntegerType54</c> (variable-length ULEB128-ish continuation format).
/// </summary>
public abstract class BIntegerType : BObjectType<BInteger>
{
    public static BIntegerType Create50Type(bool signed, int intSize, bool allownegative)
    {
        return new BIntegerType50(signed, intSize, allownegative);
    }

    public static BIntegerType Create54(bool invert)
    {
        return new BIntegerType54(invert);
    }

    public virtual int GetSize()
    {
        throw new NotSupportedException();
    }

    public BInteger Create(int n)
    {
        return new BInteger(n);
    }
}

internal sealed class BIntegerType50 : BIntegerType
{
    public readonly bool Signed;
    public readonly int IntSize;
    public readonly bool AllowNegative;

    public BIntegerType50(bool signed, int intSize, bool allownegative)
    {
        Signed = signed;
        IntSize = intSize;
        AllowNegative = allownegative;
    }

    private BInteger RawParse(LuaByteBuffer buffer, BHeader header)
    {
        BInteger value;
        if (Signed && (IntSize == 0 || IntSize == 1 || IntSize == 2 || IntSize == 4))
        {
            switch (IntSize)
            {
                case 0:
                    value = new BInteger(0);
                    break;
                case 1:
                    value = new BInteger((sbyte)buffer.Get());
                    break;
                case 2:
                    value = new BInteger(buffer.GetShort());
                    break;
                case 4:
                    value = new BInteger(buffer.GetInt());
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
        else
        {
            byte[] bytes = new byte[IntSize];
            int start = 0;
            int delta = 1;
            if (buffer.Order() == LuaByteBuffer.ByteOrder.LittleEndian)
            {
                start = IntSize - 1;
                delta = -1;
            }
            for (int i = start; i >= 0 && i < IntSize; i += delta)
            {
                bytes[i] = buffer.Get();
            }
            // Java BigInteger constructors take big-endian magnitude (or signed).
            BigInteger big;
            if (Signed)
            {
                big = new BigInteger(bytes, isUnsigned: false, isBigEndian: true);
            }
            else
            {
                big = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            }
            value = new BInteger(big);
        }

        if (!AllowNegative && value.Signum() < 0)
        {
            throw new InvalidOperationException("Illegal number");
        }

        return value;
    }

    private void RawWrite(Stream @out, BHeader header, BInteger obj)
    {
        byte[] bytes = obj.LittleEndianBytes(IntSize);
        if (header.lheader.endianness == LHeader.LEndianness.LITTLE)
        {
            foreach (byte b in bytes)
            {
                @out.WriteByte(b);
            }
        }
        else
        {
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                @out.WriteByte(bytes[i]);
            }
        }
    }

    public override BInteger Parse(LuaByteBuffer buffer, BHeader header)
    {
        BInteger value = RawParse(buffer, header);
        if (header.debug)
        {
            Console.WriteLine("-- parsed <integer> " + value.AsInt());
        }
        return value;
    }

    public override void Write(Stream @out, BHeader header, BInteger obj)
    {
        RawWrite(@out, header, obj);
    }

    public override int GetSize() => IntSize;
}

internal sealed class BIntegerType54 : BIntegerType
{
    public readonly bool Invert;
    private readonly byte _notend;
    private readonly byte _end;

    public BIntegerType54(bool invert)
    {
        Invert = invert;
        if (!invert)
        {
            _end = 0;
            _notend = 0x80;
        }
        else
        {
            _end = 0x80;
            _notend = 0;
        }
    }

    public override BInteger Parse(LuaByteBuffer buffer, BHeader header)
    {
        long x = 0;
        byte b;
        int bits = 7;
        do
        {
            b = buffer.Get();
            x = (x << 7) | (long)(b & 0x7F);
            bits += 7;
        } while ((b & 0x80) != (_end & 0x80) && bits <= 63);

        if ((b & 0x80) != (_end & 0x80))
        {
            BigInteger bigx = new BigInteger(x);
            do
            {
                b = buffer.Get();
                bigx <<= 7;
                // Note: upstream Java has a known bug here (discarded add result);
                // preserved for behavioural parity.
                _ = bigx + new BigInteger(b & 0x7F);
            } while ((b & 0x80) != (_end & 0x80));
            return new BInteger(bigx);
        }
        if (x <= int.MaxValue)
        {
            return new BInteger((int)x);
        }
        return new BInteger(new BigInteger(x));
    }

    public override void Write(Stream @out, BHeader header, BInteger obj)
    {
        byte[] bytes = obj.CompressedBytes();
        for (int i = bytes.Length - 1; i >= 1; i--)
        {
            @out.WriteByte((byte)(bytes[i] | _notend));
        }
        @out.WriteByte((byte)(bytes[0] | _end));
    }
}

internal sealed class BIntegerTypeWrapper : BIntegerType
{
    public readonly BIntegerType Base;
    public readonly bool Signed;
    public readonly int Size;

    public BIntegerTypeWrapper(BIntegerType @base, bool signed, int size)
    {
        Base = @base;
        Signed = signed;
        Size = size;
    }

    public override BInteger Parse(LuaByteBuffer buffer, BHeader header)
    {
        BInteger inner = Base.Parse(buffer, header);
        if (!inner.CheckSize(Size)) throw new InvalidOperationException("Integer out of range: " + inner);
        return Signed ? inner.Convert() : inner;
    }

    public override void Write(Stream @out, BHeader header, BInteger obj)
    {
        BInteger toWrite = Signed ? obj.ConvertReverse() : obj;
        Base.Write(@out, header, toWrite);
    }

    public override int GetSize() => Size;
}
