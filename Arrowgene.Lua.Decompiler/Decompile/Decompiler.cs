using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Expressions;
using Arrowgene.Lua.Decompiler.Decompile.Operations;
using Arrowgene.Lua.Decompiler.Decompile.Statements;
using Arrowgene.Lua.Decompiler.Decompile.Targets;
using Arrowgene.Lua.Decompiler.Parse;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Decompiler. Top-level driver that holds the
/// per-function state (registers, declarations, upvalues, child
/// decompilers) and exposes <see cref="Decompile"/>/<see cref="Print"/>
/// for clients. Lands incrementally: this commit ports the constructor,
/// declaration discovery, child-decompiler tree, and the simple
/// helpers/state types. The big <c>processLine</c> opcode switch and
/// <c>processSequence</c> walker arrive in follow-up commits.
/// </summary>
public sealed class Decompiler
{
    public readonly LFunction function;
    public readonly Code code;
    public readonly ISet<string> boundNames;
    public readonly Declaration[] declList;

    private readonly int registers;
    private readonly int length;
    private readonly Upvalues upvalues;

    private readonly Function f;
    private readonly LFunction[] functions;
    private readonly Decompiler[] decompilers;
    private readonly int @params;
    private readonly int vararg;

    public sealed class State
    {
        internal Registers r;
        internal byte[] flags;
        internal Block outer;
    }

    public enum Flag
    {
        SKIP = 1 << 0,
        LABELS = 1 << 1,
        GLOBAL = 1 << 2,
    }

    public Decompiler(LFunction function)
        : this(null, 0, function, new HashSet<string>(), null, -1)
    {
    }

    public Decompiler(Decompiler parent, int index, LFunction function, ISet<string> boundNames,
        Declaration[] parentDecls, int line)
    {
        this.f = new Function(parent != null ? parent.f : null, index, function);
        this.function = function;
        this.boundNames = boundNames;
        registers = function.maximumStackSize;
        length = function.code.Length;
        code = f.code;
        if (function.stripped || GetConfiguration().variable == Configuration.VariableMode.NODEBUG)
        {
            if (GetConfiguration().variable == Configuration.VariableMode.FINDER)
            {
                declList = VariableFinder.Process(this, function.numParams, function.maximumStackSize);
            }
            else
            {
                declList = new Declaration[function.maximumStackSize];
                int? adjust = function.header.version.outerblockscopeadjustment.Get();
                int scopeEnd = length + (adjust ?? 0);
                int i;
                for (i = 0; i < System.Math.Min(function.numParams, function.maximumStackSize); i++)
                {
                    declList[i] = new Declaration("A" + i + "_" + function.level, 0, scopeEnd);
                    declList[i].register = i;
                }

                if (GetVersion().varargtype.Get() != Version.VarArgType.ELLIPSIS && (function.vararg & 1) != 0 &&
                    i < function.maximumStackSize)
                {
                    declList[i++] = new Declaration("arg", 0, scopeEnd);
                    declList[i - 1].register = i - 1;
                }

                for (; i < function.maximumStackSize; i++)
                {
                    declList[i] = new Declaration("L" + i + "_" + function.level, 0, scopeEnd);
                    declList[i].register = i;
                }
            }
        }
        else if (function.locals.Length >= function.numParams)
        {
            declList = new Declaration[function.locals.Length];
            for (int i = 0; i < declList.Length; i++)
            {
                declList[i] = new Declaration(function.locals[i], code);
            }
        }
        else
        {
            declList = new Declaration[function.numParams];
            for (int i = 0; i < declList.Length; i++)
            {
                declList[i] = new Declaration("_ARG_" + i + "_", 0, length - 1);
            }
        }

        upvalues = new Upvalues(function, parentDecls, line);
        functions = function.functions;
        @params = function.numParams;
        vararg = function.vararg;

        int[] closureLines = new int[functions.Length];
        for (int cline = 1; cline <= code.Length(); cline++)
        {
            if (code.Op(cline) == Op.CLOSURE)
            {
                int fIdx = code.Bx(cline);
                if (closureLines[fIdx] > 0) throw new System.InvalidOperationException();
                closureLines[fIdx] = cline;
                if (function.header.version.upvaluedeclarationtype.Get() == Version.UpvalueDeclarationType.INLINE)
                {
                    LFunction func = functions[fIdx];
                    for (int i = 0; i < func.numUpvalues; i++)
                    {
                        LUpvalue upvalue = func.upvalues[i];
                        Op upvalueOp = code.Op(cline + 1 + i);
                        if (upvalueOp == Op.MOVE)
                        {
                            upvalue.instack = true;
                        }
                        else if (upvalueOp == Op.GETUPVAL)
                        {
                            upvalue.instack = false;
                        }
                        else
                        {
                            throw new System.InvalidOperationException();
                        }

                        upvalue.idx = code.B(cline + 1 + i);
                    }
                }
            }
        }

        decompilers = new Decompiler[functions.Length];
        for (int fIdx = 0; fIdx < functions.Length; fIdx++)
        {
            int closureLine = closureLines[fIdx];
            if (closureLine > 0)
            {
                HashSet<string> innerNames = new HashSet<string>(boundNames);
                foreach (Declaration decl in declList)
                {
                    if (closureLine >= decl.begin && closureLine < decl.end)
                    {
                        innerNames.Add(decl.name);
                    }
                }

                decompilers[fIdx] = new Decompiler(this, fIdx, functions[fIdx], innerNames, declList, closureLine);
            }
        }
    }

    public Configuration GetConfiguration() => function.header.config;

    public Version GetVersion() => function.header.version;

    public bool GetNoDebug()
    {
        return function.header.config.variable == Configuration.VariableMode.NODEBUG ||
               function.stripped && function.header.config.variable == Configuration.VariableMode.DEFAULT;
    }

