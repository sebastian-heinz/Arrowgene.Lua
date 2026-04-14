using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Categorises an assembler directive by what scope it belongs to.
/// Mirrors the upstream <c>unluac.assemble.DirectiveType</c> enum and
/// drives the assembler state machine: header-level directives populate
/// the <see cref="LHeader"/>, function-level directives populate the
/// current <see cref="LFunction"/>, <c>NEWFUNCTION</c> opens a nested
/// function scope, and <c>INSTRUCTION</c> emits a single opcode.
/// </summary>
public enum DirectiveType
{
    HEADER,
    NEWFUNCTION,
    FUNCTION,
    INSTRUCTION,
}

/// <summary>
/// Port of unluac.assemble.Directive. Identifies the textual disassembly
/// directives (<c>.format</c>, <c>.constant</c>, <c>.line</c>, ...) that
/// the assembler/disassembler exchange. Use <see cref="DirectiveExt"/> for
/// per-directive metadata (token spelling, scope, repeatability) and the
/// header/function disassemble helpers.
/// </summary>
public enum Directive
{
    FORMAT,
    ENDIANNESS,
    INT_SIZE,
    SIZE_T_SIZE,
    INSTRUCTION_SIZE,
    SIZE_OP,
    SIZE_A,
    SIZE_B,
    SIZE_C,
    NUMBER_FORMAT,
    INTEGER_FORMAT,
    FLOAT_FORMAT,
    TYPE,
    OP,
    FUNCTION,
    SOURCE,
    LINEDEFINED,
    LASTLINEDEFINED,
    NUMPARAMS,
    IS_VARARG,
    MAXSTACKSIZE,
    LABEL,
    CONSTANT,
    LINE,
    ABSLINEINFO,
    LOCAL,
    UPVALUE,
}

/// <summary>
/// Sidecar metadata for <see cref="Directive"/>. Holds the per-value
/// token/scope/repeatability that upstream Java packs into the enum
/// itself, plus the two <c>Disassemble</c> helpers used by the
/// disassembler when emitting header- and function-level directives.
/// </summary>
public static class DirectiveExt
{
    private sealed class Info
    {
        public readonly string Token;
        public readonly DirectiveType Type;
        public readonly bool Repeatable;

        public Info(string token, DirectiveType type, bool repeatable)
        {
            Token = token;
            Type = type;
            Repeatable = repeatable;
        }
    }

    private static readonly Dictionary<Directive, Info> Table = new Dictionary<Directive, Info>
    {
        { Directive.FORMAT,           new Info(".format",           DirectiveType.HEADER,      false) },
        { Directive.ENDIANNESS,       new Info(".endianness",       DirectiveType.HEADER,      false) },
        { Directive.INT_SIZE,         new Info(".int_size",         DirectiveType.HEADER,      false) },
        { Directive.SIZE_T_SIZE,      new Info(".size_t_size",      DirectiveType.HEADER,      false) },
        { Directive.INSTRUCTION_SIZE, new Info(".instruction_size", DirectiveType.HEADER,      false) },
        { Directive.SIZE_OP,          new Info(".size_op",          DirectiveType.HEADER,      false) },
        { Directive.SIZE_A,           new Info(".size_a",           DirectiveType.HEADER,      false) },
        { Directive.SIZE_B,           new Info(".size_b",           DirectiveType.HEADER,      false) },
        { Directive.SIZE_C,           new Info(".size_c",           DirectiveType.HEADER,      false) },
        { Directive.NUMBER_FORMAT,    new Info(".number_format",    DirectiveType.HEADER,      false) },
        { Directive.INTEGER_FORMAT,   new Info(".integer_format",   DirectiveType.HEADER,      false) },
        { Directive.FLOAT_FORMAT,     new Info(".float_format",     DirectiveType.HEADER,      false) },
        { Directive.TYPE,             new Info(".type",             DirectiveType.HEADER,      true)  },
        { Directive.OP,               new Info(".op",               DirectiveType.HEADER,      true)  },
        { Directive.FUNCTION,         new Info(".function",         DirectiveType.NEWFUNCTION, false) },
        { Directive.SOURCE,           new Info(".source",           DirectiveType.FUNCTION,    false) },
        { Directive.LINEDEFINED,      new Info(".linedefined",      DirectiveType.FUNCTION,    false) },
        { Directive.LASTLINEDEFINED,  new Info(".lastlinedefined",  DirectiveType.FUNCTION,    false) },
        { Directive.NUMPARAMS,        new Info(".numparams",        DirectiveType.FUNCTION,    false) },
        { Directive.IS_VARARG,        new Info(".is_vararg",        DirectiveType.FUNCTION,    false) },
        { Directive.MAXSTACKSIZE,     new Info(".maxstacksize",     DirectiveType.FUNCTION,    false) },
        { Directive.LABEL,            new Info(".label",            DirectiveType.FUNCTION,    false) },
        { Directive.CONSTANT,         new Info(".constant",         DirectiveType.FUNCTION,    false) },
        { Directive.LINE,             new Info(".line",             DirectiveType.FUNCTION,    false) },
        { Directive.ABSLINEINFO,      new Info(".abslineinfo",      DirectiveType.FUNCTION,    false) },
        { Directive.LOCAL,            new Info(".local",            DirectiveType.FUNCTION,    false) },
        { Directive.UPVALUE,          new Info(".upvalue",          DirectiveType.FUNCTION,    false) },
    };

