using System;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.LObject. Base class for constant-pool values (nil,
/// booleans, strings, numbers).
/// </summary>
public abstract class LObject : BObject
{
    public virtual string Deref()
    {
        throw new InvalidOperationException();
    }

    public virtual string ToPrintString(int flags)
    {
        throw new InvalidOperationException();
    }

    public abstract override bool Equals(object o);

    public override int GetHashCode() => base.GetHashCode();
}
