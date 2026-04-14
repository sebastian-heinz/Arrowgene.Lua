using System.Collections.Generic;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>Port of unluac.decompile.TypeMap.</summary>
public sealed class TypeMap
{
    private readonly Type[] _types;

    public readonly int NIL;
    public readonly int BOOLEAN;
    public readonly int FALSE;
    public readonly int TRUE;
    public readonly int NUMBER;
    public readonly int FLOAT;
    public readonly int INTEGER;
    public readonly int STRING;
    public readonly int SHORT_STRING;
    public readonly int LONG_STRING;

    public const int UNMAPPED = -1;

    private const int VARIANT = 1 << 4;

    public TypeMap(Version.TypeMapType version)
    {
        bool split_string = false;
        bool split_number = false;
        bool split_boolean = false;
        bool reverse_number = false;
        switch (version)
        {
            case Version.TypeMapType.LUA50:
                break;
            case Version.TypeMapType.LUA52:
                split_string = true;
                break;
            case Version.TypeMapType.LUA53:
                split_string = true;
                split_number = true;
                break;
            case Version.TypeMapType.LUA54:
                split_string = true;
                split_number = true;
                split_boolean = true;
                reverse_number = true;
                break;
        }
        _types = new Type[split_string ? (5 + VARIANT) : 5];

        _types[0] = Type.NIL;
        NIL = 0;

        if (!split_boolean)
        {
            BOOLEAN = 1;
            FALSE = UNMAPPED;
            TRUE = UNMAPPED;
            _types[BOOLEAN] = Type.BOOLEAN;
        }
        else
        {
            BOOLEAN = UNMAPPED;
            FALSE = 1;
            TRUE = 1 | VARIANT;
            _types[FALSE] = Type.FALSE;
            _types[TRUE] = Type.TRUE;
        }

        if (!split_number)
        {
            NUMBER = 3;
            FLOAT = UNMAPPED;
            INTEGER = UNMAPPED;
            _types[NUMBER] = Type.NUMBER;
        }
        else
        {
            NUMBER = UNMAPPED;
            if (!reverse_number)
            {
                FLOAT = 3;
                INTEGER = 3 | VARIANT;
            }
            else
            {
                INTEGER = 3;
                FLOAT = 3 | VARIANT;
            }
            _types[FLOAT] = Type.FLOAT;
            _types[INTEGER] = Type.INTEGER;
        }

        if (!split_string)
        {
            STRING = 4;
            SHORT_STRING = UNMAPPED;
            LONG_STRING = UNMAPPED;
            _types[STRING] = Type.STRING;
        }
        else
        {
            STRING = UNMAPPED;
            SHORT_STRING = 4;
            LONG_STRING = 4 | VARIANT;
            _types[SHORT_STRING] = Type.SHORT_STRING;
            _types[LONG_STRING] = Type.LONG_STRING;
        }
    }

    public TypeMap(Dictionary<int, Type> usertypemap)
    {
        int maximum = 0;
        foreach (int typecode in usertypemap.Keys)
        {
            if (typecode > maximum) maximum = typecode;
        }
        _types = new Type[maximum + 1];
        int user_nil = UNMAPPED;
        int user_boolean = UNMAPPED;
        int user_false = UNMAPPED;
        int user_true = UNMAPPED;
        int user_number = UNMAPPED;
        int user_float = UNMAPPED;
        int user_integer = UNMAPPED;
        int user_string = UNMAPPED;
        int user_short_string = UNMAPPED;
        int user_long_string = UNMAPPED;
        foreach (KeyValuePair<int, Type> entry in usertypemap)
        {
            int typecode = entry.Key;
            Type type = entry.Value;
            _types[typecode] = type;
            if (type == Type.NIL) user_nil = typecode;
            else if (type == Type.BOOLEAN) user_boolean = typecode;
            else if (type == Type.FALSE) user_false = typecode;
            else if (type == Type.TRUE) user_true = typecode;
            else if (type == Type.NUMBER) user_number = typecode;
            else if (type == Type.FLOAT) user_float = typecode;
            else if (type == Type.INTEGER) user_integer = typecode;
            else if (type == Type.STRING) user_string = typecode;
            else if (type == Type.SHORT_STRING) user_short_string = typecode;
            else if (type == Type.LONG_STRING) user_long_string = typecode;
        }
        NIL = user_nil;
        BOOLEAN = user_boolean;
        FALSE = user_false;
        TRUE = user_true;
        NUMBER = user_number;
        FLOAT = user_float;
        INTEGER = user_integer;
        STRING = user_string;
        SHORT_STRING = user_short_string;
        LONG_STRING = user_long_string;
    }

    public int Size() => _types.Length;

    public Type Get(int typecode)
    {
        if (typecode >= 0 && typecode < _types.Length)
        {
            return _types[typecode];
        }
        return null;
    }
}
