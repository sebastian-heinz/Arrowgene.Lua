namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LBoolean (with LTRUE/LFALSE singletons).</summary>
public sealed class LBoolean : LObject
{
    public static readonly LBoolean LTRUE = new LBoolean(true);
    public static readonly LBoolean LFALSE = new LBoolean(false);

    private readonly bool _value;

    private LBoolean(bool value)
    {
        _value = value;
    }

    public bool Value() => _value;

    public override string ToPrintString(int flags) => _value ? "true" : "false";

    public override bool Equals(object o) => ReferenceEquals(this, o);

    public override int GetHashCode() => _value ? 1 : 0;
}
