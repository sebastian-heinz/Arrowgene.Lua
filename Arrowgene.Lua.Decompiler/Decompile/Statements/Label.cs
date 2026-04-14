namespace Arrowgene.Lua.Decompiler.Decompile.Statements;

/// <summary>
/// Port of unluac.decompile.statement.Label. A <c>::lbl_NN::</c> goto
/// target named after the bytecode line that defined it.
/// </summary>
public class Label : Statement
{
    private readonly string name;

    public Label(int line)
    {
        name = "lbl_" + line;
    }

    public override void Walk(Walker w) => w.VisitStatement(this);

    public override void Print(Decompiler d, Output @out)
    {
        @out.Print("::" + name + "::");
    }
}
