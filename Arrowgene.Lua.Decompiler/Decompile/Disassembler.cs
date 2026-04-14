using Arrowgene.Lua.Decompiler.Assemble;
using Arrowgene.Lua.Decompiler.Parse;
using Arrowgene.Lua.Decompiler.Util;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Disassembler. Walks an <see cref="LFunction"/>
/// and emits the textual disassembly format consumed by
/// <see cref="Assembler"/>: a <c>.version</c> preamble, the chunk's
/// header directives, optional .type/.op user remappings, then a
/// <c>.function</c> block per nested function listing its
/// locals/upvalues/constants/labels/instructions before recursing into
/// the children.
/// </summary>
public sealed class Disassembler
{
    private readonly LFunction function;
    private readonly Code code;
    private readonly string name;
    private readonly string parent;

    public Disassembler(LFunction function) : this(function, "main", null) { }

    private Disassembler(LFunction function, string name, string parent)
    {
        this.function = function;
        this.code = new Code(function);
        this.name = name;
        this.parent = parent;
    }

    public void Disassemble(Output @out)
    {
        DisassembleInternal(@out, 0, 0);
    }

    private void DisassembleInternal(Output @out, int level, int index)
    {
        const int print_flags = PrintFlag.DISASSEMBLER;
        if (parent == null)
        {
            @out.PrintLn(".version\t" + function.header.version.GetName());
            @out.PrintLn();

            foreach (Directive directive in function.header.lheader_type.GetDirectives())
            {
                directive.Disassemble(@out, function.header, function.header.lheader);
            }
            @out.PrintLn();

            if (function.header.typemap != function.header.version.GetTypeMap())
            {
                TypeMap typemap = function.header.typemap;
                for (int typecode = 0; typecode < typemap.Size(); typecode++)
                {
                    Type type = typemap.Get(typecode);
                    if (type != null)
                    {
                        @out.PrintLn(Directive.TYPE.Token() + "\t" + typecode + "\t" + type.name);
                    }
                }
                @out.PrintLn();
            }

            if (function.header.opmap != function.header.version.GetOpcodeMap())
            {
                OpcodeMap opmap = function.header.opmap;
                for (int opcode = 0; opcode < opmap.Size(); opcode++)
                {
                    Op op = opmap.Get(opcode);
                    if (op != null)
                    {
                        @out.PrintLn(Directive.OP.Token() + "\t" + opcode + "\t" + op.Name);
                    }
                }
                @out.PrintLn();
            }
        }

        string fullname;
        if (parent == null)
        {
            fullname = name;
        }
        else
        {
            fullname = parent + "/" + name;
        }
        @out.PrintLn(".function\t" + fullname);
        @out.PrintLn();

        foreach (Directive directive in function.header.function.GetDirectives())
        {
            directive.Disassemble(@out, function.header, function, print_flags);
        }
        @out.PrintLn();

        if (function.locals.Length > 0)
        {
            for (int local = 1; local <= function.locals.Length; local++)
            {
                LLocal l = function.locals[local - 1];
                @out.PrintLn(".local\t" + l.name.ToPrintString(print_flags) + "\t" + l.start + "\t" + l.end);
            }
            @out.PrintLn();
        }

        if (function.upvalues.Length > 0)
        {
            for (int upvalue = 1; upvalue <= function.upvalues.Length; upvalue++)
            {
                LUpvalue u = function.upvalues[upvalue - 1];
                string line = ".upvalue\t" + StringUtils.ToPrintString(u.name) + "\t" + u.idx + "\t" + (u.instack ? "true" : "false");
                if (u.kind >= 0)
                {
                    line += "\t" + u.kind;
                }
                @out.PrintLn(line);
            }
            @out.PrintLn();
        }

        if (function.constants.Length > 0)
        {
            for (int constant = 1; constant <= function.constants.Length; constant++)
            {
                @out.PrintLn(".constant\tk" + (constant - 1) + "\t" + function.constants[constant - 1].ToPrintString(print_flags));
            }
            @out.PrintLn();
        }

        bool[] label = new bool[function.code.Length];
        for (int line = 1; line <= function.code.Length; line++)
        {
            Op op = code.Op(line);
            if (op != null && op.HasJump())
            {
                int target = code.Target(line);
                if (target >= 1 && target <= label.Length)
                {
                    label[target - 1] = true;
                }
            }
        }

        int abslineinfoindex = 0;
        int upvalue_count = 0;

        for (int line = 1; line <= function.code.Length; line++)
        {
            if (label[line - 1])
            {
                @out.PrintLn(".label\t" + "l" + line);
            }
            if (function.abslineinfo != null && abslineinfoindex < function.abslineinfo.Length && function.abslineinfo[abslineinfoindex].pc == line - 1)
            {
                LAbsLineInfo info = function.abslineinfo[abslineinfoindex++];
                @out.PrintLn(".abslineinfo\t" + info.pc + "\t" + info.line);
            }
            if (line <= function.lines.Length)
            {
                @out.Print(".line\t" + function.lines[line - 1] + "\t");
            }
            Op op_ = code.Op(line);
            string cpLabel = null;
            if (op_ != null && op_.HasJump())
            {
                int target = code.Target(line);
                if (target >= 1 && target <= code.Length())
                {
                    cpLabel = "l" + target;
                }
            }
            if (op_ == null)
            {
                @out.PrintLn(Op.DefaultToString(print_flags, function, code.Codepoint(line), function.header.version, code.GetExtractor(), upvalue_count > 0));
            }
            else
            {
                @out.PrintLn(op_.CodePointToString(print_flags, function, code.Codepoint(line), code.GetExtractor(), cpLabel, upvalue_count > 0));
            }
            if (upvalue_count > 0)
            {
                upvalue_count--;
            }
            else
            {
                if (op_ == Op.CLOSURE && function.header.version.upvaluedeclarationtype.Get() == Version.UpvalueDeclarationType.INLINE)
                {
                    int f = code.Bx(line);
                    if (f >= 0 && f < function.functions.Length)
                    {
                        LFunction closed = function.functions[f];
                        if (closed.numUpvalues > 0)
                        {
                            upvalue_count = closed.numUpvalues;
                        }
                    }
                }
            }
        }
        for (int line = function.code.Length + 1; line <= function.lines.Length; line++)
        {
            if (function.abslineinfo != null && abslineinfoindex < function.abslineinfo.Length && function.abslineinfo[abslineinfoindex].pc == line - 1)
            {
                LAbsLineInfo info = function.abslineinfo[abslineinfoindex++];
                @out.PrintLn(".abslineinfo\t" + info.pc + "\t" + info.line);
            }
            @out.PrintLn(".line\t" + function.lines[line - 1]);
        }
        if (function.abslineinfo != null)
        {
            while (abslineinfoindex < function.abslineinfo.Length)
            {
                LAbsLineInfo info = function.abslineinfo[abslineinfoindex++];
                @out.PrintLn(".abslineinfo\t" + info.pc + "\t" + info.line);
            }
        }
        @out.PrintLn();

        int subindex = 0;
        foreach (LFunction child in function.functions)
        {
            new Disassembler(child, "f" + subindex, fullname).DisassembleInternal(@out, level + 1, subindex);
            subindex++;
        }
    }
}
