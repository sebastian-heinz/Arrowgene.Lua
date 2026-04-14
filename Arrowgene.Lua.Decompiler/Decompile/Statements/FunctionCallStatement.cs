using Arrowgene.Lua.Decompiler.Decompile.Expressions;

namespace Arrowgene.Lua.Decompiler.Decompile.Statements;

/// <summary>
/// Port of unluac.decompile.statement.FunctionCallStatement. Wraps a
/// <see cref="FunctionCall"/> used in statement position
/// (<c>f(x)</c> rather than <c>local y = f(x)</c>).
/// </summary>
public class FunctionCallStatement : Statement
{
    private readonly FunctionCall call;

    public FunctionCallStatement(FunctionCall call)
    {
        this.call = call;
    }

    public override void Walk(Walker w)
    {
        w.VisitStatement(this);
        call.Walk(w);
    }

    public override void Print(Decompiler d, Output @out)
    {
        call.Print(d, @out);
    }

    public override bool BeginsWithParen() => call.BeginsWithParen();
}
