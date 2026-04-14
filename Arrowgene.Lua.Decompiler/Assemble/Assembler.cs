using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Arrowgene.Lua.Decompiler.Decompile;
using Arrowgene.Lua.Decompiler.Util;

namespace Arrowgene.Lua.Decompiler.Assemble;

/// <summary>
/// Port of unluac.assemble.Assembler. Reads the textual disassembly
/// produced by <c>Disassembler</c>, drives an <see cref="AssemblerChunk"/>
/// through the <c>.version</c>/header/function/op states, and writes the
/// assembled Lua chunk to the supplied <see cref="Stream"/>.
/// </summary>
/// <remarks>
/// The C# port keeps the upstream method/state-machine layout one-for-one:
/// <list type="bullet">
/// <item><see cref="GetAny"/>/<see cref="GetName"/>/<see cref="GetString"/>
/// pull whitespace-delimited tokens straight from the
/// <see cref="Tokenizer"/>; <see cref="GetInteger"/> and friends parse
/// numeric tokens with culture-invariant settings so float-formatted
/// constants round-trip exactly.</item>
/// <item><see cref="GetRegister"/>/<see cref="GetConstant"/>/
/// <see cref="GetUpvalue"/> recognise the single-letter register/constant/
/// upvalue prefix (<c>r0</c>, <c>k3</c>, <c>u2</c>) used in the
/// disassembly format, and <see cref="GetRegisterK54"/> handles the Lua
/// 5.4 <c>k</c>-bit fused operand.</item>
/// <item>Generic numeric opcodes (<c>op42</c>) fall through to
/// <see cref="ParseGenericOpcode"/>; if a token immediately after a code
/// instruction matches <c>field=</c>, the assembler back-patches the
/// previous opcode's matching <see cref="OperandFormat.Field"/>.</item>
/// </list>
/// </remarks>
public sealed class Assembler
{
    private Configuration config;
    private Tokenizer t;
    private Stream @out;
    private Version version;

    public Assembler(Configuration config, Stream @in, Stream @out)
    {
        this.config = config;
        t = new Tokenizer(@in);
        this.@out = @out;
    }

    public void Assemble()
    {
        string tok = t.Next();
        if (!".version".Equals(tok))
            throw new AssemblerException(t.Line(), "First directive must be .version, instead was \"" + tok + "\"");
        tok = t.Next();

        int major;
        int minor;
        string[] parts = tok.Split('.');
        if (parts.Length == 2)
        {
            try
            {
                major = int.Parse(parts[0], CultureInfo.InvariantCulture);
                minor = int.Parse(parts[1], CultureInfo.InvariantCulture);
            }
            catch (System.FormatException)
            {
                throw new AssemblerException(t.Line(), "Unsupported version " + tok);
            }
        }
        else
        {
            throw new AssemblerException(t.Line(), "Unsupported version " + tok);
        }
        if (major < 0 || major > 0xF || minor < 0 || minor > 0xF)
        {
            throw new AssemblerException(t.Line(), "Unsupported version " + tok);
        }

        version = Version.GetVersion(config, major, minor);

        if (version == null)
        {
            throw new AssemblerException(t.Line(), "Unsupported version " + tok);
        }

        Dictionary<string, Op> oplookup = null;
        Dictionary<Op, int> opcodelookup = null;

        AssemblerChunk chunk = new AssemblerChunk(version);
        bool opinit = false;
        bool postop = false;

        while ((tok = t.Next()) != null)
        {
            if (DirectiveExt.TryFromToken(tok, out Directive d))
            {
                postop = false;
                switch (d.Type())
                {
                    case DirectiveType.HEADER:
                        chunk.ProcessHeaderDirective(this, t.Line(), d);
                        break;
                    case DirectiveType.NEWFUNCTION:
                        if (!opinit)
                        {
                            opinit = true;
                            OpcodeMap opmap;
                            if (chunk.useropmap != null)
                            {
                                opmap = new OpcodeMap(chunk.useropmap);
                            }
                            else
                            {
                                opmap = version.GetOpcodeMap();
                            }
                            oplookup = new Dictionary<string, Op>();
                            opcodelookup = new Dictionary<Op, int>();
                            for (int i = 0; i < opmap.Size(); i++)
                            {
                                Op op = opmap.Get(i);
                                if (op != null)
                                {
                                    oplookup[op.Name] = op;
                                    opcodelookup[op] = i;
                                }
                            }

                            oplookup[Op.EXTRABYTE.Name] = Op.EXTRABYTE;
                            opcodelookup[Op.EXTRABYTE] = -1;
                        }

                        chunk.ProcessNewFunction(this, t.Line());
                        break;
                    case DirectiveType.FUNCTION:
                        chunk.ProcessFunctionDirective(this, t.Line(), d);
                        break;
                    default:
                        throw new System.InvalidOperationException();
                }
            }
            else
            {
                Op op = null;
                oplookup?.TryGetValue(tok, out op);
                if (op != null)
                {
                    chunk.ProcessOp(this, t.Line(), op, opcodelookup[op]);
                    postop = true;
                }
                else
                {
                    int opcode = ParseGenericOpcode(tok);
                    if (opcode >= 0)
                    {
                        chunk.ProcessOp(this, t.Line(), version.GetDefaultOp(), opcode);
                        postop = true;
                    }
                    else if (postop)
                    {
                        OperandFormat.Field? fieldtype = null;
                        foreach (OperandFormat opformat in version.GetDefaultOp().Operands)
                        {
                            if (tok.Equals(opformat.field.ToString().ToLowerInvariant() + "="))
                            {
                                fieldtype = opformat.field;
                                break;
                            }
                        }
                        if (fieldtype != null)
                        {
                            int value = GetInteger();
                            chunk.UpdateLastOp(t.Line(), fieldtype.Value, value);
                        }
                        else
                        {
                            throw new AssemblerException(t.Line(), "Unexpected token \"" + tok + "\"");
                        }
                    }
                    else
                    {
                        throw new AssemblerException(t.Line(), "Unexpected token \"" + tok + "\"");
                    }
                }
            }
        }

        chunk.Fixup();

        chunk.Write(@out);
    }

