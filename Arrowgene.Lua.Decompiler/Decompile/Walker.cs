using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Walker. The base no-op visitor used by the
/// VariableFinder and other analysis passes to traverse the statement and
/// expression trees produced by the decompiler.
/// </summary>
public class Walker
{
    public virtual void VisitStatement(Statement stmt)
    {
    }

    public virtual void VisitExpression(Expression expr)
    {
    }
}
