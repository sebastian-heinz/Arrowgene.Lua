using System;
using Arrowgene.Lua.Decompiler.Decompile;

namespace Arrowgene.Lua.Decompiler.Parse;

/// <summary>
/// Port of unluac.parse.BHeader. Root entry point for reading a Lua chunk:
/// validates the 0x1B4C7561 signature, dispatches to the version-specific
/// <see cref="LHeaderType"/>, reads the main <see cref="LFunction"/>, and
/// applies user type/opcode maps.
/// </summary>
public sealed class BHeader
{
    private static readonly byte[] signature = new byte[] { 0x1B, 0x4C, 0x75, 0x61 };

    public readonly bool debug = false;

    public readonly Configuration config;
    public readonly Version version;
    public readonly LHeader lheader;
    public readonly LHeaderType lheader_type;
    public readonly BIntegerType integer;
    public readonly BIntegerType vinteger;
    public readonly BIntegerType sizeT;
    public readonly LBooleanType @bool;
    public readonly LNumberType number;
    public readonly LNumberType linteger;
    public readonly LNumberType lfloat;
    public readonly LStringType @string;
    public readonly LConstantType constant;
    public readonly LAbsLineInfoType abslineinfo;
    public readonly LLocalType local;
    public readonly LUpvalueType upvalue;
    public readonly LFunctionType function;
    public readonly CodeExtract extractor;
    public readonly TypeMap typemap;
    public readonly OpcodeMap opmap;

    public readonly LFunction main;

    public BHeader(Version version, LHeader lheader, TypeMap typemap)
        : this(version, lheader, typemap, null) { }

    public BHeader(Version version, LHeader lheader, TypeMap typemap, LFunction main)
    {
        this.config = null;
        this.version = version;
        this.lheader = lheader;
        this.lheader_type = version.GetLHeaderType();
        this.integer = lheader.integer;
        this.vinteger = lheader.vinteger;
        this.sizeT = lheader.sizeT;
        this.@bool = lheader.@bool;
        this.number = lheader.number;
        this.linteger = lheader.linteger;
        this.lfloat = lheader.lfloat;
        this.@string = lheader.@string;
        this.constant = lheader.constant;
        this.abslineinfo = lheader.abslineinfo;
        this.local = lheader.local;
        this.upvalue = lheader.upvalue;
        this.function = lheader.function;
        this.extractor = lheader.extractor;
        this.typemap = typemap;
        this.opmap = version.GetOpcodeMap();
        this.main = main;
    }

    public BHeader(LuaByteBuffer buffer, Configuration config)
    {
        this.config = config;
        // 4 byte Lua signature
        for (int i = 0; i < signature.Length; i++)
        {
            if (buffer.Get() != signature[i])
            {
                throw new InvalidOperationException("The input file does not have the signature of a valid Lua file.");
            }
        }

        int versionNumber = 0xFF & buffer.Get();
        int major = versionNumber >> 4;
        int minor = versionNumber & 0x0F;

        version = Version.GetVersion(config, major, minor);
        if (version == null)
        {
            throw new InvalidOperationException("The input chunk's Lua version is " + major + "." + minor + "; unluac can only handle Lua 5.0 - Lua 5.5.");
        }

        lheader_type = version.GetLHeaderType();
        lheader = lheader_type.Parse(buffer, this);
        integer = lheader.integer;
        vinteger = lheader.vinteger;
        sizeT = lheader.sizeT;
        @bool = lheader.@bool;
        number = lheader.number;
        linteger = lheader.linteger;
        lfloat = lheader.lfloat;
        @string = lheader.@string;
        constant = lheader.constant;
        abslineinfo = lheader.abslineinfo;
        local = lheader.local;
        upvalue = lheader.upvalue;
        function = lheader.function;
        extractor = lheader.extractor;

        if (config.typemap != null)
        {
            // TODO: port when unluac.assemble.Tokenizer lands.
            throw new NotImplementedException("User typemap files are not yet supported in the C# port.");
        }
        typemap = version.GetTypeMap();

        if (config.opmap != null)
        {
            // TODO: port when unluac.assemble.Tokenizer lands.
            throw new NotImplementedException("User opmap files are not yet supported in the C# port.");
        }
        opmap = version.GetOpcodeMap();

        int upvalues = -1;
        if (versionNumber >= 0x53)
        {
            upvalues = 0xFF & buffer.Get();
            if (debug)
            {
                Console.WriteLine("-- main chunk upvalue count: " + upvalues);
            }
            // TODO: check this value
        }
        main = function.Parse(buffer, this);
        if (upvalues >= 0)
        {
            if (main.numUpvalues != upvalues)
            {
                throw new InvalidOperationException("The main chunk has the wrong number of upvalues: " + main.numUpvalues + " (" + upvalues + " expected)");
            }
        }
        if (main.numUpvalues >= 1 && versionNumber >= 0x52
            && (main.upvalues[0].name == null || main.upvalues[0].name.Length == 0)
            && config.mode == Configuration.Mode.DECOMPILE)
        {
            main.upvalues[0].name = "_ENV";
        }
        main.SetLevel(1);
    }

    public void Write(System.IO.Stream @out)
    {
        @out.Write(signature, 0, signature.Length);
        int major = version.GetVersionMajor();
        int minor = version.GetVersionMinor();
        int versionNumber = (major << 4) | minor;
        @out.WriteByte((byte)versionNumber);
        version.GetLHeaderType().Write(@out, this, lheader);
        if (version.useupvaluecountinheader.Get())
        {
            @out.WriteByte((byte)main.numUpvalues);
        }
        function.Write(@out, this, main);
    }
}
