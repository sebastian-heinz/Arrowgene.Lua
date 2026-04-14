using System;
using System.Collections.Generic;
using System.Numerics;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.BInteger. Wraps either a native <c>int</c> or a
/// <see cref="BigInteger"/> for values that don't fit.
/// </summary>
public sealed class BInteger : BObject
{
    private readonly BigInteger? _big;
    private readonly int _n;

    private static readonly BigInteger MAX_INT = new BigInteger(int.MaxValue);
    private static readonly BigInteger MIN_INT = new BigInteger(int.MinValue);
    private static readonly BigInteger MAX_LONG = new BigInteger(long.MaxValue);
    private static readonly BigInteger MIN_LONG = new BigInteger(long.MinValue);

    public BInteger(BInteger b)
    {
        _big = b._big;
        _n = b._n;
    }

    public BInteger(int n)
    {
        _big = null;
        _n = n;
    }

    public BInteger(BigInteger big)
    {
        _big = big;
        _n = 0;
    }

    public int AsInt()
    {
        if (_big == null)
        {
            return _n;
        }
        BigInteger big = _big.Value;
        if (big > MAX_INT || big < MIN_INT)
        {
            throw new InvalidOperationException("The size of an integer is outside the range that unluac can handle.");
        }
        return (int)big;
    }

    public long AsLong()
    {
        if (_big == null)
        {
            return _n;
        }
        BigInteger big = _big.Value;
        if (big > MAX_LONG || big < MIN_LONG)
        {
            throw new InvalidOperationException("The size of an integer is outside the range that unluac can handle.");
        }
        return (long)big;
    }

    public int Signum()
    {
        if (_big == null)
        {
            if (_n > 0) return 1;
            if (_n < 0) return -1;
            return 0;
        }
        return _big.Value.Sign;
    }

    public byte[] LittleEndianBytes(int size)
    {
        List<byte> bytes = new List<byte>();
        byte pad = 0;
        if (_big == null)
        {
            long n = _n; // sign-extend int to long
            int limit = Math.Min(size, 8);
            for (int i = 0; i < limit; i++)
            {
                bytes.Add((byte)(n & 0xFF));
                n >>= 8; // arithmetic shift preserves sign
            }
            pad = _n < 0 ? (byte)0xFF : (byte)0;
        }
        else
        {
            BigInteger n = _big.Value;
            bool negate = false;
            if (n.Sign < 0)
            {
                n = -n;
                n -= BigInteger.One;
                negate = true;
            }
            BigInteger b256 = new BigInteger(256);
            BigInteger b255 = new BigInteger(255);
            while (n < b256 && size > 0)
            {
                int v = (int)(n & b255);
                if (negate)
                {
                    v = ~v;
                }
                bytes.Add((byte)v);
                n /= b256;
                size--;
            }
            pad = negate ? (byte)0xFF : (byte)0;
        }
        while (size > bytes.Count) bytes.Add(pad);
        return bytes.ToArray();
    }

    public byte[] CompressedBytes()
    {
        BigInteger value = _big ?? new BigInteger(_n);
        if (value.IsZero)
        {
            return new byte[] { 0 };
        }
        List<byte> bytes = new List<byte>();
        BigInteger limit = new BigInteger(0x7F);
        while (value.Sign > 0)
        {
            bytes.Add((byte)(int)(value & limit));
            value >>= 7;
        }
        return bytes.ToArray();
    }

    public bool CheckSize(int size)
    {
        if (_big != null)
        {
            // Java BigInteger.bitCount returns number of set bits, not bit length.
            // Match upstream behaviour exactly (even if semantically odd).
            int bitCount = 0;
            BigInteger v = _big.Value.Sign < 0 ? -_big.Value : _big.Value;
            while (v.Sign > 0)
            {
                if ((v & BigInteger.One) == BigInteger.One) bitCount++;
                v >>= 1;
            }
            return bitCount < size * 8;
        }
        if (size >= 4)
        {
            return true;
        }
        return _n < (1 << (size * 8));
    }

    /// <summary>Lua 5.5 style sign conversion.</summary>
    public BInteger Convert()
    {
        if (_big != null)
        {
            BigInteger big = _big.Value;
            bool sign = (big & BigInteger.One) == BigInteger.One;
            if (sign)
            {
                return new BInteger(-(big >> 1));
            }
            return new BInteger(big >> 1);
        }
        int signbit = _n & 1;
        int mag = _n >> 1;
        return signbit == 0 ? new BInteger(mag) : new BInteger(-mag);
    }

    /// <summary>
    /// Inverse of <see cref="Convert"/>: encode a signed value in Lua 5.5's
    /// "sign bit in LSB" unsigned representation used on the wire.
    /// </summary>
    public BInteger ConvertReverse()
    {
        BigInteger big = _big ?? new BigInteger(_n);
        BigInteger result = big.Sign >= 0
            ? (big << 1)
            : (((-big) << 1) | BigInteger.One);
        if (result >= MIN_INT && result <= MAX_INT)
        {
            return new BInteger((int)result);
        }
        return new BInteger(result);
    }

    public void Iterate(Action thunk)
    {
        if (_big == null)
        {
            int i = _n;
            if (i < 0)
            {
                throw new InvalidOperationException("Illegal negative list length");
            }
            while (i-- != 0)
            {
                thunk();
            }
        }
        else
        {
            BigInteger i = _big.Value;
            if (i.Sign < 0)
            {
                throw new InvalidOperationException("Illegal negative list length");
            }
            while (i.Sign > 0)
            {
                thunk();
                i -= BigInteger.One;
            }
        }
    }

    public override string ToString()
    {
        return _big?.ToString() ?? _n.ToString();
    }
}
