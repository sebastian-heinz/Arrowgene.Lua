using System.Collections.Generic;
using System.IO;
using Arrowgene.Lua.Decompiler.Decompile;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.AssemblerChunk. The top-level mutable state
/// the assembler builds while parsing one source file: the chosen
/// version, header layout fields, optional user type/op maps, the tree
/// of <see cref="AssemblerFunction"/> instances, and the
/// <see cref="CodeExtract"/> derived from the header sizes. Owns the
/// final <c>Write</c> that converts the staged data into a binary
/// <see cref="BHeader"/> ready to be serialised.
/// </summary>
internal sealed class AssemblerChunk
{
    public Version version;

    public int format;

    public LHeader.LEndianness endianness;

    public int int_size;
    public BIntegerType integer;

    public int size_t_size;
    public BIntegerType sizeT;

    public int instruction_size;
    public int op_size;
    public int a_size;
    public int b_size;
    public int c_size;

    public Dictionary<int, Type> usertypemap;
    public Dictionary<int, Op> useropmap;

    public bool number_integral;
    public int number_size;
    public LNumberType number;

    public LNumberType linteger;

    public LNumberType lfloat;

    public AssemblerFunction main;
    public AssemblerFunction current;
    public CodeExtract extract;

    public readonly HashSet<Directive> processed_directives;

    public AssemblerChunk(Version version)
    {
        this.version = version;
        processed_directives = new HashSet<Directive>();

        main = null;
        current = null;
        extract = null;
    }

    public void ProcessHeaderDirective(Assembler a, int line, Directive d)
    {
        if (!d.Repeatable() && processed_directives.Contains(d))
        {
            throw new AssemblerException(line, "Duplicate " + d + " directive");
        }
        processed_directives.Add(d);
        switch (d)
        {
            case Directive.FORMAT:
                format = a.GetInteger();
                break;
            case Directive.ENDIANNESS:
            {
                string endiannessName = a.GetName();
                switch (endiannessName)
                {
                    case "LITTLE":
                        endianness = LHeader.LEndianness.LITTLE;
                        break;
                    case "BIG":
                        endianness = LHeader.LEndianness.BIG;
                        break;
                    default:
                        throw new AssemblerException(line, "Unknown endianness \"" + endiannessName + "\"");
                }
                break;
            }
            case Directive.INT_SIZE:
                int_size = a.GetInteger();
                integer = BIntegerType.Create50Type(true, int_size, version.allownegativeint.Get());
                break;
            case Directive.SIZE_T_SIZE:
                size_t_size = a.GetInteger();
                sizeT = BIntegerType.Create50Type(false, size_t_size, false);
                break;
            case Directive.INSTRUCTION_SIZE:
                instruction_size = a.GetInteger();
                break;
            case Directive.SIZE_OP:
                op_size = a.GetInteger();
                break;
            case Directive.SIZE_A:
                a_size = a.GetInteger();
                break;
            case Directive.SIZE_B:
                b_size = a.GetInteger();
                break;
            case Directive.SIZE_C:
                c_size = a.GetInteger();
                break;
            case Directive.NUMBER_FORMAT:
            {
                string numberTypeName = a.GetName();
                switch (numberTypeName)
                {
                    case "integer": number_integral = true; break;
                    case "float": number_integral = false; break;
                    default: throw new AssemblerException(line, "Unknown number_format \"" + numberTypeName + "\"");
                }
                number_size = a.GetInteger();
                number = new LNumberType(number_size, number_integral, LNumberType.NumberMode.MODE_NUMBER);
                break;
            }
            case Directive.INTEGER_FORMAT:
                linteger = new LNumberType(a.GetInteger(), true, LNumberType.NumberMode.MODE_INTEGER);
                break;
            case Directive.FLOAT_FORMAT:
                lfloat = new LNumberType(a.GetInteger(), false, LNumberType.NumberMode.MODE_FLOAT);
                break;
            case Directive.TYPE:
            {
                if (usertypemap == null)
                {
                    usertypemap = new Dictionary<int, Type>();
                }
                int typecode = a.GetInteger();
                string name = a.GetName();
                Type type = Type.Get(name);
                if (type == null)
                {
                    throw new AssemblerException(line, "Unknown type name \"" + name + "\"");
                }
                usertypemap[typecode] = type;
                break;
            }
            case Directive.OP:
            {
                if (useropmap == null)
                {
                    useropmap = new Dictionary<int, Op>();
                }
                int opcode = a.GetInteger();
                string name = a.GetName();
                Op op = version.GetOpcodeMap().Get(name);
                if (op == null)
                {
                    throw new AssemblerException(line, "Unknown op name \"" + name + "\"");
                }
                useropmap[opcode] = op;
                break;
            }
            default:
                throw new System.InvalidOperationException("Unhandled directive: " + d);
        }
    }

