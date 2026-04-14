namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>Port of unluac.parse.LNil.</summary>
public sealed class LNil : LObject
{
    public static readonly LNil NIL = new LNil();

    private LNil() { }

    public override string ToPrintString(int flags) => "nil";

    public override bool Equals(object o) => ReferenceEquals(this, o);

    public override int GetHashCode() => 0;
}
