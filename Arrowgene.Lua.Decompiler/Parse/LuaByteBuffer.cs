using System;
using System.IO;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Adapter that mimics java.nio.ByteBuffer so the parse layer can be ported from
/// the upstream unluac Java source with minimal textual changes.
///
/// Only the subset of operations used by unluac's parse layer is implemented.
/// </summary>
public sealed class LuaByteBuffer
{
    public enum ByteOrder
    {
        BigEndian,
        LittleEndian,
    }

    public static readonly ByteOrder BIG_ENDIAN = ByteOrder.BigEndian;
    public static readonly ByteOrder LITTLE_ENDIAN = ByteOrder.LittleEndian;

    private readonly byte[] _data;
    private int _position;
    private readonly int _limit;
    private ByteOrder _order;
    private int _mark = -1;

    public LuaByteBuffer(byte[] data)
    {
        _data = data;
        _position = 0;
        _limit = data.Length;
        _order = ByteOrder.BigEndian;
    }

    public static LuaByteBuffer Wrap(byte[] data) => new LuaByteBuffer(data);

    public static LuaByteBuffer ReadAll(Stream s)
    {
        using MemoryStream ms = new MemoryStream();
        s.CopyTo(ms);
        return new LuaByteBuffer(ms.ToArray());
    }

    public ByteOrder Order() => _order;
    public LuaByteBuffer Order(ByteOrder order) { _order = order; return this; }

    public int Position() => _position;
    public LuaByteBuffer Position(int pos)
    {
        if (pos < 0 || pos > _limit) throw new ArgumentOutOfRangeException(nameof(pos));
        _position = pos;
        return this;
    }

    public int Limit() => _limit;
    public int Remaining() => _limit - _position;
    public bool HasRemaining() => _position < _limit;

    public LuaByteBuffer Mark()
    {
        _mark = _position;
        return this;
    }

    public LuaByteBuffer Reset()
    {
        if (_mark < 0) throw new InvalidOperationException("mark not set");
        _position = _mark;
        return this;
    }

    public byte Get()
    {
        if (_position >= _limit) throw new InvalidOperationException("buffer underflow");
        return _data[_position++];
    }

    public byte Get(int idx)
    {
        if (idx < 0 || idx >= _limit) throw new ArgumentOutOfRangeException(nameof(idx));
        return _data[idx];
    }

    public void Get(byte[] dst)
    {
        Get(dst, 0, dst.Length);
    }

    public void Get(byte[] dst, int offset, int length)
    {
        if (_position + length > _limit) throw new InvalidOperationException("buffer underflow");
        Buffer.BlockCopy(_data, _position, dst, offset, length);
        _position += length;
    }

    public short GetShort()
    {
        if (_position + 2 > _limit) throw new InvalidOperationException("buffer underflow");
        short v;
        if (_order == ByteOrder.LittleEndian)
        {
            v = (short)(_data[_position] | (_data[_position + 1] << 8));
        }
        else
        {
            v = (short)((_data[_position] << 8) | _data[_position + 1]);
        }
        _position += 2;
        return v;
    }

    public int GetInt()
    {
        if (_position + 4 > _limit) throw new InvalidOperationException("buffer underflow");
        int v;
        if (_order == ByteOrder.LittleEndian)
        {
            v = _data[_position]
              | (_data[_position + 1] << 8)
              | (_data[_position + 2] << 16)
              | (_data[_position + 3] << 24);
        }
        else
        {
            v = (_data[_position] << 24)
              | (_data[_position + 1] << 16)
              | (_data[_position + 2] << 8)
              | _data[_position + 3];
        }
        _position += 4;
        return v;
    }

    public long GetLong()
    {
        if (_position + 8 > _limit) throw new InvalidOperationException("buffer underflow");
        long v;
        if (_order == ByteOrder.LittleEndian)
        {
            v = (long)_data[_position]
              | ((long)_data[_position + 1] << 8)
              | ((long)_data[_position + 2] << 16)
              | ((long)_data[_position + 3] << 24)
              | ((long)_data[_position + 4] << 32)
              | ((long)_data[_position + 5] << 40)
              | ((long)_data[_position + 6] << 48)
              | ((long)_data[_position + 7] << 56);
        }
        else
        {
            v = ((long)_data[_position] << 56)
              | ((long)_data[_position + 1] << 48)
              | ((long)_data[_position + 2] << 40)
              | ((long)_data[_position + 3] << 32)
              | ((long)_data[_position + 4] << 24)
              | ((long)_data[_position + 5] << 16)
              | ((long)_data[_position + 6] << 8)
              | _data[_position + 7];
        }
        _position += 8;
        return v;
    }

    public float GetFloat()
    {
        int bits = GetInt();
        return BitConverter.Int32BitsToSingle(bits);
    }

    public double GetDouble()
    {
        long bits = GetLong();
        return BitConverter.Int64BitsToDouble(bits);
    }
}
