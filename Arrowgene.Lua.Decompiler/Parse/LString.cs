using Arrowgene.Lua.Decompiler.Decompile;
using Arrowgene.Lua.Decompiler.Util;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LString.</summary>
public sealed class LString : LObject
{
    public static readonly LString NULL = new LString("");

    public readonly string value;
    public readonly char terminator;
    public bool islong;

    public LString(string value) : this(value, '\0', false) { }

    public LString(string value, char terminator) : this(value, terminator, false) { }

    public LString(string value, char terminator, bool islong)
    {
        this.value = value;
        this.terminator = terminator;
        this.islong = islong;
    }

    public override string Deref() => value;

    public override string ToPrintString(int flags)
    {
        if (ReferenceEquals(this, NULL))
        {
            return "null";
        }

        string prefix = "";
        string suffix = "";
        if (islong) prefix = "L";
        if (PrintFlag.Test(flags, PrintFlag.SHORT))
        {
            const int limit = 20;
            if (value.Length > limit) suffix = " (truncated)";
            return prefix + StringUtils.ToPrintString(value, limit) + suffix;
        }

        return prefix + StringUtils.ToPrintString(value);
    }

    public override bool Equals(object o)
    {
        if (ReferenceEquals(this, NULL) || ReferenceEquals(o, NULL))
        {
            return ReferenceEquals(this, o);
        }
        if (o is LString os)
        {
            return os.value == value && os.islong == islong;
        }
        return false;
    }

    public override int GetHashCode() => value == null ? 0 : value.GetHashCode();
}
