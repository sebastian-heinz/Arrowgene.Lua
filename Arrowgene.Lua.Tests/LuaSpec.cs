namespace Arrowgene.Lua.Tests;

/// <summary>
/// Port of unluac.test.LuaSpec — identifies a Lua version and optional
/// minor version / number format for the external luac compiler.
/// </summary>
public class LuaSpec
{
    public enum NumberFormat
    {
        DEFAULT,
        FLOAT,
        INT32,
        INT64,
    }

    public readonly int Version;
    public readonly int MinorVersion;
    public NumberFormat Format;

    public LuaSpec(int version)
        : this(version, -1) { }

    public LuaSpec(int version, int minorVersion)
    {
        Version = version;
        MinorVersion = minorVersion;
        Format = NumberFormat.DEFAULT;
    }

    public string Id()
    {
        string id = "lua" + Version.ToString("x");
        if (MinorVersion >= 0) id += MinorVersion;
        return id;
    }

    public string GetLuaCName()
    {
        string name = "luac" + Version.ToString("x");
        if (MinorVersion >= 0) name += MinorVersion;
        switch (Format)
        {
            case NumberFormat.FLOAT: name += "_float"; break;
            case NumberFormat.INT32: name += "_int32"; break;
            case NumberFormat.INT64: name += "_int64"; break;
        }
        return name;
    }

    public bool Compatible(TestFile testfile)
    {
        if (testfile.Version == TestFile.DEFAULT_VERSION)
        {
            return Compatible(testfile.Name);
        }
        return Version >= testfile.Version;
    }

    public bool Compatible(string filename)
    {
        int version = 0;
        int underscore = filename.IndexOf('_');
        if (underscore != -1)
        {
            string prefix = filename.Substring(0, underscore);
            if (int.TryParse(prefix, System.Globalization.NumberStyles.HexNumber, null, out int parsed))
            {
                version = parsed;
            }
        }
        return version == 0 || Version >= version;
    }
}
