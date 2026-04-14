using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Upvalues. Resolves upvalue names for
/// bytecode that was compiled without explicit upvalue names, by
/// chasing the parent declaration list (for in-stack upvalues) or
/// the parent's own upvalue table (for up-chain upvalues). Empty
/// or missing names are surfaced as <c>_UPVALUEn_</c> placeholders
/// so the decompiled output stays lexically valid.
/// </summary>
public class Upvalues
{
    private readonly LUpvalue[] upvalues;

    public Upvalues(LFunction func, Declaration[] parentDecls, int line)
    {
        upvalues = func.upvalues;
        foreach (LUpvalue upvalue in upvalues)
        {
            if (upvalue.name == null || upvalue.name.Length == 0)
            {
                if (upvalue.instack)
                {
                    if (parentDecls != null)
                    {
                        foreach (Declaration decl in parentDecls)
                        {
                            if (decl.register == upvalue.idx && line >= decl.begin && line < decl.end)
                            {
                                upvalue.name = decl.name;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    LUpvalue[] parentvals = func.parent.upvalues;
                    if (upvalue.idx >= 0 && upvalue.idx < parentvals.Length)
                    {
                        upvalue.name = parentvals[upvalue.idx].name;
                    }
                }
            }
        }
    }

    public string GetName(int index)
    {
        if (index < upvalues.Length && upvalues[index].name != null && upvalues[index].name.Length > 0)
        {
            return upvalues[index].name;
        }
        // TODO: SET ERROR
        return "_UPVALUE" + index + "_";
    }

    public UpvalueExpression GetExpression(int index)
    {
        return new UpvalueExpression(GetName(index));
    }
}
