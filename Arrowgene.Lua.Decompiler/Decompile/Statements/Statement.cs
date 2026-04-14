using System.Collections.Generic;

namespace Arrowgene.Lua.Decompiler.Decompile.Statements;

/// <summary>
/// Port of unluac.decompile.statement.Statement. Abstract base for every
/// statement node the decompiler emits (assignments, declarations, calls,
/// returns, labels, blocks). Provides <see cref="PrintSequence"/> for
/// emitting a list of statements with correct newline/semicolon handling.
/// </summary>
/// <remarks>
/// The C# namespace is pluralised (<c>Statements</c>) to match the
/// expression and target subpackages, even though Java permits a class
/// named identically to its package.
/// </remarks>
public abstract class Statement
{
    public string comment;

    /// <summary>
    /// Prints out a sequence of statements on separate lines. Correctly
    /// informs the last statement that it is last in a block.
    /// </summary>
    public static void PrintSequence(Decompiler d, Output @out, IList<Statement> stmts)
    {
        int n = stmts.Count;
        for (int i = 0; i < n; i++)
        {
            bool last = (i + 1 == n);
            Statement stmt = stmts[i];
            if (stmt.BeginsWithParen() && (i > 0 || d.GetVersion().allowpreceedingsemicolon.Get()))
            {
                @out.Print(";");
            }
            if (last)
            {
                stmt.PrintTail(d, @out);
            }
            else
            {
                stmt.Print(d, @out);
            }
            if (!stmt.SuppressNewline())
            {
                @out.PrintLn();
            }
        }
    }

    public abstract void Print(Decompiler d, Output @out);

    public virtual void PrintTail(Decompiler d, Output @out) => Print(d, @out);

    public void AddComment(string comment)
    {
        this.comment = comment;
    }

    public abstract void Walk(Walker w);

    public virtual bool BeginsWithParen() => false;

    public virtual bool SuppressNewline() => false;

    public virtual bool UseConstant(Function f, int index) => false;
}