    public State Decompile()
    {
        State state = new State();
        state.r = new Registers(registers, length, declList, f, GetNoDebug());

        Validator.Process(f, state.r);

        ControlFlowHandler.Result result = ControlFlowHandler.Process(this, state.r);
        List<Block> blocks = result.blocks;
        state.outer = blocks[0];
        state.flags = new byte[code.length + 1];
        for (int i = 1; i <= code.length; i++)
        {
            if (result.labels[i]) state.flags[i] |= (byte)Flag.LABELS;
        }

        ProcessSequence(state, blocks, 1, code.length);
        foreach (Block block in blocks)
        {
            block.Resolve(state.r);
        }

        HandleUnusedConstants(state.outer);
        return state;
    }

    public void Print(State state) => Print(state, new Output());

    public void Print(State state, OutputProvider @out) => Print(state, new Output(@out));

    public void Print(State state, Output @out)
    {
        HandleInitialDeclares(@out);
        state.outer.Print(this, @out);
    }

    private sealed class UnusedConstantCollector : Walker
    {
        private readonly HashSet<int> unusedConstants;
        private int nextConstant;

        public UnusedConstantCollector(HashSet<int> unusedConstants)
        {
            this.unusedConstants = unusedConstants;
            nextConstant = 0;
        }

        public override void VisitExpression(Expression expression)
        {
            if (expression.IsConstant())
            {
                int index = expression.GetConstantIndex();
                if (index >= 0)
                {
                    while (index > nextConstant)
                    {
                        unusedConstants.Add(nextConstant++);
                    }

                    if (index == nextConstant)
                    {
                        nextConstant++;
                    }
                }
            }
        }
    }

    private sealed class UnusedConstantApplier : Walker
    {
        private readonly HashSet<int> unusedConstants;
        private readonly Function f;
        private int nextConstant;

        public UnusedConstantApplier(HashSet<int> unusedConstants, Function f)
        {
            this.unusedConstants = unusedConstants;
            this.f = f;
            nextConstant = 0;
        }

        public override void VisitStatement(Statement statement)
        {
            if (unusedConstants.Contains(nextConstant))
            {
                if (statement.UseConstant(f, nextConstant))
                {
                    nextConstant++;
                }
            }
        }

        public override void VisitExpression(Expression expression)
        {
            if (expression.IsConstant())
            {
                int index = expression.GetConstantIndex();
                if (index >= nextConstant)
                {
                    nextConstant = index + 1;
                }
            }
        }
    }

    private void HandleUnusedConstants(Block outer)
    {
        HashSet<int> unusedConstants = new HashSet<int>(function.constants.Length);
        outer.Walk(new UnusedConstantCollector(unusedConstants));
        outer.Walk(new UnusedConstantApplier(unusedConstants, f));
    }

    private void HandleInitialDeclares(Output @out)
    {
        List<Declaration> initdecls = new List<Declaration>(declList.Length);
        int initdeclcount = @params;
        switch (GetVersion().varargtype.Get())
        {
            case Version.VarArgType.ARG:
            case Version.VarArgType.HYBRID:
            case Version.VarArgType.NAMED:
                initdeclcount += vararg & 1;
                break;
            case Version.VarArgType.ELLIPSIS:
                break;
        }

        for (int i = initdeclcount; i < declList.Length; i++)
        {
            if (declList[i].begin == 0 && !declList[i].IsInternalName())
            {
                initdecls.Add(declList[i]);
            }
        }

        if (initdecls.Count > 0)
        {
            @out.Print("local ");
            @out.Print(initdecls[0].name);
            for (int i = 1; i < initdecls.Count; i++)
            {
                @out.Print(", ");
                @out.Print(initdecls[i].name);
            }

            @out.PrintLn();
        }
    }

    private int Fb2int50(int fb) => (fb & 7) << (fb >> 3);

    private int Fb2int(int fb)
    {
        int exponent = (fb >> 3) & 0x1f;
        if (exponent == 0)
        {
            return fb;
        }
        else
        {
            return ((fb & 7) + 8) << (exponent - 1);
        }
    }

    /// <summary>
    /// Decodes values from the Lua TMS enumeration used for the MMBIN family of operations.
    /// </summary>
    private Expression.BinaryOperation DecodeBinOp(int tm)
    {
        switch (tm)
        {
            case 6: return Expression.BinaryOperation.ADD;
            case 7: return Expression.BinaryOperation.SUB;
            case 8: return Expression.BinaryOperation.MUL;
            case 9: return Expression.BinaryOperation.MOD;
            case 10: return Expression.BinaryOperation.POW;
            case 11: return Expression.BinaryOperation.DIV;
            case 12: return Expression.BinaryOperation.IDIV;
            case 13: return Expression.BinaryOperation.BAND;
            case 14: return Expression.BinaryOperation.BOR;
            case 15: return Expression.BinaryOperation.BXOR;
            case 16: return Expression.BinaryOperation.SHL;
            case 17: return Expression.BinaryOperation.SHR;
            default: throw new System.InvalidOperationException();
        }
    }

    private void Handle50BinOp(List<Operation> operations, State state, int line, Expression.BinaryOperation op)
    {
        operations.Add(new RegisterSet(line, code.A(line),
            Expression.Make(op, state.r.GetKExpression(code.B(line), line),
                state.r.GetKExpression(code.C(line), line))));
    }

    private void Handle54BinOp(List<Operation> operations, State state, int line, Expression.BinaryOperation op)
    {
        operations.Add(new RegisterSet(line, code.A(line),
            Expression.Make(op, state.r.GetExpression(code.B(line), line), state.r.GetExpression(code.C(line), line))));
    }

    private void Handle54BinKOp(List<Operation> operations, State state, int line, Expression.BinaryOperation op)
    {
        if (line + 1 > code.length || code.Op(line + 1) != Op.MMBINK) throw new System.InvalidOperationException();
        Expression left = state.r.GetExpression(code.B(line), line);
        Expression right = f.GetConstantExpression(code.C(line));
        if (code.k(line + 1))
        {
            Expression temp = left;
            left = right;
            right = temp;
        }

        operations.Add(new RegisterSet(line, code.A(line), Expression.Make(op, left, right)));
    }

    private void HandleUnaryOp(List<Operation> operations, State state, int line, Expression.UnaryOperation op)
    {
        operations.Add(new RegisterSet(line, code.A(line),
            Expression.Make(op, state.r.GetExpression(code.B(line), line))));
    }

