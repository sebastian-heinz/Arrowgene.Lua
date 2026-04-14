namespace Arrowgene.Lua.Tests;

/// <summary>
/// Port of unluac.test.RunTests — runs the full unluac test suite against
/// all supported Lua versions. Mirrors the official SourceForge test methodology:
/// compile → decompile → recompile → compare bytecode.
///
/// Tests are skipped when the corresponding luac binary is not available on PATH.
/// Install luac50, luac51, luac52, luac53, luac54, luac55 to run the full suite.
/// </summary>
public class RunTests : IDisposable
{
    private readonly TestSuite _suite;
    private readonly string _workingDir;

    /// <summary>
    /// Lua versions tested by the official unluac suite.
    /// Mirrors RunTests.java: 0x50, 0x51, 0x51/4, 0x52, 0x53, 0x54, 0x54/8, 0x55.
    /// </summary>
    private static readonly LuaSpec[] Specs =
    {
        new LuaSpec(0x50),
        new LuaSpec(0x51),
        new LuaSpec(0x51, 4),
        new LuaSpec(0x52),
        new LuaSpec(0x53),
        new LuaSpec(0x54),
        new LuaSpec(0x54, 8),
        new LuaSpec(0x55),
    };

    public RunTests()
    {
        string _srcPath = FindTestSrcPath();
        _workingDir = Path.Combine(Path.GetTempPath(), "unluac_test_" + Guid.NewGuid().ToString("N")[..8]);
        _suite = new TestSuite(_srcPath, _workingDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workingDir))
                Directory.Delete(_workingDir, true);
        }
        catch { }
    }

    public static IEnumerable<object[]> DecompileTestData()
    {
        foreach (LuaSpec spec in Specs)
        {
            foreach (TestFile tf in TestFiles.Tests)
            {
                if (spec.Compatible(tf))
                {
                    yield return new object[] { spec.Id(), tf.Name, tf.Version, tf.Flags };
                }
            }
        }
    }

    public static IEnumerable<object[]> DisassembleTestData()
    {
        foreach (LuaSpec spec in Specs)
        {
            foreach (TestFile tf in TestFiles.Tests)
            {
                if (spec.Compatible(tf))
                {
                    yield return new object[] { spec.Id(), tf.Name, tf.Version, tf.Flags };
                }
            }
        }
    }

    public static IEnumerable<object[]> ParseTestData()
    {
        foreach (LuaSpec spec in Specs)
        {
            foreach (TestFile tf in TestFiles.Tests)
            {
                if (spec.Compatible(tf))
                {
                    yield return new object[] { spec.Id(), tf.Name, tf.Version, tf.Flags };
                }
            }
        }
    }

    /// <summary>
    /// Decompile round-trip test: compile → decompile → recompile → compare bytecode.
    /// </summary>
    [Theory]
    [MemberData(nameof(DecompileTestData))]
    public void Decompile(string specId, string name, int version, int flags)
    {
        LuaSpec spec = ResolveSpec(specId);
        TestFile tf = new TestFile(name, version, flags);

        SkipIf(!LuaC.IsAvailable(spec), $"{spec.GetLuaCName()} not found on PATH");

        TestSuite.TestResult result = _suite.RunDecompileTest(spec, tf);
        SkipIf(result == TestSuite.TestResult.SKIPPED, $"Test source not found: {name}.lua");

        Assert.Equal(TestSuite.TestResult.OK, result);
    }

    /// <summary>
    /// Disassemble round-trip test: compile → disassemble → assemble → compare bytecode (FULL).
    /// </summary>
    [Theory]
    [MemberData(nameof(DisassembleTestData))]
    public void Disassemble(string specId, string name, int version, int flags)
    {
        LuaSpec spec = ResolveSpec(specId);
        TestFile tf = new TestFile(name, version, flags);

        SkipIf(!LuaC.IsAvailable(spec), $"{spec.GetLuaCName()} not found on PATH");

        TestSuite.TestResult result = _suite.RunDisassembleTest(spec, tf);
        SkipIf(result == TestSuite.TestResult.SKIPPED, $"Test source not found: {name}.lua");

        Assert.Equal(TestSuite.TestResult.OK, result);
    }

    /// <summary>
    /// Parse-only test: compile → parse bytecode without errors.
    /// </summary>
    [Theory]
    [MemberData(nameof(ParseTestData))]
    public void Parse(string specId, string name, int version, int flags)
    {
        LuaSpec spec = ResolveSpec(specId);
        TestFile tf = new TestFile(name, version, flags);

        SkipIf(!LuaC.IsAvailable(spec), $"{spec.GetLuaCName()} not found on PATH");

        TestSuite.TestResult result = _suite.RunParseTest(spec, tf);
        SkipIf(result == TestSuite.TestResult.SKIPPED, $"Test source not found: {name}.lua");

        Assert.Equal(TestSuite.TestResult.OK, result);
    }

    private static void SkipIf(bool condition, string reason)
    {
        if (condition)
            Assert.Skip(reason);
    }

    private static LuaSpec ResolveSpec(string specId)
    {
        foreach (LuaSpec s in Specs)
        {
            if (s.Id() == specId) return s;
        }
        throw new ArgumentException($"Unknown spec: {specId}");
    }

    private static string FindTestSrcPath()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "files");
            if (Directory.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "files");
    }
}
