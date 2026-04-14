using System.Collections.Generic;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Lua constant/value types recognized by unluac (and extensions for assembler typemaps).
/// </summary>
public sealed class Type
{
    public static readonly Type NIL          = new Type("nil");
    public static readonly Type BOOLEAN      = new Type("boolean");
    public static readonly Type FALSE        = new Type("false");
    public static readonly Type TRUE         = new Type("true");
    public static readonly Type NUMBER       = new Type("number");
    public static readonly Type FLOAT        = new Type("float");
    public static readonly Type INTEGER      = new Type("integer");
    public static readonly Type STRING       = new Type("string");
    public static readonly Type SHORT_STRING = new Type("short_string");
    public static readonly Type LONG_STRING  = new Type("long_string");

    private static readonly Type[] _values =
    {
        NIL, BOOLEAN, FALSE, TRUE, NUMBER, FLOAT, INTEGER, STRING, SHORT_STRING, LONG_STRING,
    };

    private static Dictionary<string, Type> _lookup;

    public readonly string name;

    private Type(string name)
    {
        this.name = name;
    }

    public static Type[] Values() => (Type[])_values.Clone();

    private static void InitializeLookup()
    {
        if (_lookup == null)
        {
            _lookup = new Dictionary<string, Type>();
            foreach (Type type in _values)
            {
                _lookup[type.name] = type;
            }
        }
    }

    public static Type Get(string name)
    {
        InitializeLookup();
        _lookup.TryGetValue(name, out Type t);
        return t;
    }

    public override string ToString() => name;
}
