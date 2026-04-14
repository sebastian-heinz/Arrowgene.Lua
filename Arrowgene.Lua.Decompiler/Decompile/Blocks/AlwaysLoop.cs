using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.AlwaysLoop. An unconditional loop
/// that prints as either <c>while true do ... end</c> (or
/// <c>while &lt;constant&gt; do ... end</c> if the compiler retained
/// a non-nil test constant) or <c>repeat ... until false</c>,
/// depending on the shape chosen at construction time and on the
/// version's WhileFormat. Absorbs a single constant via
/// <see cref="UseConstant"/> so the analyzer can preserve the
/// original test constant for display even though the loop is
/// always taken.
/// </summary>
public class AlwaysLoop : ContainerBlock
{
    private readonly bool repeat;

    private ConstantExpression condition;
    private Version.WhileFormat whileFormat;

    public AlwaysLoop(LFunction function, int begin, int end, CloseType closeType, int closeLine, bool repeat)
        : base(function, begin, end, closeType, closeLine, 0)
    {
        this.repeat = repeat;
        whileFormat = function.header.version.whileformat.Get();
        condition = null;
    }

    public override int ScopeEnd() => end - 2;

    public override bool Breakable() => true;

    public override bool HasHeader()
    {
        if (whileFormat == Version.WhileFormat.BOTTOM_CONDITION)
        {
            return !repeat;
        }
        return false;
    }

    public override bool IsUnprotected() => true;

    public override int GetUnprotectedTarget() => begin;

    public override int GetUnprotectedLine() => end - 1;

    public override int GetLoopback() => begin;

    public override void Print(Decompiler d, Output @out)
    {
        if (repeat)
        {
            @out.PrintLn("repeat");
        }
        else
        {
            @out.Print("while ");
            if (condition == null)
            {
                @out.Print("true");
            }
            else
            {
                condition.Print(d, @out);
            }
            @out.PrintLn(" do");
        }
        @out.Indent();
        Statement.PrintSequence(d, @out, statements);
        @out.Dedent();
        if (repeat)
        {
            @out.Print("until false");
        }
        else
        {
            @out.Print("end");
        }
    }

    public override bool UseConstant(Function f, int index)
    {
        if (!repeat && condition == null)
        {
            condition = f.GetConstantExpression(index);
            return true;
        }
        return false;
    }
}