    private void HandleSetList(List<Operation> operations, State state, int line, int stack, int count, int offset)
    {
        Expression table = state.r.GetValue(stack, line);
        for (int i = 1; i <= count; i++)
        {
            operations.Add(new TableSet(line, table, ConstantExpression.CreateInteger(offset + i),
                state.r.GetExpression(stack + i, line), false, state.r.GetUpdated(stack + i, line)));
        }
    }

    private List<Operation> ProcessLine(State state, int line)
    {
        Registers r = state.r;
        byte[] flags = state.flags;
        List<Operation> operations = new List<Operation>();
        int A = code.A(line);
        int B = code.B(line);
        int C = code.C(line);
        int Bx = code.Bx(line);
        Op currentOp = code.Op(line);
        OpT opType = currentOp != null ? currentOp.Type : OpT.DEFAULT;
        switch (opType)
        {
            case OpT.MOVE:
                operations.Add(new RegisterSet(line, A, r.GetExpression(B, line)));
                break;
            case OpT.LOADI:
                operations.Add(new RegisterSet(line, A, ConstantExpression.CreateInteger(code.sBx(line))));
                break;
            case OpT.LOADF:
                operations.Add(new RegisterSet(line, A, ConstantExpression.CreateDouble((double)code.sBx(line))));
                break;
            case OpT.LOADK:
                operations.Add(new RegisterSet(line, A, f.GetConstantExpression(Bx)));
                break;
            case OpT.LOADKX:
                if (line + 1 > code.length || code.Op(line + 1) != Op.EXTRAARG)
                    throw new System.InvalidOperationException();
                operations.Add(new RegisterSet(line, A, f.GetConstantExpression(code.Ax(line + 1))));
                break;
            case OpT.LOADBOOL:
                operations.Add(new RegisterSet(line, A, ConstantExpression.CreateBoolean(B != 0)));
                break;
            case OpT.LOADFALSE:
            case OpT.LFALSESKIP:
                operations.Add(new RegisterSet(line, A, ConstantExpression.CreateBoolean(false)));
                break;
            case OpT.LOADTRUE:
                operations.Add(new RegisterSet(line, A, ConstantExpression.CreateBoolean(true)));
                break;
            case OpT.LOADNIL:
                operations.Add(new LoadNil(line, A, B));
                break;
            case OpT.LOADNIL52:
                operations.Add(new LoadNil(line, A, A + B));
                break;
            case OpT.GETGLOBAL:
                operations.Add(new RegisterSet(line, A, f.GetGlobalExpression(Bx)));
                break;
            case OpT.SETGLOBAL:
                operations.Add(new GlobalSet(line, f.GetGlobalName(Bx), r.GetExpression(A, line)));
                break;
            case OpT.GETUPVAL:
                operations.Add(new RegisterSet(line, A, upvalues.GetExpression(B)));
                break;
            case OpT.SETUPVAL:
                operations.Add(new UpvalueSet(line, upvalues.GetName(B), r.GetExpression(A, line)));
                break;
            case OpT.GETTABUP:
                operations.Add(new RegisterSet(line, A,
                    new TableReference(r, line, upvalues.GetExpression(B), r.GetKExpression(C, line))));
                break;
            case OpT.GETTABUP54:
                operations.Add(new RegisterSet(line, A,
                    new TableReference(r, line, upvalues.GetExpression(B), f.GetConstantExpression(C))));
                break;
            case OpT.GETTABLE:
                operations.Add(new RegisterSet(line, A,
                    new TableReference(r, line, r.GetExpression(B, line), r.GetKExpression(C, line))));
                break;
            case OpT.GETTABLE54:
                operations.Add(new RegisterSet(line, A,
                    new TableReference(r, line, r.GetExpression(B, line), r.GetExpression(C, line))));
                break;
            case OpT.GETI:
                operations.Add(new RegisterSet(line, A,
                    new TableReference(r, line, r.GetExpression(B, line), ConstantExpression.CreateInteger(C))));
                break;
            case OpT.GETFIELD:
                operations.Add(new RegisterSet(line, A,
                    new TableReference(r, line, r.GetExpression(B, line), f.GetConstantExpression(C))));
                break;
            case OpT.SETTABLE:
                operations.Add(new TableSet(line, r.GetExpression(A, line), r.GetKExpression(B, line),
                    r.GetKExpression(C, line), true, line));
                break;
            case OpT.SETTABLE54:
                operations.Add(new TableSet(line, r.GetExpression(A, line), r.GetExpression(B, line),
                    r.GetKExpression54(C, code.k(line), line), true, line));
                break;
            case OpT.SETI:
                operations.Add(new TableSet(line, r.GetExpression(A, line), ConstantExpression.CreateInteger(B),
                    r.GetKExpression54(C, code.k(line), line), true, line));
                break;
            case OpT.SETFIELD:
                operations.Add(new TableSet(line, r.GetExpression(A, line), f.GetConstantExpression(B),
                    r.GetKExpression54(C, code.k(line), line), true, line));
                break;
            case OpT.SETTABUP:
                operations.Add(new TableSet(line, upvalues.GetExpression(A), r.GetKExpression(B, line),
                    r.GetKExpression(C, line), true, line));
                break;
            case OpT.SETTABUP54:
                operations.Add(new TableSet(line, upvalues.GetExpression(A), f.GetConstantExpression(B),
                    r.GetKExpression54(C, code.k(line), line), true, line));
                break;
            case OpT.NEWTABLE50:
                operations.Add(new RegisterSet(line, A, new TableLiteral(Fb2int50(B), C == 0 ? 0 : 1 << C)));
                break;
            case OpT.NEWTABLE:
                operations.Add(new RegisterSet(line, A, new TableLiteral(Fb2int(B), Fb2int(C))));
                break;
            case OpT.NEWTABLE54:
            {
                if (code.Op(line + 1) != Op.EXTRAARG) throw new System.InvalidOperationException();
                int arraySize = C;
                if (code.k(line))
                {
                    arraySize += code.Ax(line + 1) * (code.GetExtractor().C.Max() + 1);
                }

                operations.Add(new RegisterSet(line, A, new TableLiteral(arraySize, B == 0 ? 0 : (1 << (B - 1)))));
                break;
            }
            case OpT.NEWTABLE55:
            {
                if (code.Op(line + 1) != Op.EXTRAARG) throw new System.InvalidOperationException();
                int arraySize = code.vC(line);
                if (code.k(line))
                {
                    arraySize += code.Ax(line + 1) * (code.GetExtractor().vC.Max() + 1);
                }

                int vB = code.vB(line);
                operations.Add(new RegisterSet(line, A, new TableLiteral(arraySize, vB == 0 ? 0 : (1 << (vB - 1)))));
                break;
            }
            case OpT.SELF:
            {
                Expression common = r.GetExpression(B, line);
                operations.Add(new RegisterSet(line, A + 1, common));
                operations.Add(new RegisterSet(line, A,
                    new TableReference(r, line, common, r.GetKExpression(C, line))));
                break;
            }
            case OpT.SELF54:
            {
                Expression common = r.GetExpression(B, line);
                operations.Add(new RegisterSet(line, A + 1, common));
                operations.Add(new RegisterSet(line, A,
                    new TableReference(r, line, common, r.GetKExpression54(C, code.k(line), line))));
                break;
            }
            case OpT.SELF55:
            {
                Expression common = r.GetExpression(B, line);
                operations.Add(new RegisterSet(line, A + 1, common));
                operations.Add(
                    new RegisterSet(line, A, new TableReference(r, line, common, f.GetConstantExpression(C))));
                break;
            }
            case OpT.ADD:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.ADD);
                break;
            case OpT.SUB:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.SUB);
                break;
            case OpT.MUL:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.MUL);
                break;
            case OpT.DIV:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.DIV);
                break;
            case OpT.IDIV:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.IDIV);
                break;
            case OpT.MOD:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.MOD);
                break;
            case OpT.POW:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.POW);
                break;
            case OpT.BAND:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.BAND);
                break;
            case OpT.BOR:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.BOR);
                break;
            case OpT.BXOR:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.BXOR);
                break;
            case OpT.SHL:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.SHL);
                break;
            case OpT.SHR:
                Handle50BinOp(operations, state, line, Expression.BinaryOperation.SHR);
                break;
            case OpT.ADD54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.ADD);
                break;
            case OpT.SUB54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.SUB);
                break;
            case OpT.MUL54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.MUL);
                break;
            case OpT.DIV54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.DIV);
                break;
            case OpT.IDIV54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.IDIV);
                break;
            case OpT.MOD54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.MOD);
                break;
            case OpT.POW54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.POW);
                break;
            case OpT.BAND54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.BAND);
                break;
            case OpT.BOR54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.BOR);
                break;
            case OpT.BXOR54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.BXOR);
                break;
            case OpT.SHL54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.SHL);
                break;
            case OpT.SHR54:
                Handle54BinOp(operations, state, line, Expression.BinaryOperation.SHR);
                break;
            case OpT.ADDI:
            {
                if (line + 1 > code.length || code.Op(line + 1) != Op.MMBINI)
                    throw new System.InvalidOperationException();
                Expression.BinaryOperation op = DecodeBinOp(code.C(line + 1));
                int immediate = code.sC(line);
                bool swap = false;
                if (code.k(line + 1))
                {
                    if (op != Expression.BinaryOperation.ADD)
                    {
                        throw new System.InvalidOperationException();
                    }

                    swap = true;
                }
                else
                {
                    if (op == Expression.BinaryOperation.ADD)
                    {
                        // do nothing
                    }
                    else if (op == Expression.BinaryOperation.SUB)
                    {
                        immediate = -immediate;
                    }
                    else
                    {
                        throw new System.InvalidOperationException();
                    }
                }

                Expression left = r.GetExpression(B, line);
                Expression right = ConstantExpression.CreateInteger(immediate);
                if (swap)
                {
                    Expression temp = left;
                    left = right;
                    right = temp;
                }

                operations.Add(new RegisterSet(line, A, Expression.Make(op, left, right)));
                break;
            }
            case OpT.ADDK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.ADD);
                break;
            case OpT.SUBK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.SUB);
                break;
            case OpT.MULK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.MUL);
                break;
            case OpT.DIVK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.DIV);
                break;
            case OpT.IDIVK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.IDIV);
                break;
            case OpT.MODK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.MOD);
                break;
            case OpT.POWK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.POW);
                break;
            case OpT.BANDK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.BAND);
                break;
            case OpT.BORK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.BOR);
                break;
            case OpT.BXORK:
                Handle54BinKOp(operations, state, line, Expression.BinaryOperation.BXOR);
                break;
            case OpT.SHRI:
            {
                if (line + 1 > code.length || code.Op(line + 1) != Op.MMBINI)
                    throw new System.InvalidOperationException();
                int immediate = code.sC(line);
                Expression.BinaryOperation op = DecodeBinOp(code.C(line + 1));
                if (op == Expression.BinaryOperation.SHR)
                {
                    // okay
                }
                else if (op == Expression.BinaryOperation.SHL)
                {
                    immediate = -immediate;
                }
                else
                {
                    throw new System.InvalidOperationException();
                }

                operations.Add(new RegisterSet(line, A,
                    Expression.Make(op, r.GetExpression(B, line), ConstantExpression.CreateInteger(immediate))));
                break;
            }
            case OpT.SHLI:
            {
                operations.Add(new RegisterSet(line, A,
                    Expression.Make(Expression.BinaryOperation.SHL, ConstantExpression.CreateInteger(code.sC(line)),
                        r.GetExpression(B, line))));
                break;
            }
            case OpT.MMBIN:
            case OpT.MMBINI:
            case OpT.MMBINK:
                /* Do nothing ... handled with preceding operation. */
                break;
            case OpT.UNM:
                HandleUnaryOp(operations, state, line, Expression.UnaryOperation.UNM);
                break;
            case OpT.NOT:
                HandleUnaryOp(operations, state, line, Expression.UnaryOperation.NOT);
                break;
            case OpT.LEN:
                HandleUnaryOp(operations, state, line, Expression.UnaryOperation.LEN);
                break;
            case OpT.BNOT:
                HandleUnaryOp(operations, state, line, Expression.UnaryOperation.BNOT);
                break;
            case OpT.CONCAT:
            {
                Expression value = r.GetExpression(C, line);
                // Remember that CONCAT is right associative.
                while (C-- > B)
                {
                    value = Expression.Make(Expression.BinaryOperation.CONCAT, r.GetExpression(C, line), value);
                }

                operations.Add(new RegisterSet(line, A, value));
                break;
            }
            case OpT.CONCAT54:
            {
                if (B < 2) throw new System.InvalidOperationException();
                B--;
                Expression value = r.GetExpression(A + B, line);
                while (B-- > 0)
                {
                    value = Expression.Make(Expression.BinaryOperation.CONCAT, r.GetExpression(A + B, line), value);
                }

                operations.Add(new RegisterSet(line, A, value));
                break;
            }
            case OpT.JMP:
            case OpT.JMP52:
            case OpT.JMP54:
            case OpT.EQ:
            case OpT.LT:
            case OpT.LE:
            case OpT.EQ54:
            case OpT.LT54:
            case OpT.LE54:
            case OpT.EQK:
            case OpT.EQI:
            case OpT.LTI:
            case OpT.LEI:
            case OpT.GTI:
            case OpT.GEI:
            case OpT.TEST:
            case OpT.TEST54:
                /* Do nothing ... handled with branches */
                break;
            case OpT.TEST50:
            {
                if (GetNoDebug() && A != B)
                {
                    operations.Add(new RegisterSet(line, A,
                        Expression.Make(Expression.BinaryOperation.OR, r.GetExpression(B, line),
                            InitialExpression(state, A, line))));
                }

                break;
            }
            case OpT.TESTSET:
            case OpT.TESTSET54:
            {
                if (GetNoDebug())
                {
                    operations.Add(new RegisterSet(line, A,
                        Expression.Make(Expression.BinaryOperation.OR, r.GetExpression(B, line),
                            InitialExpression(state, A, line))));
                }

                break;
            }
            case OpT.CALL:
            {
                bool multiple = (C >= 3 || C == 0);
                if (B == 0) B = registers - A;
                if (C == 0) C = registers - A + 1;
                Expression function = r.GetExpression(A, line);
                Expression[] arguments = new Expression[B - 1];
                for (int register = A + 1; register <= A + B - 1; register++)
                {
                    arguments[register - A - 1] = r.GetExpression(register, line);
                }

                FunctionCall value = new FunctionCall(function, arguments, multiple);
                if (C == 1 && !(A > 0 && (!r.IsLocal(A - 1, line) || r.IsNewLocal(A - 1, line))))
                {
                    operations.Add(new CallOperation(line, value));
                }
                else
                {
                    if (C == 1) C = 2;
                    if (C == 2 && !multiple)
                    {
                        operations.Add(new RegisterSet(line, A, value));
                    }
                    else
                    {
                        operations.Add(new MultipleRegisterSet(line, A, A + C - 2, value));
                    }
                }

                break;
            }
            case OpT.TAILCALL:
            case OpT.TAILCALL54:
            {
                if (B == 0) B = registers - A;
                Expression function = r.GetExpression(A, line);
                Expression[] arguments = new Expression[B - 1];
                for (int register = A + 1; register <= A + B - 1; register++)
                {
                    arguments[register - A - 1] = r.GetExpression(register, line);
                }

                FunctionCall value = new FunctionCall(function, arguments, true);
                operations.Add(new ReturnOperation(line, value));
                flags[line + 1] |= (byte)Flag.SKIP;
                break;
            }
            case OpT.RETURN:
            case OpT.RETURN54:
            {
                if (B == 0) B = registers - A + 1;
                Expression[] values = new Expression[B - 1];
                for (int register = A; register <= A + B - 2; register++)
                {
                    values[register - A] = r.GetExpression(register, line);
                }

                operations.Add(new ReturnOperation(line, values));
                break;
            }
            case OpT.RETURN0:
                operations.Add(new ReturnOperation(line, new Expression[0]));
                break;
            case OpT.RETURN1:
                operations.Add(new ReturnOperation(line, new Expression[] { r.GetExpression(A, line) }));
                break;
            case OpT.FORLOOP:
            case OpT.FORLOOP54:
            case OpT.FORPREP:
            case OpT.FORPREP54:
            case OpT.FORPREP55:
            case OpT.TFORPREP:
            case OpT.TFORPREP54:
            case OpT.TFORPREP55:
            case OpT.TFORCALL:
            case OpT.TFORCALL54:
            case OpT.TFORLOOP:
            case OpT.TFORLOOP52:
            case OpT.TFORLOOP54:
                /* Do nothing ... handled with branches */
                break;
            case OpT.SETLIST50:
            {
                HandleSetList(operations, state, line, A, 1 + Bx % 32, Bx - Bx % 32);
                break;
            }
            case OpT.SETLISTO:
            {
                HandleSetList(operations, state, line, A, registers - A - 1, Bx - Bx % 32);
                break;
            }
            case OpT.SETLIST:
            {
                if (C == 0)
                {
                    C = code.Codepoint(line + 1);
                    flags[line + 1] |= (byte)Flag.SKIP;
                }

                if (B == 0)
                {
                    B = registers - A - 1;
                }

                HandleSetList(operations, state, line, A, B, (C - 1) * 50);
                break;
            }
            case OpT.SETLIST52:
            {
                if (C == 0)
                {
                    if (line + 1 > code.length || code.Op(line + 1) != Op.EXTRAARG)
                        throw new System.InvalidOperationException();
                    C = code.Ax(line + 1);
                    flags[line + 1] |= (byte)Flag.SKIP;
                }

                if (B == 0)
                {
                    B = registers - A - 1;
                }

                HandleSetList(operations, state, line, A, B, (C - 1) * 50);
                break;
            }
            case OpT.SETLIST54:
            {
                if (code.k(line))
                {
                    if (line + 1 > code.length || code.Op(line + 1) != Op.EXTRAARG)
                        throw new System.InvalidOperationException();
                    C += code.Ax(line + 1) * (code.GetExtractor().C.Max() + 1);
                    flags[line + 1] |= (byte)Flag.SKIP;
                }

                if (B == 0)
                {
                    B = registers - A - 1;
                }

                HandleSetList(operations, state, line, A, B, C);
                break;
            }
            case OpT.SETLIST55:
            {
                int vB = code.vB(line);
                int vC = code.vC(line);
                if (code.k(line))
                {
                    if (line + 1 > code.length || code.Op(line + 1) != Op.EXTRAARG)
                        throw new System.InvalidOperationException();
                    vC += code.Ax(line + 1) * (code.GetExtractor().vC.Max() + 1);
                    flags[line + 1] |= (byte)Flag.SKIP;
                }

                if (vB == 0)
                {
                    vB = registers - A - 1;
                }

                HandleSetList(operations, state, line, A, vB, vC);
                break;
            }
            case OpT.ERRNNIL:
                if (line + 1 <= code.length)
                {
                    flags[line + 1] |= (byte)Flag.GLOBAL;
                }

                break;
            case OpT.TBC:
                r.GetDeclaration(A, line).tbc = true;
                break;
            case OpT.CLOSE:
            case OpT.CLOSE55:
                break;
            case OpT.CLOSURE:
            {
                LFunction fn = functions[Bx];
                Decompiler innerd = decompilers[Bx];
                operations.Add(new RegisterSet(line, A, new ClosureExpression(fn, innerd, line + 1)));
                break;
            }
            case OpT.VARARGPREP:
                if (GetVersion().varargtype.Get() == Version.VarArgType.NAMED && r.registers > function.numParams)
                {
                    Declaration decl = r.GetDeclaration(function.numParams, line);
                    if (decl != null && !decl.IsInternalName())
                    {
                        decl.namedVararg = true;
                    }
                }

                break;
            case OpT.VARARG:
            {
                bool multiple = (B != 2);

                // B == 1 means no registers are set; this should only happen when the VARARG
                // appears on the right-hand side of an assignment without enough targets.
                // Should be multiple (as not adjusted "(...)"), and we need to pretend it's
                // an actual operation so we can capture it...
                // (luac allocates stack space even though it doesn't technically use it)
                if (B == 1) B = 2;

                if (B == 0) B = registers - A + 1;
                Expression value = new Vararg(B - 1, multiple);
                operations.Add(new MultipleRegisterSet(line, A, A + B - 2, value));
                break;
            }
            case OpT.VARARG54:
            {
                bool multiple = (C != 2);
                if (C == 1) C = 2; // see above
                if (C == 0) C = registers - A + 1;
                Expression value = new Vararg(C - 1, multiple);
                operations.Add(new MultipleRegisterSet(line, A, A + C - 2, value));
                break;
            }
            case OpT.GETVARG:
            {
                Expression value = new TableReference(r, line, r.GetExpression(B, line), r.GetExpression(C, line));
                operations.Add(new RegisterSet(line, A, value));
                break;
            }
            case OpT.EXTRAARG:
            case OpT.EXTRABYTE:
                /* Do nothing ... handled by previous instruction */
                break;
            case OpT.DEFAULT:
            case OpT.DEFAULT54:
                throw new System.InvalidOperationException();
        }

        return operations;
    }

    private Expression InitialExpression(State state, int register, int line)
    {
        if (line == 1)
        {
            if (register < function.numParams) throw new System.InvalidOperationException();
            return ConstantExpression.CreateNil(line);
        }
        else
        {
            return state.r.GetExpression(register, line - 1);
        }
    }

    private Assignment ProcessOperation(State state, Operation operation, int line, int nextLine, Block block)
    {
        Registers r = state.r;
        byte[] flags = state.flags;
        Assignment assign = null;
        IList<Statement> stmts = operation.Process(r, block);
        if (stmts.Count == 1)
        {
            Statement stmt = stmts[0];
            if (stmt is Assignment a)
            {
                assign = a;
                if ((flags[line] & (byte)Flag.GLOBAL) != 0)
                {
                    assign.GlobalDeclare();
                }
            }

            if (assign != null)
            {
                bool declare = false;
                foreach (Declaration newLocal in r.GetNewLocals(line, block.closeRegister))
                {
                    if (assign.GetFirstTarget().IsDeclaration(newLocal))
                    {
                        declare = true;
                        break;
                    }
                }

                while (!declare && nextLine < block.end)
                {
                    Op op = code.Op(nextLine);
                    if (IsMoveIntoTarget(r, nextLine))
                    {
                        Target target = GetMoveIntoTargetTarget(r, nextLine, line + 1);
                        Expression value = GetMoveIntoTargetValue(r, nextLine, line + 1);
                        assign.AddFirst(target, value, nextLine);
                        flags[nextLine] |= (byte)Flag.SKIP;
                        nextLine++;
                    }
                    else if (op == Op.MMBIN || op == Op.MMBINI || op == Op.MMBINK ||
                             code.IsUpvalueDeclaration(nextLine))
                    {
                        // skip
                        nextLine++;
                    }
                    else if (nextLine + 1 < block.end && code.Op(nextLine + 1) == Op.ERRNNIL)
                    {
                        // skip
                        nextLine += 2;
                    }
                    else
                    {
                        break;
                    }
                }

                if (line >= 2 && IsMoveIntoTarget(r, line))
                {
                    int lastUsed = GetMoveIntoTargetValueRegister(r, line, line);
                    int lastLoaded = GetRegister(line - 1);
                    if (!r.IsLocal(lastLoaded, line - 1))
                    {
                        while (lastUsed < lastLoaded)
                        {
                            lastUsed++;
                            assign.AddExcessValue(r.GetValue(lastUsed, line), r.GetUpdated(lastUsed, line), lastUsed);
                        }
                    }
                }
            }
        }

        foreach (Statement stmt in stmts)
        {
            block.AddStatement(stmt);
        }

        return assign;
    }

    public bool IsNameGlobal(string name)
    {
        for (int line = 1; line <= code.length; line++)
        {
            Op op = code.Op(line);
            OpT t = op != null ? op.Type : OpT.DEFAULT;
            switch (t)
            {
                case OpT.GETGLOBAL:
                case OpT.SETGLOBAL:
                    if (function.constants[code.Bx(line)].Deref().Equals(name))
                    {
                        return true;
                    }

                    break;
                case OpT.CLOSURE:
                    if (decompilers[code.Bx(line)].IsNameGlobal(name))
                    {
                        return true;
                    }

                    break;
                default:
                    break;
            }
        }

        return false;
    }

    public bool HasStatement(int begin, int end)
    {
        if (begin <= end)
        {
            State state = new State();
            state.r = new Registers(registers, length, declList, f, GetNoDebug());
            state.outer = new OuterBlock(function, code.length);
            Block scoped = new DoEndBlock(function, begin, end + 1);
            state.flags = new byte[code.length + 1];
            List<Block> blocks = new List<Block> { state.outer, scoped };
            ProcessSequence(state, blocks, 1, code.length);
            return !scoped.IsEmpty();
        }
        else
        {
            return false;
        }
    }

    private void ProcessSequence(State state, List<Block> blocks, int begin, int end)
    {
        Registers r = state.r;
        int blockContainerIndex = 0;
        int blockStatementIndex = 0;
        List<Block> blockContainers = new List<Block>(blocks.Count);
        List<Block> blockStatements = new List<Block>(blocks.Count);
        foreach (Block block in blocks)
        {
            if (block.IsContainer())
            {
                blockContainers.Add(block);
            }
            else
            {
                blockStatements.Add(block);
            }
        }

        Util.Stack<Block> blockStack = new Util.Stack<Block>();
        blockStack.Push(blockContainers[blockContainerIndex++]);

        byte[] flags = state.flags;
        bool[] labels_handled = new bool[code.length + 1];

        int line = 1;
        while (true)
        {
            int nextline = line;
            IList<Operation> operations = null;
            IList<Declaration> prevLocals = null;
            IList<Declaration> newLocals = null;

            // Handle container blocks
            if (blockStack.Peek().end <= line)
            {
                Block endingBlock = blockStack.Pop();
                Operation operation = endingBlock.Process(this);
                if (blockStack.IsEmpty()) return;
                if (operation == null) throw new System.InvalidOperationException();
                operations = new List<Operation> { operation };
                prevLocals = r.GetNewLocals(line - 1);
            }
            else
            {
                IList<Declaration> locals = r.GetNewLocals(line, blockStack.Peek().closeRegister);
                while (blockContainerIndex < blockContainers.Count &&
                       blockContainers[blockContainerIndex].begin <= line)
                {
                    Block next = blockContainers[blockContainerIndex++];
                    if (locals.Count > 0 && next.AllowsPreDeclare() &&
                        (locals[0].end > next.ScopeEnd() || locals[0].register < next.closeRegister))
                    {
                        Assignment declaration = new Assignment();
                        int declareEnd = locals[0].end;
                        declaration.Declare();
                        while (locals.Count > 0 && locals[0].end == declareEnd &&
                               (next.closeRegister == -1 || locals[0].register < next.closeRegister))
                        {
                            Declaration decl = locals[0];
                            declaration.AddLast(new VariableTarget(decl), ConstantExpression.CreateNil(line), line);
                            locals.RemoveAt(0);
                        }

                        blockStack.Peek().AddStatement(declaration);
                    }

                    if (!next.HasHeader())
                    {
                        if (!labels_handled[line] && ((flags[line] & (byte)Flag.LABELS) != 0))
                        {
                            blockStack.Peek().AddStatement(new Label(line));
                            labels_handled[line] = true;
                        }
                    }

                    blockStack.Push(next);
                }

                if (!labels_handled[line] && ((flags[line] & (byte)Flag.LABELS) != 0))
                {
                    blockStack.Peek().AddStatement(new Label(line));
                    labels_handled[line] = true;
                }
            }

            Block block = blockStack.Peek();

            r.StartLine(line);

            // Handle other sources of operations (after pushing any new container block)
            if (operations == null)
            {
                if (blockStatementIndex < blockStatements.Count && blockStatements[blockStatementIndex].begin <= line)
                {
                    Block blockStatement = blockStatements[blockStatementIndex++];
                    Operation operation = blockStatement.Process(this);
                    operations = new List<Operation> { operation };
                }
                else
                {
                    // After all blocks are handled for a line, we will reach here
                    nextline = line + 1;
                    if (!((flags[line] & (byte)Flag.SKIP) != 0) && !code.IsUpvalueDeclaration(line) && line >= begin &&
                        line <= end)
                    {
                        operations = ProcessLine(state, line);
                    }
                    else
                    {
                        operations = new List<Operation>();
                    }

                    if (line >= begin && line <= end)
                    {
                        newLocals = r.GetNewLocals(line, block.closeRegister);
                    }
                }
            }

            // Need to capture the assignment (if any) to attach local variable declarations
            Assignment assignment = null;

            foreach (Operation operation in operations)
            {
                Assignment operationAssignment = ProcessOperation(state, operation, line, nextline, block);
                if (operationAssignment != null)
                {
                    assignment = operationAssignment;
                }
            }

            // Some declarations may be swallowed by assignment blocks.
            // These are restored via prevLocals
            IList<Declaration> locals2 = newLocals;
            if (assignment != null && prevLocals != null)
            {
                locals2 = prevLocals;
            }

            if (locals2 != null && locals2.Count > 0)
            {
                int scopeEnd = -1;
                if (assignment == null)
                {
                    // Create a new Assignment to hold the declarations
                    assignment = new Assignment();
                    block.AddStatement(assignment);
                }
                else
                {
                    foreach (Declaration decl in locals2)
                    {
                        if (assignment.Assigns(decl))
                        {
                            scopeEnd = decl.end;
                            break;
                        }
                    }
                }

                bool firstProcess = !assignment.IsDeclaration();
                assignment.Declare();
                int lastAssigned = locals2[0].register;
                foreach (Declaration decl in locals2)
                {
                    if ((scopeEnd == -1 || decl.end == scopeEnd) && decl.register >= block.closeRegister)
                    {
                        assignment.AddLast(new VariableTarget(decl), r.GetValue(decl.register, line + 1),
                            r.GetUpdated(decl.register, line - 1));
                        lastAssigned = decl.register;
                    }
                }

                if (firstProcess)
                {
                    // only populate once (excess detection can fail on later lines)
                    int lastLoaded = GetRegister(line);
                    while (lastAssigned < lastLoaded)
                    {
                        lastAssigned++;
                        assignment.AddExcessValue(r.GetValue(lastAssigned, line + 1), r.GetUpdated(lastAssigned, line),
                            lastAssigned);
                    }
                }
            }

            line = nextline;
        }
    }

    private bool IsMoveIntoTarget(Registers r, int line)
    {
        if (code.IsUpvalueDeclaration(line)) return false;
        Op op = code.Op(line);
        OpT t = op != null ? op.Type : OpT.DEFAULT;
        switch (t)
        {
            case OpT.MOVE:
                return r.IsAssignable(code.A(line), line) && !r.IsLocal(code.B(line), line);
            case OpT.SETUPVAL:
            case OpT.SETGLOBAL:
                return !r.IsLocal(code.A(line), line);
            case OpT.SETTABLE:
            case OpT.SETTABUP:
            {
                int C = code.C(line);
                if (f.IsConstant(C))
                {
                    return false;
                }
                else
                {
                    return !r.IsLocal(C, line);
                }
            }
            case OpT.SETTABLE54:
            case OpT.SETI:
            case OpT.SETFIELD:
            case OpT.SETTABUP54:
            {
                if (code.k(line))
                {
                    return false;
                }
                else
                {
                    return !r.IsLocal(code.C(line), line);
                }
            }
            default:
                return false;
        }
    }

    private Target GetMoveIntoTargetTarget(Registers r, int line, int previous)
    {
        Op op = code.Op(line);
        OpT t = op != null ? op.Type : OpT.DEFAULT;
        switch (t)
        {
            case OpT.MOVE:
                return r.GetTarget(code.A(line), line);
            case OpT.SETUPVAL:
                return new UpvalueTarget(upvalues.GetName(code.B(line)));
            case OpT.SETGLOBAL:
                return new GlobalTarget(f.GetGlobalName(code.Bx(line)));
            case OpT.SETTABLE:
                return new TableTarget(r, line, r.GetExpression(code.A(line), previous),
                    r.GetKExpression(code.B(line), previous));
            case OpT.SETTABLE54:
                return new TableTarget(r, line, r.GetExpression(code.A(line), previous),
                    r.GetExpression(code.B(line), previous));
            case OpT.SETI:
                return new TableTarget(r, line, r.GetExpression(code.A(line), previous),
                    ConstantExpression.CreateInteger(code.B(line)));
            case OpT.SETFIELD:
                return new TableTarget(r, line, r.GetExpression(code.A(line), previous),
                    f.GetConstantExpression(code.B(line)));
            case OpT.SETTABUP:
            {
                int A = code.A(line);
                int B = code.B(line);
                return new TableTarget(r, line, upvalues.GetExpression(A), r.GetKExpression(B, previous));
            }
            case OpT.SETTABUP54:
            {
                int A = code.A(line);
                int B = code.B(line);
                return new TableTarget(r, line, upvalues.GetExpression(A), f.GetConstantExpression(B));
            }
            default:
                throw new System.InvalidOperationException();
        }
    }

    private Expression GetMoveIntoTargetValue(Registers r, int line, int previous)
    {
        int A = code.A(line);
        int B = code.B(line);
        int C = code.C(line);
        Op op = code.Op(line);
        OpT t = op != null ? op.Type : OpT.DEFAULT;
        switch (t)
        {
            case OpT.MOVE:
                return r.GetValue(B, previous);
            case OpT.SETUPVAL:
            case OpT.SETGLOBAL:
                return r.GetExpression(A, previous);
            case OpT.SETTABLE:
            case OpT.SETTABUP:
                if (f.IsConstant(C))
                {
                    throw new System.InvalidOperationException();
                }
                else
                {
                    return r.GetExpression(C, previous);
                }
            case OpT.SETTABLE54:
            case OpT.SETI:
            case OpT.SETFIELD:
            case OpT.SETTABUP54:
                if (code.k(line))
                {
                    throw new System.InvalidOperationException();
                }
                else
                {
                    return r.GetExpression(C, previous);
                }
            default:
                throw new System.InvalidOperationException();
        }
    }

    private int GetMoveIntoTargetValueRegister(Registers r, int line, int previous)
    {
        int A = code.A(line);
        int B = code.B(line);
        int C = code.C(line);
        Op op = code.Op(line);
        OpT t = op != null ? op.Type : OpT.DEFAULT;
        switch (t)
        {
            case OpT.MOVE:
                return B;
            case OpT.SETUPVAL:
            case OpT.SETGLOBAL:
                return A;
            case OpT.SETTABLE:
            case OpT.SETTABUP:
                if (f.IsConstant(C))
                {
                    throw new System.InvalidOperationException();
                }
                else
                {
                    return C;
                }
            case OpT.SETTABLE54:
            case OpT.SETI:
            case OpT.SETFIELD:
            case OpT.SETTABUP54:
                if (code.k(line))
                {
                    throw new System.InvalidOperationException();
                }
                else
                {
                    return C;
                }
            default:
                throw new System.InvalidOperationException();
        }
    }

    public int GetRegister(int line)
    {
        while (code.IsUpvalueDeclaration(line) || code.Op(line) == Op.EXTRAARG || code.Op(line) == Op.EXTRABYTE)
        {
            if (line == 1) return -1;
            line--;
        }

        return code.Register(line);
    }
}
