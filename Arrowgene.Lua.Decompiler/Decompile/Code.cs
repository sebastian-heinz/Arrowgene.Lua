using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Code. A line-addressable view over an
/// <see cref="LFunction"/>'s bytecode that exposes individual operand fields,
/// jump targets, and disassembled forms while caching SETLIST extra-byte and
/// CLOSURE-upvalue metadata.
/// </summary>
public sealed class Code
{
    private readonly CodeExtract extractor;
    private readonly OpcodeMap map;
    private readonly int[] code;
    private readonly bool[] extraByte;
    private readonly bool[] upvalue;
    public readonly int length;

    public Code(LFunction function)
    {
        this.code = function.code;
        this.length = code.Length;
        map = function.header.opmap;
        extractor = function.header.extractor;
        extraByte = new bool[length];
        for (int i = 0; i < length; i++)
        {
            int line = i + 1;
            Op op = Op(line);
            extraByte[i] = op != null && op.HasExtraByte(Codepoint(line), extractor);
        }
        upvalue = new bool[length];
        if (function.header.version.upvaluedeclarationtype.Get() == Version.UpvalueDeclarationType.INLINE)
        {
            for (int i = 0; i < length; i++)
            {
                int line = i + 1;
                if (Op(line) == Decompile.Op.CLOSURE)
                {
                    int f = Bx(line);
                    if (f < function.functions.Length)
                    {
                        int nups = function.functions[f].numUpvalues;
                        for (int j = 1; j <= nups; j++)
                        {
                            if (i + j < length)
                            {
                                upvalue[i + j] = true;
                            }
                        }
                    }
                }
            }
        }
    }

    public CodeExtract GetExtractor() => extractor;

    /// <summary>Returns the operation indicated by the instruction at the given line.</summary>
    public Op Op(int line)
    {
        if (line >= 2 && extraByte[line - 2])
        {
            return Decompile.Op.EXTRABYTE;
        }
        return map.Get(Opcode(line));
    }

    public int Opcode(int line)
    {
        return extractor.op.Extract(code[line - 1]);
    }

    /// <summary>Returns the A field of the instruction at the given line.</summary>
    public int A(int line) => extractor.A.Extract(code[line - 1]);

    /// <summary>Returns the C field of the instruction at the given line.</summary>
    public int C(int line) => extractor.C.Extract(code[line - 1]);

    /// <summary>Returns the sC (signed C) field of the instruction at the given line.</summary>
    public int sC(int line)
    {
        int c = C(line);
        return c - extractor.C.Max() / 2;
    }

    public int vC(int line) => extractor.vC.Extract(code[line - 1]);

    /// <summary>Returns the k field of the instruction at the given line (1 is true, 0 is false).</summary>
    public bool k(int line) => extractor.k.Extract(code[line - 1]) != 0;

    /// <summary>Returns the B field of the instruction at the given line.</summary>
    public int B(int line) => extractor.B.Extract(code[line - 1]);

    /// <summary>Returns the sB (signed B) field of the instruction at the given line.</summary>
    public int sB(int line)
    {
        int b = B(line);
        return b - extractor.B.Max() / 2;
    }

    public int vB(int line) => extractor.vB.Extract(code[line - 1]);

    /// <summary>Returns the Ax field (A extended) of the instruction at the given line.</summary>
    public int Ax(int line) => extractor.Ax.Extract(code[line - 1]);

    /// <summary>Returns the Bx field (B extended) of the instruction at the given line.</summary>
    public int Bx(int line) => extractor.Bx.Extract(code[line - 1]);

    /// <summary>Returns the sBx field (signed B extended) of the instruction at the given line.</summary>
    public int sBx(int line) => extractor.sBx.Extract(code[line - 1]);

    public int Field(OperandFormat.Field f, int line)
    {
        return extractor.GetField(f).Extract(code[line - 1]);
    }

    /// <summary>
    /// Returns the absolute target address of a jump instruction at the given line.
    /// This field will be chosen automatically based on the opcode.
    /// </summary>
    public int Target(int line)
    {
        return line + 1 + Op(line).JumpField(Codepoint(line), extractor);
    }

    public int Register(int line)
    {
        return Op(line).Target(Codepoint(line), extractor);
    }

    /// <summary>Returns the full instruction codepoint at the given line.</summary>
    public int Codepoint(int line) => code[line - 1];

    public bool IsUpvalueDeclaration(int line) => upvalue[line - 1];

    public int Length() => code.Length;

    public string ToString(int line)
    {
        return Op(line).CodePointToString(0, (LFunction)null, Codepoint(line), extractor, (string)null, false);
    }
}
