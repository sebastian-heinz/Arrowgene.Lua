using System;
using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Operations;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile.Blocks;

/// <summary>
/// Port of unluac.decompile.block.Block. The abstract base for every
/// control-flow construct the decompiler emits as a self-contained
/// region of statements: do/end blocks, if/then/else, loops, etc.
/// A Block is itself a <see cref="Statement"/> so that nested blocks can
/// participate in the parent block's statement sequence.
/// </summary>
/// <remarks>
/// The C# namespace is pluralised (<c>Blocks</c>) to match the other
/// subpackages and avoid colliding with the <see cref="Block"/> class
/// name itself. Concrete block subclasses (DoEndBlock, IfThenElseBlock,
/// loops, ...) land in follow-up commits.
/// </remarks>
public abstract class Block : Statement, IComparable<Block>
{
    protected readonly LFunction function;
    public int begin;
    public int end;
    public int closeRegister;
    private readonly int priority;
    public bool loopRedirectAdjustment = false;
    protected bool scopeUsed = false;

    protected Block(LFunction function, int begin, int end, int priority)
    {
        this.function = function;
        this.begin = begin;
        this.end = end;
        closeRegister = -1;
        this.priority = priority;
    }

    public abstract void AddStatement(Statement statement);

    public virtual void Resolve(Registers r) { }

    public bool Contains(Block block) => Contains(block.begin, block.end);

    public bool Contains(int line) => begin <= line && line < end;

    public bool Contains(int begin, int end) => this.begin <= begin && this.end >= end;

    public virtual int ScopeEnd() => end - 1;

    public void UseScope() { scopeUsed = true; }

    public virtual bool HasCloseLine() => false;

    public virtual int GetCloseLine() => throw new InvalidOperationException();

    public virtual CloseType GetCloseType() => throw new InvalidOperationException();

    public virtual void UseClose() => throw new InvalidOperationException();

    public abstract bool HasHeader();

    /// <summary>
    /// An unprotected block is one that ends in a JMP instruction.
    /// If this is the case, any inner statement that tries to jump
    /// to the end of this block will be redirected.
    ///
    /// (One of the Lua compiler's few optimizations is that it changes
    /// any JMP that targets another JMP to the ultimate target.)
    /// </summary>
    public abstract bool IsUnprotected();

    public virtual int GetUnprotectedTarget() => throw new InvalidOperationException(ToString());

    public virtual int GetUnprotectedLine() => throw new InvalidOperationException(ToString());

    public abstract int GetLoopback();

    public abstract bool Breakable();

    public abstract bool IsContainer();

    public abstract bool IsEmpty();

    public virtual bool AllowsPreDeclare() => false;

    public virtual bool IsSplitable() => false;

    public virtual Block[] Split(int line, CloseType closeType) => throw new InvalidOperationException();

    public virtual int CompareTo(Block other)
    {
        if (begin < other.begin) return -1;
        if (begin == other.begin)
        {
            if (end < other.end) return 1;
            if (end == other.end) return priority - other.priority;
            return -1;
        }
        return 1;
    }

    public virtual Operation Process(Decompiler d)
    {
        Statement statement = this;
        return new BlockOperation(end - 1, statement);
    }

    private sealed class BlockOperation : Operation
    {
        private readonly Statement _statement;

        public BlockOperation(int line, Statement statement) : base(line)
        {
            _statement = statement;
        }

        public override IList<Statement> Process(Registers r, Block block)
        {
            return new List<Statement> { _statement };
        }
    }
}
