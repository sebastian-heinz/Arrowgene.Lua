using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Arrowgene.Lua.Decompiler.Decompile;
using Arrowgene.Lua.Decompiler.Util;

namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.AssemblerFunction. The mutable scratch
/// buffer that the assembler builds up as it walks the textual input
/// for one Lua function: collected directives populate the metadata,
/// labels and constants tables; <c>processOp</c> appends bytecode and
/// queues function- and jump-relocation records for the post-parse
/// fixup pass to resolve once every label and child function exists.
/// </summary>
internal sealed class AssemblerFunction
{
    internal sealed class FunctionFixup
    {
        public int line;
        public int code_index;
        public string function;
        public CodeExtract.Field field;
    }

    internal sealed class JumpFixup
    {
        public int line;
        public int code_index;
        public string label;
        public CodeExtract.Field field;
        public bool negate;
    }

    public AssemblerChunk chunk;
    public AssemblerFunction parent;
    public string name;
    public List<AssemblerFunction> children;

    public bool hasSource;
    public string source;

    public bool hasLineDefined;
    public int linedefined;

    public bool hasLastLineDefined;
    public int lastlinedefined;

    public bool hasMaxStackSize;
    public int maxStackSize;

    public bool hasNumParams;
    public int numParams;

    public bool hasVararg;
    public int vararg;

    public List<AssemblerLabel> labels;
    public List<AssemblerConstant> constants;
    public List<AssemblerUpvalue> upvalues;
    public List<int> code;
    public List<int> lines;
    public List<AssemblerAbsLineInfo> abslineinfo;
    public List<AssemblerLocal> locals;

    public List<FunctionFixup> f_fixup;
    public List<JumpFixup> j_fixup;

    public AssemblerFunction(AssemblerChunk chunk, AssemblerFunction parent, string name)
    {
        this.chunk = chunk;
        this.parent = parent;
        this.name = name;
        children = new List<AssemblerFunction>();

        hasSource = false;
        hasLineDefined = false;
        hasLastLineDefined = false;
        hasMaxStackSize = false;
        hasNumParams = false;
        hasVararg = false;

        labels = new List<AssemblerLabel>();
        constants = new List<AssemblerConstant>();
        upvalues = new List<AssemblerUpvalue>();
        code = new List<int>();
        lines = new List<int>();
        abslineinfo = new List<AssemblerAbsLineInfo>();
        locals = new List<AssemblerLocal>();

        f_fixup = new List<FunctionFixup>();
        j_fixup = new List<JumpFixup>();
    }

    public AssemblerFunction AddChild(string name)
    {
        AssemblerFunction child = new AssemblerFunction(chunk, this, name);
        children.Add(child);
        return child;
    }

    public AssemblerFunction GetInnerParent(string[] parts, int index)
    {
        if (index + 1 == parts.Length) return this;
        foreach (AssemblerFunction child in children)
        {
            if (child.name.Equals(parts[index]))
            {
                return child.GetInnerParent(parts, index + 1);
            }
        }
        return null;
    }

