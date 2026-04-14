using System;
using System.IO;
using System.Numerics;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LNumberType.</summary>
public class LNumberType : BObjectType<LNumber>
{
    public enum NumberMode
    {
        /// <summary>Lua 5.0 - 5.2 where numbers can represent integers or floats.</summary>
        MODE_NUMBER,
        /// <summary>Lua 5.3+ floats.</summary>
        MODE_FLOAT,
        /// <summary>Lua 5.3+ integers.</summary>
        MODE_INTEGER,
    }

    public readonly int size;
    public readonly bool integral;
    public readonly NumberMode mode;

    public LNumberType(int size, bool integral, NumberMode mode)
    {
        this.size = size;
        this.integral = integral;
        this.mode = mode;
        if (!(size == 4 || size == 8))
        {
            throw new InvalidOperationException("The input chunk has an unsupported Lua number size: " + size);
        }
    }

    public double Convert(double number)
    {
        if (integral)
        {
            return size switch
            {
                4 => (int)number,
                8 => (long)number,
                _ => throw new InvalidOperationException("The input chunk has an unsupported Lua number format"),
            };
        }

        return size switch
        {
            4 => (float)number,
            8 => number,
            _ => throw new InvalidOperationException("The input chunk has an unsupported Lua number format"),
        };
    }

    public override LNumber Parse(LuaByteBuffer buffer, BHeader header)
    {
        LNumber value = null;
        if (integral)
        {
            switch (size)
            {
                case 4:
                    value = new LIntNumber(buffer.GetInt());
                    break;
                case 8:
                    value = new LLongNumber(buffer.GetLong());
                    break;
            }
        }
        else
        {
            switch (size)
            {
                case 4:
                    value = new LFloatNumber(buffer.GetFloat(), mode);
                    break;
                case 8:
                    value = new LDoubleNumber(buffer.GetDouble(), mode);
                    break;
            }
        }
        if (value == null)
        {
            throw new InvalidOperationException("The input chunk has an unsupported Lua number format");
        }
        if (header.debug)
        {
            Console.WriteLine("-- parsed <number> " + value);
        }
        return value;
    }

    public override void Write(Stream @out, BHeader header, LNumber n)
    {
        long bits = n.Bits();
        if (header.lheader.endianness == LHeader.LEndianness.LITTLE)
        {
            for (int i = 0; i < size; i++)
            {
                @out.WriteByte((byte)(bits & 0xFF));
                bits = (long)((ulong)bits >> 8);
            }
        }
        else
        {
            for (int i = size - 1; i >= 0; i--)
            {
                @out.WriteByte((byte)((bits >> (i * 8)) & 0xFF));
            }
        }
    }

    public LNumber CreateNaN(long bits)
    {
        if (integral)
        {
            throw new InvalidOperationException();
        }
        switch (size)
        {
            case 4:
            {
                int fbits = BitConverter.SingleToInt32Bits(float.NaN);
                if (bits < 0)
                {
                    bits ^= unchecked((long)0x8000000000000000L);
                    fbits ^= unchecked((int)0x80000000);
                }
                fbits |= (int)(bits >> LFloatNumber.NAN_SHIFT_OFFSET);
                return new LFloatNumber(BitConverter.Int32BitsToSingle(fbits), mode);
            }
            case 8:
                return new LDoubleNumber(
                    BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(double.NaN) ^ bits), mode);
            default:
                throw new InvalidOperationException();
        }
    }

    public LNumber Create(double x)
    {
        if (integral)
        {
            return size switch
            {
                4 => new LIntNumber((int)x),
                8 => new LLongNumber((long)x),
                _ => throw new InvalidOperationException(),
            };
        }
        return size switch
        {
            4 => new LFloatNumber((float)x, mode),
            8 => new LDoubleNumber(x, mode),
            _ => throw new InvalidOperationException(),
        };
    }

    public LNumber Create(BigInteger x)
    {
        if (integral)
        {
            return size switch
            {
                4 => new LIntNumber(checked((int)x)),
                8 => new LLongNumber(checked((long)x)),
                _ => throw new InvalidOperationException(),
            };
        }
        return size switch
        {
            4 => new LFloatNumber((float)x, mode),
            8 => new LDoubleNumber((double)x, mode),
            _ => throw new InvalidOperationException(),
        };
    }
}

internal sealed class LNumberTypeWrapper : LNumberType
{
    public readonly BIntegerType Itype;

    public LNumberTypeWrapper(BIntegerType itype, int size)
        : base(size, true, NumberMode.MODE_INTEGER)
    {
        Itype = itype;
    }

    public override LNumber Parse(LuaByteBuffer buffer, BHeader header)
    {
        BInteger i = Itype.Parse(buffer, header);
        return size switch
        {
            4 => new LIntNumber(i.AsInt()),
            8 => new LLongNumber(i.AsLong()),
            _ => null,
        };
    }

    public override void Write(Stream @out, BHeader header, LNumber n)
    {
        BInteger bi;
        if (n is LIntNumber li)
        {
            bi = new BInteger(li.number);
        }
        else if (n is LLongNumber ll)
        {
            bi = new BInteger(new BigInteger(ll.number));
        }
        else
        {
            throw new InvalidOperationException("LNumberTypeWrapper can only write integer numbers");
        }
        Itype.Write(@out, header, bi);
    }
}