    public CodeExtract GetCodeExtract()
    {
        if (extract == null)
        {
            extract = new CodeExtract(version, op_size, a_size, b_size, c_size);
        }
        return extract;
    }

    public void ProcessNewFunction(Assembler a, int line)
    {
        string name = a.GetName();
        string[] parts = name.Split('/');
        if (main == null)
        {
            if (parts.Length != 1) throw new AssemblerException(line, "First (main) function declaration must not have a \"/\" in the name");
            main = new AssemblerFunction(this, null, name);
            current = main;
        }
        else
        {
            if (parts.Length == 1 || !parts[0].Equals(main.name)) throw new AssemblerException(line, "Function \"" + name + "\" isn't contained in the main function");
            AssemblerFunction parent = main.GetInnerParent(parts, 1);
            if (parent == null)
            {
                throw new AssemblerException(line, "Can't find outer function");
            }
            current = parent.AddChild(parts[parts.Length - 1]);
        }
    }

    public void ProcessFunctionDirective(Assembler a, int line, Directive d)
    {
        if (current == null)
        {
            throw new AssemblerException(line, "Misplaced function directive before declaration of any function");
        }
        current.ProcessFunctionDirective(a, line, d);
    }

    public void ProcessOp(Assembler a, int line, Op op, int opcode)
    {
        if (current == null)
        {
            throw new AssemblerException(line, "Misplaced code before declaration of any function");
        }
        current.ProcessOp(a, line, GetCodeExtract(), op, opcode);
    }

    public void UpdateLastOp(int line, OperandFormat.Field fieldtype, int value)
    {
        current.UpdateLastOp(line, GetCodeExtract(), fieldtype, value);
    }

    public void Fixup()
    {
        main.Fixup(GetCodeExtract());
    }

    public void Write(Stream @out)
    {
        LBooleanType bool_ = new LBooleanType();
        LStringType string_ = version.GetLStringType();
        LConstantType constant = version.GetLConstantType();
        LAbsLineInfoType abslineinfo = new LAbsLineInfoType();
        LLocalType local = new LLocalType();
        LUpvalueType upvalue = version.GetLUpvalueType();
        LFunctionType function = version.GetLFunctionType();
        CodeExtract extract = GetCodeExtract();

        BIntegerType vinteger;
        if (version.GetVersionMinor() == 5)
        {
            // Lua 5.5 uses a variable-length size_t (unsigned BIntegerType54) for
            // strings / sizes / line numbers, an unsigned wrapper over it for the
            // general "integer" slot (with a nominal byte-width used by the header
            // .int_size directive), and a fixed-width signed BIntegerType50 for the
            // test integers that appear in the chunk header. Mirror what
            // LHeaderType55.ParseMain sets up so the writing path produces the
            // same bytes luac emits.
            sizeT = BIntegerType.Create54(false);
            integer = new BIntegerTypeWrapper(sizeT, false, int_size);
            vinteger = BIntegerType.Create50Type(true, int_size, true);
            if (linteger != null)
            {
                linteger = new LNumberTypeWrapper(
                    new BIntegerTypeWrapper(sizeT, true, linteger.size), linteger.size);
            }
        }
        else
        {
            if (integer == null)
            {
                integer = BIntegerType.Create54(true);
                sizeT = integer;
            }
            vinteger = integer;
        }

        TypeMap typemap;
        if (usertypemap != null)
        {
            typemap = new TypeMap(usertypemap);
        }
        else
        {
            typemap = version.GetTypeMap();
        }

        LHeader lheader = new LHeader(format, endianness, integer, vinteger, sizeT, bool_, number, linteger, lfloat, string_, constant, abslineinfo, local, upvalue, function, extract);
        BHeader header = new BHeader(version, lheader, typemap);
        LFunction main = ConvertFunction(header, this.main);
        header = new BHeader(version, lheader, typemap, main);

        header.Write(@out);
    }

