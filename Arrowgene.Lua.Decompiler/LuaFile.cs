using System.IO;

namespace Arrowgene.Lua.Decompiler;

public enum LuaFileType
{
    Unknown,
    LuaSource,
    LuaCompiled,
}

public sealed class LuaFileInfo
{
    public LuaFileType Type { get; }
    public int? MajorVersion { get; }
    public int? MinorVersion { get; }

    public LuaFileInfo(LuaFileType type, int? majorVersion = null, int? minorVersion = null)
    {
        Type = type;
        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
    }

    public string GetVersionString()
    {
        if (MajorVersion == null || MinorVersion == null) return null;
        return MajorVersion + "." + MinorVersion;
    }
}

public static class LuaFile
{
    private static readonly byte[] CompiledSignature = { 0x1B, 0x4C, 0x75, 0x61 };

    public static LuaFileInfo Identify(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File not found.", path);

        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Identify(fs);
    }

    public static LuaFileInfo Identify(byte[] data)
    {
        using MemoryStream ms = new MemoryStream(data);
        return Identify(ms);
    }

    public static LuaFileInfo Identify(Stream stream)
    {
        byte[] header = new byte[5];
        int bytesRead = 0;
        while (bytesRead < header.Length)
        {
            int read = stream.Read(header, bytesRead, header.Length - bytesRead);
            if (read == 0) break;
            bytesRead += read;
        }

        if (bytesRead >= 5 && IsCompiledSignature(header))
        {
            int versionByte = header[4];
            int major = versionByte >> 4;
            int minor = versionByte & 0x0F;
            return new LuaFileInfo(LuaFileType.LuaCompiled, major, minor);
        }

        if (IsLuaSource(header, bytesRead))
        {
            return new LuaFileInfo(LuaFileType.LuaSource);
        }

        return new LuaFileInfo(LuaFileType.Unknown);
    }

    private static bool IsCompiledSignature(byte[] header)
    {
        for (int i = 0; i < CompiledSignature.Length; i++)
        {
            if (header[i] != CompiledSignature[i]) return false;
        }
        return true;
    }

    private static bool IsLuaSource(byte[] header, int length)
    {
        for (int i = 0; i < length; i++)
        {
            byte b = header[i];
            if (b == 0x00) return false;
        }
        return length > 0;
    }
}