    public void ProcessFunctionDirective(Assembler a, int line, Directive d)
    {
        switch (d)
        {
            case Directive.SOURCE:
                if (hasSource) throw new AssemblerException(line, "Duplicate .source directive");
                hasSource = true;
                source = a.GetString();
                break;
            case Directive.LINEDEFINED:
                if (hasLineDefined) throw new AssemblerException(line, "Duplicate .linedefined directive");
                hasLineDefined = true;
                linedefined = a.GetInteger();
                break;
            case Directive.LASTLINEDEFINED:
                if (hasLastLineDefined) throw new AssemblerException(line, "Duplicate .lastlinedefined directive");
                hasLastLineDefined = true;
                lastlinedefined = a.GetInteger();
                break;
            case Directive.MAXSTACKSIZE:
                if (hasMaxStackSize) throw new AssemblerException(line, "Duplicate .maxstacksize directive");
                hasMaxStackSize = true;
                maxStackSize = a.GetInteger();
                break;
            case Directive.NUMPARAMS:
                if (hasNumParams) throw new AssemblerException(line, "Duplicate .numparams directive");
                hasNumParams = true;
                numParams = a.GetInteger();
                break;
            case Directive.IS_VARARG:
                if (hasVararg) throw new AssemblerException(line, "Duplicate .is_vararg directive");
                hasVararg = true;
                vararg = a.GetInteger();
                break;
            case Directive.LABEL:
            {
                string lname = a.GetAny();
                AssemblerLabel label = new AssemblerLabel();
                label.name = lname;
                label.code_index = code.Count;
                labels.Add(label);
                break;
            }
            case Directive.CONSTANT:
            {
                string cname = a.GetName();
                string value = a.GetAny();
                AssemblerConstant constant = new AssemblerConstant();
                constant.name = cname;
                if (value.Equals("nil"))
                {
                    constant.type = AssemblerConstant.AssemblerConstantType.NIL;
                }
                else if (value.Equals("true"))
                {
                    constant.type = AssemblerConstant.AssemblerConstantType.BOOLEAN;
                    constant.booleanValue = true;
                }
                else if (value.Equals("false"))
                {
                    constant.type = AssemblerConstant.AssemblerConstantType.BOOLEAN;
                    constant.booleanValue = false;
                }
                else if (value.StartsWith("\""))
                {
                    constant.type = AssemblerConstant.AssemblerConstantType.STRING;
                    constant.stringValue = StringUtils.FromPrintString(value);
                }
                else if (value.StartsWith("L\""))
                {
                    constant.type = AssemblerConstant.AssemblerConstantType.LONGSTRING;
                    constant.stringValue = StringUtils.FromPrintString(value.Substring(1));
                }
                else if (value.Equals("null"))
                {
                    constant.type = AssemblerConstant.AssemblerConstantType.STRING;
                    constant.stringValue = null;
                }
                else if (value.Equals("NaN"))
                {
                    constant.type = AssemblerConstant.AssemblerConstantType.NAN;
                    constant.nanValue = 0;
                }
                else
                {
                    try
                    {
                        if (value.StartsWith("NaN+") || value.StartsWith("NaN-"))
                        {
                            ulong ubits = ulong.Parse(value.Substring(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            long bits = unchecked((long)ubits);
                            if (bits < 0 || (bits & System.BitConverter.DoubleToInt64Bits(double.NaN)) != 0)
                            {
                                throw new AssemblerException(line, "Unrecognized NaN value: " + value);
                            }
                            if (value.StartsWith("NaN-"))
                            {
                                bits ^= unchecked((long)0x8000000000000000UL);
                            }
                            constant.type = AssemblerConstant.AssemblerConstantType.NAN;
                            constant.nanValue = bits;
                        }
                        else if (chunk.number != null) // TODO: better check
                        {
                            constant.numberValue = double.Parse(value, CultureInfo.InvariantCulture);
                            constant.type = AssemblerConstant.AssemblerConstantType.NUMBER;
                        }
                        else
                        {
                            if (value.Contains(".") || value.Contains("E") || value.Contains("e"))
                            {
                                constant.numberValue = double.Parse(value, CultureInfo.InvariantCulture);
                                constant.type = AssemblerConstant.AssemblerConstantType.FLOAT;
                            }
                            else
                            {
                                constant.integerValue = BigInteger.Parse(value, CultureInfo.InvariantCulture);
                                constant.type = AssemblerConstant.AssemblerConstantType.INTEGER;
                            }
                        }
                    }
                    catch (System.FormatException)
                    {
                        throw new AssemblerException(line, "Unrecognized constant value: " + value);
                    }
                }
                constants.Add(constant);
                break;
            }
            case Directive.LINE:
            {
                lines.Add(a.GetInteger());
                break;
            }
            case Directive.ABSLINEINFO:
            {
                AssemblerAbsLineInfo info = new AssemblerAbsLineInfo();
                info.pc = a.GetInteger();
                info.line = a.GetInteger();
                abslineinfo.Add(info);
                break;
            }
            case Directive.LOCAL:
            {
                AssemblerLocal local = new AssemblerLocal();
                local.name = a.GetString();
                local.begin = a.GetInteger();
                local.end = a.GetInteger();
                locals.Add(local);
                break;
            }
            case Directive.UPVALUE:
            {
                AssemblerUpvalue upvalue = new AssemblerUpvalue();
                upvalue.name = a.GetString();
                upvalue.index = a.GetInteger();
                upvalue.instack = a.GetBoolean();
                if (chunk.version.GetLUpvalueType().HasKind)
                {
                    upvalue.kind = a.GetInteger();
                }
                upvalues.Add(upvalue);
                break;
            }
            default:
                throw new System.InvalidOperationException("Unhandled directive: " + d);
        }
    }

    public void ProcessOp(Assembler a, int line, CodeExtract extract, Op op, int opcode)
    {
        if (!hasMaxStackSize) throw new AssemblerException(line, "Expected .maxstacksize before code");
        if (opcode >= 0 && !extract.op.Check(opcode)) throw new System.InvalidOperationException("Invalid opcode: " + opcode);
        int codepoint = opcode >= 0 ? extract.op.Encode(opcode) : 0;
        foreach (OperandFormat operand in op.Operands)
        {
            CodeExtract.Field field = extract.GetField(operand.field);
            int x;
            switch (operand.format)
            {
                case OperandFormat.Format.RAW:
                case OperandFormat.Format.IMMEDIATE_INTEGER:
                case OperandFormat.Format.IMMEDIATE_FLOAT:
                    x = a.GetInteger();
                    break;
                case OperandFormat.Format.IMMEDIATE_SIGNED_INTEGER:
                    x = a.GetInteger();
                    x += field.Max() / 2;
                    break;
                case OperandFormat.Format.REGISTER:
                {
                    x = a.GetRegister();
                    //TODO: stack warning
                    break;
                }
                case OperandFormat.Format.REGISTER_K:
                {
                    Assembler.RKInfo rk = a.GetRegisterK54();
                    x = rk.x;
                    if (rk.constant)
                    {
                        x += chunk.version.rkoffset.Get().Value;
                    }
                    //TODO: stack warning
                    break;
                }
                case OperandFormat.Format.REGISTER_K54:
                {
                    Assembler.RKInfo rk = a.GetRegisterK54();
                    codepoint |= extract.k.Encode(rk.constant ? 1 : 0);
                    x = rk.x;
                    break;
                }
                case OperandFormat.Format.CONSTANT:
                case OperandFormat.Format.CONSTANT_INTEGER:
                case OperandFormat.Format.CONSTANT_STRING:
                {
                    x = a.GetConstant();
                    break;
                }
                case OperandFormat.Format.UPVALUE:
                {
                    x = a.GetUpvalue();
                    break;
                }
                case OperandFormat.Format.FUNCTION:
                {
                    FunctionFixup fix = new FunctionFixup();
                    fix.line = line;
                    fix.code_index = code.Count;
                    fix.function = a.GetAny();
                    fix.field = field;
                    f_fixup.Add(fix);
                    x = 0;
                    break;
                }
                case OperandFormat.Format.JUMP:
                {
                    JumpFixup fix = new JumpFixup();
                    fix.line = line;
                    fix.code_index = code.Count;
                    fix.label = a.GetAny();
                    fix.field = field;
                    fix.negate = false;
                    j_fixup.Add(fix);
                    x = 0;
                    break;
                }
                case OperandFormat.Format.JUMP_NEGATIVE:
                {
                    JumpFixup fix = new JumpFixup();
                    fix.line = line;
                    fix.code_index = code.Count;
                    fix.label = a.GetAny();
                    fix.field = field;
                    fix.negate = true;
                    j_fixup.Add(fix);
                    x = 0;
                    break;
                }
                default:
                    throw new System.InvalidOperationException("Unhandled operand format: " + operand.format);
            }
            if (!field.Check(x))
            {
                throw new AssemblerException(line, "Operand " + operand.field + " out of range");
            }
            codepoint |= field.Encode(x);
        }
        code.Add(codepoint);
    }

    public void UpdateLastOp(int line, CodeExtract extract, OperandFormat.Field fieldtype, int x)
    {
        int index = code.Count - 1;
        int codepoint = code[index];

        CodeExtract.Field field = extract.GetField(fieldtype);

        if (!field.Check(x))
        {
            throw new AssemblerException(line, "Field " + fieldtype + " out of range");
        }

        codepoint &= ~field.Mask();
        codepoint |= field.Encode(x);

        code[index] = codepoint;
    }

    public void Fixup(CodeExtract extract)
    {
        foreach (FunctionFixup fix in f_fixup)
        {
            int codepoint = code[fix.code_index];
            int x = -1;
            for (int f = 0; f < children.Count; f++)
            {
                AssemblerFunction child = children[f];
                if (fix.function.Equals(child.name))
                {
                    x = f;
                    break;
                }
            }
            if (x == -1)
            {
                throw new AssemblerException(fix.line, "Unknown function: " + fix.function);
            }
            codepoint = fix.field.Clear(codepoint);
            codepoint |= fix.field.Encode(x);
            code[fix.code_index] = codepoint;
        }

        foreach (JumpFixup fix in j_fixup)
        {
            int codepoint = code[fix.code_index];
            int x = 0;
            bool found = false;
            foreach (AssemblerLabel label in labels)
            {
                if (fix.label.Equals(label.name))
                {
                    x = label.code_index - fix.code_index - 1;
                    if (fix.negate) x = -x;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                throw new AssemblerException(fix.line, "Unknown label: " + fix.label);
            }
            codepoint = fix.field.Clear(codepoint);
            codepoint |= fix.field.Encode(x);
            code[fix.code_index] = codepoint;
        }

        foreach (AssemblerFunction f in children)
        {
            f.Fixup(extract);
        }
    }
}
