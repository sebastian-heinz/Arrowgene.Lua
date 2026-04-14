using System;
using System.IO;
using Arrowgene.Lua.Decompiler.Assemble;
using Arrowgene.Lua.Decompiler.Decompile;
using Arrowgene.Lua.Decompiler.Parse;
using Arrowgene.Lua.Decompiler.Util;

namespace Arrowgene.Lua.Decompiler;

/// <summary>
/// Port of unluac.Main. The LuaDecompiler project is a class library so
/// there is no <c>Main(string[])</c> entry point (C# would also reject a
/// static method with the same name as its enclosing class). Instead the
/// equivalent of the upstream <c>main()</c> lives on <see cref="Run"/>; a
/// thin <c>Exe</c> wrapper can call into it. <see cref="Error"/> stays
/// public because <see cref="Configuration.GetOutput"/> calls it on file
/// open failures.
/// </summary>
public static class Main
{
    public const string Version = "1.2.3.569";

    public static void Run(string[] args)
    {
        string fn = null;
        Configuration config = new Configuration();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith("-"))
            {
                // option
                if (arg.Equals("--rawstring"))
                {
                    config.rawstring = true;
                }
                else if (arg.Equals("--luaj"))
                {
                    config.luaj = true;
                }
                else if (arg.Equals("--nodebug"))
                {
                    config.variable = Configuration.VariableMode.NODEBUG;
                }
                else if (arg.Equals("--disassemble"))
                {
                    config.mode = Configuration.Mode.DISASSEMBLE;
                }
                else if (arg.Equals("--assemble"))
                {
                    config.mode = Configuration.Mode.ASSEMBLE;
                }
                else if (arg.Equals("--help"))
                {
                    config.mode = Configuration.Mode.HELP;
                }
                else if (arg.Equals("--version"))
                {
                    config.mode = Configuration.Mode.VERSION;
                }
                else if (arg.Equals("--output") || arg.Equals("-o"))
                {
                    if (i + 1 < args.Length)
                    {
                        config.output = args[i + 1];
                        i++;
                    }
                    else
                    {
                        Error("option \"" + arg + "\" doesn't have an argument", true);
                    }
                }
                else if (arg.Equals("--typemap"))
                {
                    if (i + 1 < args.Length)
                    {
                        config.typemap = args[i + 1];
                        i++;
                    }
                    else
                    {
                        Error("option \"" + arg + "\" doesn't have an argument", true);
                    }
                }
                else if (arg.Equals("--opmap"))
                {
                    if (i + 1 < args.Length)
                    {
                        config.opmap = args[i + 1];
                        i++;
                    }
                    else
                    {
                        Error("option \"" + arg + "\" doesn't have an argument", true);
                    }
                }
                else
                {
                    Error("unrecognized option: " + arg, true);
                }
            }
            else if (fn == null)
            {
                fn = arg;
            }
            else
            {
                Error("too many arguments: " + arg, true);
            }
        }
        if (fn == null && config.mode != Configuration.Mode.HELP && config.mode != Configuration.Mode.VERSION)
        {
            Error("no input file provided", true);
        }
        else
        {
            switch (config.mode)
            {
                case Configuration.Mode.HELP:
                    Help();
                    break;
                case Configuration.Mode.VERSION:
                    Console.Out.WriteLine(Version);
                    break;
                case Configuration.Mode.DECOMPILE:
                {
                    LFunction lmain = null;
                    try
                    {
                        lmain = FileToFunction(fn, config);
                    }
                    catch (IOException e)
                    {
                        Error(e.Message, false);
                    }
                    Decompile.Decompiler d = new Decompile.Decompiler(lmain);
                    Decompile.Decompiler.State result = d.Decompile();
                    Output output = config.GetOutput();
                    d.Print(result, output);
                    output.Finish();
                    break;
                }
                case Configuration.Mode.DISASSEMBLE:
                {
                    LFunction lmain = null;
                    try
                    {
                        lmain = FileToFunction(fn, config);
                    }
                    catch (IOException e)
                    {
                        Error(e.Message, false);
                    }
                    Disassembler d = new Disassembler(lmain);
                    Output output = config.GetOutput();
                    d.Disassemble(output);
                    output.Finish();
                    break;
                }
                case Configuration.Mode.ASSEMBLE:
                {
                    if (config.output == null)
                    {
                        Error("assembler mode requires an output file", true);
                    }
                    else
                    {
                        try
                        {
                            using Stream input = FileUtils.CreateSmartTextFileReader(fn);
                            using Stream outFile = new FileStream(config.output, FileMode.Create, FileAccess.Write);
                            Assembler a = new Assembler(config, input, outFile);
                            a.Assemble();
                        }
                        catch (IOException e)
                        {
                            Error(e.Message, false);
                        }
                        catch (AssemblerException e)
                        {
                            Error(e.Message, false);
                        }
                    }
                    break;
                }
                default:
                    throw new InvalidOperationException();
            }
            Environment.Exit(0);
        }
    }

    public static void Error(string message, bool usage)
    {
        PrintUnluacString(Console.Error);
        Console.Error.Write("  error: ");
        Console.Error.WriteLine(message);
        if (usage)
        {
            PrintUsage(Console.Error);
            Console.Error.WriteLine("For information about options, use option: --help");
        }
        Environment.Exit(1);
    }

    public static void Help()
    {
        PrintUnluacString(Console.Out);
        PrintUsage(Console.Out);
        Console.Out.WriteLine("Available options are:");
        Console.Out.WriteLine("  --assemble        assemble given disassembly listing");
        Console.Out.WriteLine("  --disassemble     disassemble instead of decompile");
        Console.Out.WriteLine("  --nodebug         ignore debugging information in input file");
        Console.Out.WriteLine("  --typemap <file>  use type mapping specified in <file>");
        Console.Out.WriteLine("  --opmap <file>    use opcode mapping specified in <file>");
        Console.Out.WriteLine("  --output <file>   output to <file> instead of stdout");
        Console.Out.WriteLine("  --rawstring       copy string bytes directly to output");
        Console.Out.WriteLine("  --luaj            emulate Luaj's permissive parser");
    }

    private static void PrintUnluacString(System.IO.TextWriter @out)
    {
        @out.WriteLine("unluac v" + Version);
    }

    private static void PrintUsage(System.IO.TextWriter @out)
    {
        @out.WriteLine("  usage: unluac [options] <file>");
    }

    private static LFunction FileToFunction(string fn, Configuration config)
    {
        using FileStream file = new FileStream(fn, FileMode.Open, FileAccess.Read);
        LuaByteBuffer buffer = LuaByteBuffer.ReadAll(file);
        buffer.Order(LuaByteBuffer.LITTLE_ENDIAN);
        buffer.Position(0);
        BHeader header = new BHeader(buffer, config);
        return header.main;
    }

    public static void DecompileToFile(string @in, string @out, Configuration config)
    {
        LFunction lmain = FileToFunction(@in, config);
        Decompile.Decompiler d = new Decompile.Decompiler(lmain);
        Decompile.Decompiler.State result = d.Decompile();
        using FileStream fs = new FileStream(@out, FileMode.Create, FileAccess.Write);
        Output output = new Output(new FileOutputProvider(fs));
        d.Print(result, output);
        output.Finish();
    }

    public static void AssembleToFile(string @in, string @out)
    {
        using Stream outstream = new FileStream(@out, FileMode.Create, FileAccess.Write);
        using Stream input = FileUtils.CreateSmartTextFileReader(@in);
        Assembler a = new Assembler(new Configuration(), input, outstream);
        a.Assemble();
        outstream.Flush();
    }

    public static void DisassembleToFile(string @in, string @out)
    {
        LFunction lmain = FileToFunction(@in, new Configuration());
        Disassembler d = new Disassembler(lmain);
        using FileStream fs = new FileStream(@out, FileMode.Create, FileAccess.Write);
        Output output = new Output(new FileOutputProvider(fs));
        d.Disassemble(output);
        output.Finish();
    }
}