    private LFunction ConvertFunction(BHeader header, AssemblerFunction function)
    {
        int i;
        int[] code = new int[function.code.Count];
        i = 0;
        foreach (int codepoint in function.code)
        {
            code[i++] = codepoint;
        }
        int[] lines = new int[function.lines.Count];
        i = 0;
        foreach (int line in function.lines)
        {
            lines[i++] = line;
        }
        LAbsLineInfo[] abslineinfo = new LAbsLineInfo[function.abslineinfo.Count];
        i = 0;
        foreach (AssemblerAbsLineInfo info in function.abslineinfo)
        {
            abslineinfo[i++] = new LAbsLineInfo(info.pc, info.line);
        }
        LLocal[] locals = new LLocal[function.locals.Count];
        i = 0;
        foreach (AssemblerLocal local_ in function.locals)
        {
            locals[i++] = new LLocal(ConvertString(header, local_.name), new BInteger(local_.begin), new BInteger(local_.end));
        }
        LObject[] constants = new LObject[function.constants.Count];
        i = 0;
        foreach (AssemblerConstant constant in function.constants)
        {
            LObject @object;
            switch (constant.type)
            {
                case AssemblerConstant.AssemblerConstantType.NIL:
                    @object = LNil.NIL;
                    break;
                case AssemblerConstant.AssemblerConstantType.BOOLEAN:
                    @object = constant.booleanValue ? LBoolean.LTRUE : LBoolean.LFALSE;
                    break;
                case AssemblerConstant.AssemblerConstantType.NUMBER:
                    @object = header.number.Create(constant.numberValue);
                    break;
                case AssemblerConstant.AssemblerConstantType.INTEGER:
                    @object = header.linteger.Create(constant.integerValue);
                    break;
                case AssemblerConstant.AssemblerConstantType.FLOAT:
                    @object = header.lfloat.Create(constant.numberValue);
                    break;
                case AssemblerConstant.AssemblerConstantType.STRING:
                    @object = ConvertString(header, constant.stringValue);
                    break;
                case AssemblerConstant.AssemblerConstantType.LONGSTRING:
                    @object = ConvertLongString(header, constant.stringValue);
                    break;
                case AssemblerConstant.AssemblerConstantType.NAN:
                    if (header.number != null)
                    {
                        @object = header.number.CreateNaN(constant.nanValue);
                    }
                    else
                    {
                        @object = header.lfloat.CreateNaN(constant.nanValue);
                    }
                    break;
                default:
                    throw new System.InvalidOperationException();
            }
            constants[i++] = @object;
        }
        LUpvalue[] upvalues = new LUpvalue[function.upvalues.Count];
        i = 0;
        foreach (AssemblerUpvalue upvalue_ in function.upvalues)
        {
            LUpvalue lup = new LUpvalue();
            lup.bname = ConvertString(header, upvalue_.name);
            lup.idx = upvalue_.index;
            lup.instack = upvalue_.instack;
            lup.kind = upvalue_.kind;
            upvalues[i++] = lup;
        }
        LFunction[] functions = new LFunction[function.children.Count];
        i = 0;
        foreach (AssemblerFunction f in function.children)
        {
            functions[i++] = ConvertFunction(header, f);
        }
        return new LFunction(
            header,
            ConvertString(header, function.source),
            function.linedefined,
            function.lastlinedefined,
            code,
            lines,
            abslineinfo,
            locals,
            constants,
            upvalues,
            functions,
            function.maxStackSize,
            function.upvalues.Count,
            function.numParams,
            function.vararg
        );
    }

    private LString ConvertString(BHeader header, string @string)
    {
        if (@string == null)
        {
            return LString.NULL;
        }
        return new LString(@string, '\0');
    }

    private LString ConvertLongString(BHeader header, string @string)
    {
        return new LString(@string, '\0', true);
    }
}
