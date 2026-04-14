using System;
using System.IO;
using Arrowgene.Lua.Decompiler.Decompile;
using Type = Arrowgene.Lua.Decompiler.Decompile.Type;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LConstantType.</summary>
public sealed class LConstantType : BObjectType<LObject>
{
    public override LObject Parse(LuaByteBuffer buffer, BHeader header)
    {
        int typecode = 0xFF & buffer.Get();
        Type type = header.typemap.Get(typecode);
        if (header.debug)
        {
            Console.Write("-- parsing <constant>, type " + typecode + " is ");
            Console.WriteLine(type == null ? "unknown" : type.ToString());
        }
        if (type == null)
        {
            throw new Exception("unmapped type code " + typecode);
        }
        if (type == Type.NIL) return LNil.NIL;
        if (type == Type.BOOLEAN) return header.@bool.Parse(buffer, header);
        if (type == Type.FALSE) return LBoolean.LFALSE;
        if (type == Type.TRUE) return LBoolean.LTRUE;
        if (type == Type.NUMBER) return header.number.Parse(buffer, header);
        if (type == Type.FLOAT) return header.lfloat.Parse(buffer, header);
        if (type == Type.INTEGER) return header.linteger.Parse(buffer, header);
        if (type == Type.STRING || type == Type.SHORT_STRING)
        {
            return header.@string.Parse(buffer, header);
        }
        if (type == Type.LONG_STRING)
        {
            LString s = header.@string.Parse(buffer, header);
            s.islong = true;
            return s;
        }
        throw new InvalidOperationException();
    }

    public override void Write(Stream @out, BHeader header, LObject obj)
    {
        if (obj is LNil)
        {
            if (header.typemap.NIL == TypeMap.UNMAPPED) throw new InvalidOperationException();
            @out.WriteByte((byte)header.typemap.NIL);
        }
        else if (obj is LBoolean b)
        {
            bool value = b.Value();
            if (value && header.typemap.TRUE != TypeMap.UNMAPPED)
            {
                @out.WriteByte((byte)header.typemap.TRUE);
            }
            else if (!value && header.typemap.FALSE != TypeMap.UNMAPPED)
            {
                @out.WriteByte((byte)header.typemap.FALSE);
            }
            else if (header.typemap.BOOLEAN != TypeMap.UNMAPPED)
            {
                @out.WriteByte((byte)header.typemap.BOOLEAN);
                header.@bool.Write(@out, header, b);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        else if (obj is LNumber n)
        {
            if (header.typemap.FLOAT != TypeMap.UNMAPPED && !n.IntegralType())
            {
                @out.WriteByte((byte)header.typemap.FLOAT);
                header.lfloat.Write(@out, header, n);
            }
            else if (header.typemap.INTEGER != TypeMap.UNMAPPED && n.IntegralType())
            {
                @out.WriteByte((byte)header.typemap.INTEGER);
                header.linteger.Write(@out, header, n);
            }
            else if (header.typemap.NUMBER != TypeMap.UNMAPPED)
            {
                @out.WriteByte((byte)header.typemap.NUMBER);
                header.number.Write(@out, header, n);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        else if (obj is LString s)
        {
            if (header.typemap.SHORT_STRING != TypeMap.UNMAPPED && !s.islong)
            {
                @out.WriteByte((byte)header.typemap.SHORT_STRING);
            }
            else if (header.typemap.LONG_STRING != TypeMap.UNMAPPED && s.islong)
            {
                @out.WriteByte((byte)header.typemap.LONG_STRING);
            }
            else if (header.typemap.STRING != TypeMap.UNMAPPED)
            {
                @out.WriteByte((byte)header.typemap.STRING);
            }
            header.@string.Write(@out, header, s);
        }
        else
        {
            throw new InvalidOperationException();
        }
    }
}