    private static readonly Dictionary<string, Directive> Lookup = BuildLookup();

    private static Dictionary<string, Directive> BuildLookup()
    {
        var map = new Dictionary<string, Directive>();
        foreach (var kv in Table)
        {
            map[kv.Value.Token] = kv.Key;
        }
        return map;
    }

    public static string Token(this Directive d) => Table[d].Token;

    public static DirectiveType Type(this Directive d) => Table[d].Type;

    public static bool Repeatable(this Directive d) => Table[d].Repeatable;

    public static bool TryFromToken(string token, out Directive directive) =>
        Lookup.TryGetValue(token, out directive);

    public static void Disassemble(this Directive d, Output @out, BHeader chunk, LHeader header)
    {
        @out.Print(d.Token() + "\t");
        switch (d)
        {
            case Directive.FORMAT:
                @out.PrintLn(header.format.ToString());
                break;
            case Directive.ENDIANNESS:
                @out.PrintLn(header.endianness.ToString());
                break;
            case Directive.INT_SIZE:
                @out.PrintLn(header.integer.GetSize().ToString());
                break;
            case Directive.SIZE_T_SIZE:
                @out.PrintLn(header.sizeT.GetSize().ToString());
                break;
            case Directive.INSTRUCTION_SIZE:
                @out.PrintLn("4");
                break;
            case Directive.SIZE_OP:
                @out.PrintLn(header.extractor.op.size.ToString());
                break;
            case Directive.SIZE_A:
                @out.PrintLn(header.extractor.A.size.ToString());
                break;
            case Directive.SIZE_B:
                @out.PrintLn(header.extractor.B.size.ToString());
                break;
            case Directive.SIZE_C:
                @out.PrintLn(header.extractor.C.size.ToString());
                break;
            case Directive.NUMBER_FORMAT:
                @out.PrintLn((header.number.integral ? "integer" : "float") + "\t" + header.number.size);
                break;
            case Directive.INTEGER_FORMAT:
                @out.PrintLn(header.linteger.size.ToString());
                break;
            case Directive.FLOAT_FORMAT:
                @out.PrintLn(header.lfloat.size.ToString());
                break;
            default:
                throw new System.InvalidOperationException();
        }
    }

    public static void Disassemble(this Directive d, Output @out, BHeader chunk, LFunction function, int print_flags)
    {
        @out.Print(d.Token() + "\t");
        switch (d)
        {
            case Directive.SOURCE:
                @out.PrintLn(function.name.ToPrintString(print_flags));
                break;
            case Directive.LINEDEFINED:
                @out.PrintLn(function.linedefined.ToString());
                break;
            case Directive.LASTLINEDEFINED:
                @out.PrintLn(function.lastlinedefined.ToString());
                break;
            case Directive.NUMPARAMS:
                @out.PrintLn(function.numParams.ToString());
                break;
            case Directive.IS_VARARG:
                @out.PrintLn(function.vararg.ToString());
                break;
            case Directive.MAXSTACKSIZE:
                @out.PrintLn(function.maximumStackSize.ToString());
                break;
            default:
                throw new System.InvalidOperationException();
        }
    }
}
