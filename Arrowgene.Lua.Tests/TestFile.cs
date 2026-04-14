namespace Arrowgene.Lua.Tests;

/// <summary>
/// Port of unluac.test.TestFile.
/// </summary>
public class TestFile
{
    public const int DEFAULT_VERSION = 0x50;
    public const int RELAXED_SCOPE = 1;

    public readonly string Name;
    public readonly int Version;
    public readonly int Flags;

    public TestFile(string name)
        : this(name, DEFAULT_VERSION, 0) { }

    public TestFile(string name, int version)
        : this(name, version, 0) { }

    public TestFile(string name, int version, int flags)
    {
        Name = name;
        Version = version;
        Flags = flags;
    }

    public bool GetFlag(int flag)
    {
        return (Flags & flag) == flag;
    }
}
