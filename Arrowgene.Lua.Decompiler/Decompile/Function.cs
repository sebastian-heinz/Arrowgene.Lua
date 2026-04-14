using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Function. Wraps a parsed <see cref="LFunction"/>
/// with the decompiler-side bookkeeping: a line-addressable <see cref="Code"/>,
/// a typed <see cref="Constant"/> table, the active <see cref="CodeExtract"/>,
/// and the function name used in disassembler/exception messages. Also acts
/// as the factory for the expression-layer wrappers around constant-pool
/// entries.
/// </summary>
public sealed class Function
{
    private readonly Version version;
    private readonly string name;
    private readonly Function parent;
    public readonly Code code;
    public readonly Constant[] constants;
    private readonly CodeExtract extract;

    public Function(Function parent, int index, LFunction function)
    {
        name = parent == null ? "main" : index.ToString();
        this.parent = parent;
        version = function.header.version;
        code = new Code(function);
        constants = new Constant[function.constants.Length];
        for (int i = 0; i < constants.Length; i++)
        {
            constants[i] = new Constant(function.constants[i]);
        }
        extract = function.header.extractor;
    }

    public string DisassemblerName() => name;

    public string FullDisassemblerName()
    {
        return parent == null ? name : parent.FullDisassemblerName() + "/" + name;
    }

    public bool IsConstant(int register) => extract.IsK(register);

    public int ConstantIndex(int register) => extract.GetK(register);

    public ConstantExpression GetGlobalName(int constantIndex)
    {
        Constant constant = constants[constantIndex];
        if (!constant.IsIdentifierPermissive(version))
        {
            throw new System.InvalidOperationException();
        }
        return new ConstantExpression(constant, true, constantIndex);
    }

    public ConstantExpression GetConstantExpression(int constantIndex)
    {
        Constant constant = constants[constantIndex];
        return new ConstantExpression(constant, constant.IsIdentifier(version), constantIndex);
    }

    public GlobalExpression GetGlobalExpression(int constantIndex)
    {
        return new GlobalExpression(GetGlobalName(constantIndex), constantIndex);
    }

    public Version GetVersion() => version;
}
