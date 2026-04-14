using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.ElseEndBlock. The <c>else ... end</c>
/// partner of an <see cref="IfThenElseBlock"/>. Compares greater-than
/// its partner so the if-half processes first, and folds nested
/// <c>elseif</c> cases inline: a lone nested <see cref="IfThenEndBlock"/>
/// or an <see cref="IfThenElseBlock"/>+<see cref="ElseEndBlock"/> pair
/// prints as <c>elseif</c> without a fresh <c>else ... end</c> frame.
/// </summary>
public class ElseEndBlock : ContainerBlock
{
    public IfThenElseBlock partner;

    public ElseEndBlock(LFunction function, int begin, int end, CloseType closeType, int closeLine)
        : base(function, begin, end, closeType, closeLine, -1)
    {
    }

    public override int CompareTo(Block block)
    {
        if (block == partner)
        {
            return 1;
        }
        return base.CompareTo(block);
    }

    public override bool Breakable() => false;

    public override bool HasHeader() => true;

    public override bool IsUnprotected() => false;

    public override int GetLoopback() => throw new System.InvalidOperationException();

    public override void Print(Decompiler d, Output @out)
    {
        if (statements.Count == 1 && statements[0] is IfThenEndBlock)
        {
            @out.Print("else");
            statements[0].Print(d, @out);
        }
        else if (statements.Count == 2 && statements[0] is IfThenElseBlock && statements[1] is ElseEndBlock)
        {
            @out.Print("else");
            statements[0].Print(d, @out);
            statements[1].Print(d, @out);
        }
        else
        {
            @out.Print("else");
            @out.PrintLn();
            @out.Indent();
            Statement.PrintSequence(d, @out, statements);
            @out.Dedent();
            @out.Print("end");
        }
    }
}
