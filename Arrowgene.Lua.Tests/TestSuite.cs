using Arrowgene.Lua.Decompiler;
using Arrowgene.Lua.Decompiler.Assemble;
using Arrowgene.Lua.Decompiler.Parse;
using Arrowgene.Lua.Decompiler.Util;
using LuaDecompiler = Arrowgene.Lua.Decompiler.Decompile.Decompiler;
using Disassembler = Arrowgene.Lua.Decompiler.Decompile.Disassembler;
using Output = Arrowgene.Lua.Decompiler.Decompile.Output;
using FileOutputProvider = Arrowgene.Lua.Decompiler.Decompile.FileOutputProvider;

namespace Arrowgene.Lua.Tests;

/// <summary>
/// Port of unluac.test.TestSuite — orchestrates the compile → decompile → recompile → compare cycle.
/// </summary>
public class TestSuite
{
    public enum TestResult
    {
        OK,
        SKIPPED,
        FAILED,
    }

    private readonly string _srcPath;
    private readonly string _workingDir;

    public TestSuite(string srcPath, string workingDir)
    {
        _srcPath = srcPath;
        _workingDir = workingDir;
        Directory.CreateDirectory(workingDir);
    }

    /// <summary>
    /// Runs the full unluac test cycle for a single test file against a Lua version:
    /// 1. Compile .lua source with luac → luac.out
    /// 2. Decompile luac.out with the C# decompiler → unluac.out
    /// 3. Recompile unluac.out with luac → test.out
    /// 4. Compare bytecode of luac.out vs test.out
    /// </summary>
    public TestResult RunDecompileTest(LuaSpec spec, TestFile testfile)
    {
        string srcFile = Path.Combine(_srcPath, testfile.Name + ".lua");
        string compiled = Path.Combine(_workingDir, "luac.out");
        string decompiled = Path.Combine(_workingDir, "unluac.out");
        string recompiled = Path.Combine(_workingDir, "test.out");

        if (!File.Exists(srcFile))
            return TestResult.SKIPPED;

        // Step 1: Compile with luac
        try
        {
            LuaC.Compile(spec, srcFile, compiled);
        }
        catch (IOException)
        {
            return TestResult.SKIPPED;
        }

        try
        {
            // Step 2: Decompile with C# decompiler
            Configuration config = new Configuration();
            if (testfile.GetFlag(TestFile.RELAXED_SCOPE))
                config.strict_scope = false;
            else
                config.strict_scope = true;

            Decompile(compiled, decompiled, config);

            // Step 3: Recompile decompiled output
            LuaC.Compile(spec, decompiled, recompiled);

            // Step 4: Compare bytecode
            Compare compare = new Compare(Compare.Mode.NORMAL);
            return compare.BytecodeEqual(compiled, recompiled) ? TestResult.OK : TestResult.FAILED;
        }
        catch (Exception)
        {
            return TestResult.FAILED;
        }
    }

    /// <summary>
    /// Runs the disassemble → assemble → compare cycle:
    /// 1. Compile .lua source with luac → luac.out
    /// 2. Disassemble luac.out → unluac.out
    /// 3. Assemble unluac.out → test.out
    /// 4. Compare bytecode of luac.out vs test.out (FULL mode)
    /// </summary>
    public TestResult RunDisassembleTest(LuaSpec spec, TestFile testfile)
    {
        string srcFile = Path.Combine(_srcPath, testfile.Name + ".lua");
        string compiled = Path.Combine(_workingDir, "luac.out");
        string disassembled = Path.Combine(_workingDir, "unluac.out");
        string reassembled = Path.Combine(_workingDir, "test.out");

        if (!File.Exists(srcFile))
            return TestResult.SKIPPED;

        // Step 1: Compile with luac
        try
        {
            LuaC.Compile(spec, srcFile, compiled);
        }
        catch (IOException)
        {
            return TestResult.SKIPPED;
        }

        try
        {
            // Step 2: Disassemble
            Disassemble(compiled, disassembled);

            // Step 3: Assemble
            Assemble(disassembled, reassembled);

            // Step 4: Compare (FULL — all fields including line numbers)
            Compare compare = new Compare(Compare.Mode.FULL);
            return compare.BytecodeEqual(compiled, reassembled) ? TestResult.OK : TestResult.FAILED;
        }
        catch (Exception)
        {
            return TestResult.FAILED;
        }
    }

    /// <summary>
    /// Tests that a compiled .luac file can be parsed without errors.
    /// </summary>
    public TestResult RunParseTest(LuaSpec spec, TestFile testfile)
    {
        string srcFile = Path.Combine(_srcPath, testfile.Name + ".lua");
        string compiled = Path.Combine(_workingDir, "luac.out");

        if (!File.Exists(srcFile))
            return TestResult.SKIPPED;

        try
        {
            LuaC.Compile(spec, srcFile, compiled);
        }
        catch (IOException)
        {
            return TestResult.SKIPPED;
        }

        try
        {
            LFunction? main = Compare.FileToFunction(compiled);
            return main != null ? TestResult.OK : TestResult.FAILED;
        }
        catch (Exception)
        {
            return TestResult.FAILED;
        }
    }

    private static void Decompile(string input, string output, Configuration config)
    {
        using FileStream file = new FileStream(input, FileMode.Open, FileAccess.Read);
        LuaByteBuffer buffer = LuaByteBuffer.ReadAll(file);
        buffer.Order(LuaByteBuffer.LITTLE_ENDIAN);
        buffer.Position(0);
        BHeader header = new BHeader(buffer, config);
        LFunction lmain = header.main;

        LuaDecompiler d = new LuaDecompiler(lmain);
        LuaDecompiler.State result = d.Decompile();

        using FileStream fs = new FileStream(output, FileMode.Create, FileAccess.Write);
        Output @out = new Output(new FileOutputProvider(fs));
        d.Print(result, @out);
        @out.Finish();
    }

    private static void Disassemble(string input, string output)
    {
        using FileStream file = new FileStream(input, FileMode.Open, FileAccess.Read);
        LuaByteBuffer buffer = LuaByteBuffer.ReadAll(file);
        buffer.Order(LuaByteBuffer.LITTLE_ENDIAN);
        buffer.Position(0);
        BHeader header = new BHeader(buffer, new Configuration());
        LFunction lmain = header.main;

        Disassembler d = new Disassembler(lmain);
        using FileStream fs = new FileStream(output, FileMode.Create, FileAccess.Write);
        Output @out = new Output(new FileOutputProvider(fs));
        d.Disassemble(@out);
        @out.Finish();
    }

    private static void Assemble(string input, string output)
    {
        using Stream inStream = FileUtils.CreateSmartTextFileReader(input);
        using Stream outStream = new FileStream(output, FileMode.Create, FileAccess.Write);
        Assembler a = new Assembler(new Configuration(), inStream, outStream);
        a.Assemble();
        outStream.Flush();
    }
}