    internal string GetAny()
    {
        string s = t.Next();
        if (s == null) throw new AssemblerException(t.Line(), "Unexcepted end of file");
        return s;
    }

    internal string GetName()
    {
        string s = t.Next();
        if (s == null) throw new AssemblerException(t.Line(), "Unexcepted end of file");
        return s;
    }

    internal string GetString()
    {
        string s = t.Next();
        if (s == null) throw new AssemblerException(t.Line(), "Unexcepted end of file");
        return StringUtils.FromPrintString(s);
    }

    internal int GetInteger()
    {
        string s = t.Next();
        if (s == null) throw new AssemblerException(t.Line(), "Unexcepted end of file");
        int i;
        try
        {
            i = int.Parse(s, CultureInfo.InvariantCulture);
        }
        catch (System.FormatException)
        {
            throw new AssemblerException(t.Line(), "Excepted number, got \"" + s + "\"");
        }
        return i;
    }

    internal bool GetBoolean()
    {
        string s = t.Next();
        if (s == null) throw new AssemblerException(t.Line(), "Unexcepted end of file");
        bool b;
        if (s.Equals("true"))
        {
            b = true;
        }
        else if (s.Equals("false"))
        {
            b = false;
        }
        else
        {
            throw new AssemblerException(t.Line(), "Expected boolean, got \"" + s + "\"");
        }
        return b;
    }

    internal int GetRegister()
    {
        string s = t.Next();
        if (s == null) throw new AssemblerException(t.Line(), "Unexcepted end of file");
        int r;
        if (s.Length >= 2 && s[0] == 'r')
        {
            try
            {
                r = int.Parse(s.Substring(1), CultureInfo.InvariantCulture);
            }
            catch (System.FormatException)
            {
                throw new AssemblerException(t.Line(), "Excepted register, got \"" + s + "\"");
            }
        }
        else
        {
            throw new AssemblerException(t.Line(), "Excepted register, got \"" + s + "\"");
        }
        return r;
    }

    internal sealed class RKInfo
    {
        public int x;
        public bool constant;
    }

    internal RKInfo GetRegisterK54()
    {
        string s = t.Next();
        if (s == null) throw new AssemblerException(t.Line(), "Unexcepted end of file");
        RKInfo rk = new RKInfo();
        if (s.Length >= 2 && s[0] == 'r')
        {
            rk.constant = false;
            try
            {
                rk.x = int.Parse(s.Substring(1), CultureInfo.InvariantCulture);
            }
            catch (System.FormatException)
            {
                throw new AssemblerException(t.Line(), "Excepted register, got \"" + s + "\"");
            }
        }
        else if (s.Length >= 2 && s[0] == 'k')
        {
            rk.constant = true;
            try
            {
                rk.x = int.Parse(s.Substring(1), CultureInfo.InvariantCulture);
            }
            catch (System.FormatException)
            {
                throw new AssemblerException(t.Line(), "Excepted constant, got \"" + s + "\"");
            }
        }
        else
        {
            throw new AssemblerException(t.Line(), "Excepted register or constant, got \"" + s + "\"");
        }
        return rk;
    }

    internal int GetConstant()
    {
        string s = t.Next();
        if (s == null) throw new AssemblerException(t.Line(), "Unexpected end of file");
        int k;
        if (s.Length >= 2 && s[0] == 'k')
        {
            try
            {
                k = int.Parse(s.Substring(1), CultureInfo.InvariantCulture);
            }
            catch (System.FormatException)
            {
                throw new AssemblerException(t.Line(), "Excepted constant, got \"" + s + "\"");
            }
        }
        else
        {
            throw new AssemblerException(t.Line(), "Excepted constant, got \"" + s + "\"");
        }
        return k;
    }

    internal int GetUpvalue()
    {
        string s = t.Next();
        if (s == null) throw new AssemblerException(t.Line(), "Unexcepted end of file");
        int u;
        if (s.Length >= 2 && s[0] == 'u')
        {
            try
            {
                u = int.Parse(s.Substring(1), CultureInfo.InvariantCulture);
            }
            catch (System.FormatException)
            {
                throw new AssemblerException(t.Line(), "Excepted register, got \"" + s + "\"");
            }
        }
        else
        {
            throw new AssemblerException(t.Line(), "Excepted register, got \"" + s + "\"");
        }
        return u;
    }

    internal int ParseGenericOpcode(string tok)
    {
        if (!tok.StartsWith("op"))
        {
            return -1;
        }
        char first = tok[2];
        if (first < '0' || first > '9')
        {
            return -1;
        }
        try
        {
            return int.Parse(tok.Substring(2), CultureInfo.InvariantCulture);
        }
        catch (System.FormatException)
        {
            return -1;
        }
    }
}
