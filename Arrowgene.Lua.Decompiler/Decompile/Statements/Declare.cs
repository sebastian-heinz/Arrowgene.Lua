using System.Collections.Generic;

namespace Arrowgene.Lua.Decompiler.Decompile.Statements;

/// <summary>
/// Port of unluac.decompile.statement.Declare. A bare <c>local x, y, z</c>
/// declaration with no initializer (initialized declarations are emitted
/// by <c>Assignment</c>).
/// </summary>
public class Declare : Statement
{
    private readonly IList<Declaration> decls;

    public Declare(IList<Declaration> decls)
    {
        this.decls = decls;
    }

    public override void Walk(Walker w) => w.VisitStatement(this);

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("local ");
        @out.Print(decls[0].name);
        for (int i = 1; i < decls.Count; i++)
        {
            @out.Print(", ");
            @out.Print(decls[i].name);
        }
    }
}
