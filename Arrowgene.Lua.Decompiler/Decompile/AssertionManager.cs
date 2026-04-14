using System;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.AssertionManager. A tiny helper that
/// promotes an assertion failure to an <see cref="InvalidOperationException"/>,
/// centralising the "this should never happen, bail loudly" path
/// used throughout the decompiler pipeline.
/// </summary>
public static class AssertionManager
{
    public static bool AssertCritical(bool condition, string message)
    {
        if (!condition)
        {
            Critical(message);
        }
        return condition;
    }

    public static void Critical(string message)
    {
        throw new InvalidOperationException(message);
    }
}
