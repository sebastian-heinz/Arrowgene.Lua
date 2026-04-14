using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Arrowgene.Lua.Tests;

/// <summary>
/// Port of unluac.test.LuaC — wrapper to invoke an external luac compiler.
/// Binary name follows the unluac convention (luac50, luac51, etc.) but can
/// be overridden via environment variable (e.g. LUAC54=/usr/bin/luac).
/// Also checks plain "luac" and validates its version matches the spec.
/// </summary>
public static class LuaC
{
    public static void Compile(LuaSpec spec, string input, string output)
    {
        string luac = ResolveBinary(spec);
        if (luac == null)
            throw new IOException($"No luac binary found for {spec.Id()}");

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = luac,
            Arguments = $"-o \"{output}\" \"{input}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process? p;
        try
        {
            p = Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new IOException($"luac not found: {luac}");
        }

        if (p == null)
            throw new IOException($"Failed to start: {luac}");

        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            string stderr = p.StandardError.ReadToEnd();
            throw new IOException($"luac failed on file: {input}\n{stderr}");
        }
    }

    public static bool IsAvailable(LuaSpec spec)
    {
        return ResolveBinary(spec) != null;
    }

    private static string? ResolveBinary(LuaSpec spec)
    {
        string name = spec.GetLuaCName();
        string ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

        // 1. Environment variable override (e.g. LUAC54=/usr/bin/luac)
        string? envOverride = Environment.GetEnvironmentVariable(name.ToUpperInvariant());
        if (!string.IsNullOrEmpty(envOverride) && CanRun(envOverride))
            return envOverride;

        // 2. Bundled binary in tools/luac/{platform}/
        string? bundled = ResolveBundledBinary(name + ext);
        if (bundled != null)
            return bundled;

        // 3. Versioned name on PATH (e.g. luac54)
        string versioned = name + ext;
        if (CanRun(versioned))
            return versioned;

        // 4. Plain "luac" — only if its version matches the spec
        string plain = "luac" + ext;
        if (CanRun(plain) && VersionMatches(plain, spec))
            return plain;

        return null;
    }

    private static string? ResolveBundledBinary(string binaryName)
    {
        string platformDir;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            platformDir = "windows-x64";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            platformDir = "osx-arm64";
        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            platformDir = "linux-arm64";
        else
            platformDir = "linux-x64";

        // Walk up from the test assembly directory to find the repo root (contains tools/)
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "tools", "luac", platformDir, binaryName);
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    /// <summary>
    /// Checks if "luac -v" reports a version matching the spec (e.g. "Lua 5.4" for spec 0x54).
    /// </summary>
    private static bool VersionMatches(string binary, LuaSpec spec)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = binary,
                Arguments = "-v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process? p = Process.Start(psi);
            if (p == null) return false;
            // Must read streams before WaitForExit to avoid deadlocks
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            string output = stdout + stderr;

            int major = spec.Version >> 4;
            int minor = spec.Version & 0x0F;
            return output.Contains($"Lua {major}.{minor}");
        }
        catch
        {
            return false;
        }
    }

    private static bool CanRun(string binary)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = binary,
                Arguments = "-v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process? p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(5000);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
