using System;
using System.Collections.Generic;
using Arrowgene.Lua.Decompiler.Decompile.Blocks;
using Arrowgene.Lua.Decompiler.Decompile.Conditions;
using Arrowgene.Lua.Decompiler.Parse;
using Util = Arrowgene.Lua.Decompiler.Util;

namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.ControlFlowHandler. The graph-partitioning
/// pass that walks a function's bytecode and identifies the
/// <see cref="Block"/> structure: loops, ifs, do-end scopes, breaks,
/// gotos, and TESTSET assignment chains. Driven by <see cref="Process"/>.
/// </summary>
/// <remarks>
/// The C# port keeps the upstream method/state-machine layout one-for-one.
/// Methods are PascalCase to match the rest of the C# port; field and
/// local naming preserves the upstream snake_case so the side-by-side
/// diff against the Java source stays legible. Lands in multiple
/// commits: the skeleton + simple opcode helpers first, then branch
/// management, branch finding, fixed-block discovery, loops, if/break
/// resolution, set blocks, pseudo-goto, and do-end blocks.
/// </remarks>
public static class ControlFlowHandler
{
    public static bool verbose = false;

    internal enum BranchType
    {
        Comparison,
        Test,
        TestSet,
        FinalSet,
        Jump,
    }

    internal sealed class Branch
    {
        public Branch previous;
        public Branch next;
        public int line;
        public int line2;
        public int target;
        public BranchType type;
        public Condition cond;
        public int targetFirst;
        public int targetSecond;
        public bool inverseValue;
        public FinalSetCondition finalset;
        public bool deadclose;

        public Branch(int line, int line2, BranchType type, Condition cond, int targetFirst, int targetSecond, FinalSetCondition finalset)
        {
            this.line = line;
            this.line2 = line2;
            this.type = type;
            this.cond = cond;
            this.targetFirst = targetFirst;
            this.targetSecond = targetSecond;
            this.inverseValue = false;
            this.target = -1;
            this.finalset = finalset;
            this.deadclose = false;
        }
    }

    internal sealed class State
    {
        public Decompiler d;
        public LFunction function;
        public Registers r;
        public Code code;
        public Branch begin_branch;
        public Branch end_branch;
        public Branch[] branches;
        public Branch[] setbranches;
        public List<List<Branch>> finalsetbranches;
        public bool[] reverse_targets;
        public int[] resolved;
        public bool[] labels;
        public List<Block> blocks;
    }

    public sealed class Result
    {
        public List<Block> blocks;
        public bool[] labels;
    }

    public static Result Process(Decompiler d, Registers r)
    {
        State state = new State();
        state.d = d;
        state.function = d.function;
        state.r = r;
        state.code = d.code;
        state.labels = new bool[d.code.length + 1];
        FindReverseTargets(state);
        FindBranches(state);
        CombineBranches(state);
        ResolveLines(state);
        InitializeBlocks(state);
        FindFixedBlocks(state);
        FindWhileLoops(state, d.declList);
        FindRepeatLoops(state);
        FindIfBreak(state, d.declList);
        FindSetBlocks(state);
        FindPseudoGotoStatements(state, d.declList);
        FindDoBlocks(state, d.declList);
        state.blocks.Sort();
        return new Result { blocks = state.blocks, labels = state.labels };
    }

    // --- Stubbed sub-passes. Subsequent commits flesh these out one at a time. ---

    private static void FindReverseTargets(State state)
    {
        Code code = state.code;
        bool[] reverse_targets = state.reverse_targets = new bool[state.code.length + 1];
        for (int line = 1; line <= code.length; line++)
        {
            if (IsJmp(state, line))
            {
                int target = code.Target(line);
                if (target <= line)
                {
                    reverse_targets[target] = true;
                }
            }
        }
    }

    // --- Branch list management ----------------------------------------------------

    private static void RawAddBranch(State state, Branch b)
    {
        if (b.type == BranchType.FinalSet)
        {
            List<Branch> list = state.finalsetbranches[b.line];
            if (list == null)
            {
                list = new List<Branch>();
                state.finalsetbranches[b.line] = list;
            }
            list.Add(b);
        }
        else if (b.type == BranchType.TestSet)
        {
            state.setbranches[b.line] = b;
        }
        else
        {
            state.branches[b.line] = b;
        }
    }

    private static void RawRemoveBranch(State state, Branch b)
    {
        if (b.type == BranchType.FinalSet)
        {
            List<Branch> list = state.finalsetbranches[b.line];
            if (list == null)
            {
                throw new InvalidOperationException();
            }
            list.Remove(b);
        }
        else if (b.type == BranchType.TestSet)
        {
            state.setbranches[b.line] = null;
        }
        else
        {
            state.branches[b.line] = null;
        }
    }

    private static void ReplaceBranch(State state, Branch branch0, Branch branch1, Branch branchn)
    {
        RemoveBranch(state, branch0);
        RawRemoveBranch(state, branch1);
        branchn.previous = branch1.previous;
        if (branchn.previous == null)
        {
            state.begin_branch = branchn;
        }
        else
        {
            branchn.previous.next = branchn;
        }
        branchn.next = branch1.next;
        if (branchn.next == null)
        {
            state.end_branch = branchn;
        }
        else
        {
            branchn.next.previous = branchn;
        }
        RawAddBranch(state, branchn);
    }

    private static void RemoveBranch(State state, Branch b)
    {
        RawRemoveBranch(state, b);
        Branch prev = b.previous;
        Branch next = b.next;
        if (prev != null)
        {
            prev.next = next;
        }
        else
        {
            state.begin_branch = next;
        }
        if (next != null)
        {
            next.previous = prev;
        }
        else
        {
            state.end_branch = prev;
        }
    }

    private static void InsertBranch(State state, Branch b)
    {
        RawAddBranch(state, b);
    }

    private static void LinkBranches(State state)
    {
        Branch previous = null;
        for (int index = 0; index < state.branches.Length; index++)
        {
            for (int array = 0; array < 3; array++)
            {
                if (array == 0)
                {
                    List<Branch> list = state.finalsetbranches[index];
                    if (list != null)
                    {
                        foreach (Branch b in list)
                        {
                            b.previous = previous;
                            if (previous != null)
                            {
                                previous.next = b;
                            }
                            else
                            {
                                state.begin_branch = b;
                            }
                            previous = b;
                        }
                    }
                }
                else
                {
                    Branch[] branches;
                    if (array == 1)
                    {
                        branches = state.setbranches;
                    }
                    else
                    {
                        branches = state.branches;
                    }
                    Branch b = branches[index];
                    if (b != null)
                    {
                        b.previous = previous;
                        if (previous != null)
                        {
                            previous.next = b;
                        }
                        else
                        {
                            state.begin_branch = b;
                        }
                        previous = b;
                    }
                }
            }
        }
        state.end_branch = previous;
    }

    // --- Branch discovery ----------------------------------------------------------

    private static int FindLoadboolblock(State state, int target)
    {
        if (target < 1)
        {
            return -1;
        }
        int loadboolblock = -1;
        Op op = state.code.Op(target);
        if (op == Op.LOADBOOL)
        {
            if (state.code.C(target) != 0)
            {
                loadboolblock = target;
            }
            else if (target - 1 >= 1 && state.code.Op(target - 1) == Op.LOADBOOL && state.code.C(target - 1) != 0)
            {
                loadboolblock = target - 1;
            }
        }
        else if (op == Op.LFALSESKIP)
        {
            loadboolblock = target;
        }
        else if (target - 1 >= 1 && op == Op.LOADTRUE && state.code.Op(target - 1) == Op.LFALSESKIP)
        {
            loadboolblock = target - 1;
        }
        return loadboolblock;
    }

    private static void HandleLoadboolblock(State state, bool[] skip, int loadboolblock, Condition c, int line, int target)
    {
        bool loadboolvalue;
        Op op = state.code.Op(target);
        if (op == Op.LOADBOOL)
        {
            loadboolvalue = state.code.B(target) != 0;
        }
        else if (op == Op.LFALSESKIP)
        {
            loadboolvalue = false;
        }
        else if (op == Op.LOADTRUE)
        {
            loadboolvalue = true;
        }
        else
        {
            throw new InvalidOperationException();
        }
        int final_line = -1;
        if (loadboolblock - 1 >= 1 && IsJmp(state, loadboolblock - 1))
        {
            int boolskip_target = state.code.Target(loadboolblock - 1);
            int boolskip_target_redirected = -1;
            if (IsJmpRaw(state, loadboolblock + 2))
            {
                boolskip_target_redirected = state.code.Target(loadboolblock + 2);
            }
            if (boolskip_target == loadboolblock + 2 || boolskip_target == boolskip_target_redirected)
            {
                skip[loadboolblock - 1] = true;
                final_line = loadboolblock - 2;
            }
        }
        bool inverse = false;
        if (loadboolvalue)
        {
            inverse = true;
            c = c.Inverse();
        }
        bool constant = IsJmp(state, line);
        Branch b;
        int begin = line + 2;

        if (constant)
        {
            begin--;
            b = new Branch(line, line, BranchType.TestSet, c, begin, loadboolblock + 2, null);
        }
        else if (line + 2 == loadboolblock)
        {
            b = new Branch(loadboolblock, loadboolblock, BranchType.FinalSet, c, begin, loadboolblock + 2, null);
        }
        else
        {
            b = new Branch(line, line, BranchType.TestSet, c, begin, loadboolblock + 2, null);
        }
        b.target = state.code.A(loadboolblock);
        b.inverseValue = inverse;
        InsertBranch(state, b);

        if (final_line != -1)
        {
            if (constant && final_line < begin)
            {
                final_line++;
            }
            FinalSetCondition finalc = new FinalSetCondition(final_line, b.target);
            Branch finalb = new Branch(final_line, final_line, BranchType.FinalSet, finalc, final_line, loadboolblock + 2, finalc);
            finalb.target = b.target;
            InsertBranch(state, finalb);
            b.finalset = finalc;
        }
    }

    private static void HandleTest(State state, bool[] skip, int line, Condition c, int target, bool invert)
    {
        Code code = state.code;
        int loadboolblock = FindLoadboolblock(state, target);
        if (loadboolblock >= 1)
        {
            if (invert) c = c.Inverse();
            HandleLoadboolblock(state, skip, loadboolblock, c, line, target);
        }
        else
        {
            int ploadboolblock = target - 2 >= 1 ? FindLoadboolblock(state, target - 2) : -1;
            if (ploadboolblock != -1 && ploadboolblock == target - 2 && code.A(target - 2) == c.Register() && !HasStatement(state, line + 2, target - 3))
            {
                HandleTestset(state, skip, line, c, target, c.Register(), invert);
            }
            else
            {
                if (invert) c = c.Inverse();
                Branch b = new Branch(line, line, BranchType.Test, c, line + 2, target, null);
                b.target = code.A(line);
                if (invert) b.inverseValue = true;
                InsertBranch(state, b);
            }
        }
        skip[line + 1] = true;
    }

    private static void HandleTestset(State state, bool[] skip, int line, Condition c, int target, int register, bool invert)
    {
        if (state.r.isNoDebug && FindLoadboolblock(state, target) == -1)
        {
            if (invert) c = c.Inverse();
            Branch b = new Branch(line, line, BranchType.Test, c, line + 2, target, null);
            b.target = state.code.A(line);
            if (invert) b.inverseValue = true;
            InsertBranch(state, b);
            skip[line + 1] = true;
            return;
        }
        Branch bb = new Branch(line, line, BranchType.TestSet, c, line + 2, target, null);
        bb.target = register;
        if (invert) bb.inverseValue = true;
        skip[line + 1] = true;
        InsertBranch(state, bb);
        int final_line = target - 1;
        int branch_line;
        int loadboolblock = FindLoadboolblock(state, target - 2);
        if (loadboolblock != -1 && state.code.A(loadboolblock) == register)
        {
            final_line = loadboolblock;
            if (loadboolblock - 2 >= 1 && IsJmp(state, loadboolblock - 1) &&
                (state.code.Target(loadboolblock - 1) == target || IsJmpRaw(state, target) && state.code.Target(loadboolblock - 1) == state.code.Target(target)))
            {
                final_line = loadboolblock - 2;
            }
            branch_line = final_line;
        }
        else
        {
            branch_line = Math.Max(final_line, line + 2);
        }
        FinalSetCondition finalc = new FinalSetCondition(final_line, register);
        Branch finalb = new Branch(branch_line, branch_line, BranchType.FinalSet, finalc, final_line, target, finalc);
        finalb.target = register;
        InsertBranch(state, finalb);
        bb.finalset = finalc;
    }

    private static void ProcessCondition(State state, bool[] skip, int line, Condition c, bool invert)
    {
        int target = state.code.Target(line + 1);
        if (invert)
        {
            c = c.Inverse();
        }
        int loadboolblock = FindLoadboolblock(state, target);
        if (loadboolblock >= 1)
        {
            HandleLoadboolblock(state, skip, loadboolblock, c, line, target);
        }
        else
        {
            Branch b = new Branch(line, line, BranchType.Comparison, c, line + 2, target, null);
            if (invert)
            {
                b.inverseValue = true;
            }
            InsertBranch(state, b);
        }
        skip[line + 1] = true;
    }

    private static void FindBranches(State state)
    {
        Code code = state.code;
        state.branches = new Branch[state.code.length + 1];
        state.setbranches = new Branch[state.code.length + 1];
        state.finalsetbranches = new List<List<Branch>>(state.code.length + 1);
        for (int i = 0; i <= state.code.length; i++) state.finalsetbranches.Add(null);
        bool[] skip = new bool[code.length + 1];
        for (int line = 1; line <= code.length; line++)
        {
            if (!skip[line])
            {
                Op currentOp = code.Op(line);
                OpT t = currentOp != null ? currentOp.Type : OpT.DEFAULT;
                switch (t)
                {
                    case OpT.EQ:
                    case OpT.LT:
                    case OpT.LE:
                    {
                        BinaryCondition.Operator op = BinaryCondition.Operator.EQ;
                        if (currentOp == Op.LT) op = BinaryCondition.Operator.LT;
                        if (currentOp == Op.LE) op = BinaryCondition.Operator.LE;
                        Operand left = new Operand(OperandType.RK, code.B(line));
                        Operand right = new Operand(OperandType.RK, code.C(line));
                        Condition c = new BinaryCondition(op, line, left, right);
                        ProcessCondition(state, skip, line, c, code.A(line) != 0);
                        break;
                    }
                    case OpT.EQ54:
                    case OpT.LT54:
                    case OpT.LE54:
                    {
                        BinaryCondition.Operator op = BinaryCondition.Operator.EQ;
                        if (currentOp == Op.LT54) op = BinaryCondition.Operator.LT;
                        if (currentOp == Op.LE54) op = BinaryCondition.Operator.LE;
                        Operand left = new Operand(OperandType.R, code.A(line));
                        Operand right = new Operand(OperandType.R, code.B(line));
                        Condition c = new BinaryCondition(op, line, left, right);
                        ProcessCondition(state, skip, line, c, code.k(line));
                        break;
                    }
                    case OpT.EQK:
                    {
                        BinaryCondition.Operator op = BinaryCondition.Operator.EQ;
                        Operand right = new Operand(OperandType.R, code.A(line));
                        Operand left = new Operand(OperandType.K, code.B(line));
                        Condition c = new BinaryCondition(op, line, left, right);
                        ProcessCondition(state, skip, line, c, code.k(line));
                        break;
                    }
                    case OpT.EQI:
                    case OpT.LTI:
                    case OpT.LEI:
                    case OpT.GTI:
                    case OpT.GEI:
                    {
                        BinaryCondition.Operator op = BinaryCondition.Operator.EQ;
                        if (currentOp == Op.LTI) op = BinaryCondition.Operator.LT;
                        if (currentOp == Op.LEI) op = BinaryCondition.Operator.LE;
                        if (currentOp == Op.GTI) op = BinaryCondition.Operator.GT;
                        if (currentOp == Op.GEI) op = BinaryCondition.Operator.GE;
                        OperandType operandType;
                        if (code.C(line) != 0)
                        {
                            operandType = OperandType.F;
                        }
                        else
                        {
                            operandType = OperandType.I;
                        }
                        Operand left = new Operand(OperandType.R, code.A(line));
                        Operand right = new Operand(operandType, code.sB(line));
                        if (op == BinaryCondition.Operator.EQ)
                        {
                            Operand temp = left;
                            left = right;
                            right = temp;
                        }
                        Condition c = new BinaryCondition(op, line, left, right);
                        ProcessCondition(state, skip, line, c, code.k(line));
                        break;
                    }
                    case OpT.TEST50:
                    {
                        Condition c = new TestCondition(line, code.B(line));
                        int target = code.Target(line + 1);
                        if (code.A(line) == code.B(line))
                        {
                            HandleTest(state, skip, line, c, target, code.C(line) != 0);
                        }
                        else
                        {
                            HandleTestset(state, skip, line, c, target, code.A(line), code.C(line) != 0);
                        }
                        break;
                    }
                    case OpT.TEST:
                    {
                        Condition c;
                        int target = code.Target(line + 1);
                        c = new TestCondition(line, code.A(line));
                        HandleTest(state, skip, line, c, target, code.C(line) != 0);
                        break;
                    }
                    case OpT.TEST54:
                    {
                        Condition c;
                        int target = code.Target(line + 1);
                        c = new TestCondition(line, code.A(line));
                        HandleTest(state, skip, line, c, target, code.k(line));
                        break;
                    }
                    case OpT.TESTSET:
                    {
                        Condition c = new TestCondition(line, code.B(line));
                        int target = code.Target(line + 1);
                        HandleTestset(state, skip, line, c, target, code.A(line), code.C(line) != 0);
                        break;
                    }
                    case OpT.TESTSET54:
                    {
                        Condition c = new TestCondition(line, code.B(line));
                        int target = code.Target(line + 1);
                        HandleTestset(state, skip, line, c, target, code.A(line), code.k(line));
                        break;
                    }
                    case OpT.JMP:
                    case OpT.JMP52:
                    case OpT.JMP54:
                    {
                        if (IsJmp(state, line))
                        {
                            int target = code.Target(line);
                            int loadboolblock = FindLoadboolblock(state, target);
                            if (loadboolblock >= 1)
                            {
                                HandleLoadboolblock(state, skip, loadboolblock, new ConstantCondition(-1, false), line, target);
                            }
                            else
                            {
                                Branch b = new Branch(line, line, BranchType.Jump, null, target, target, null);
                                InsertBranch(state, b);
                                if (line + 1 <= code.length && code.Op(line + 1) == Op.CLOSE55 && code.B(line + 1) != 0)
                                {
                                    b.deadclose = true;
                                }
                            }
                        }
                        break;
                    }
                    default:
                        break;
                }
            }
        }
        LinkBranches(state);
    }

    private static void CombineBranches(State state)
    {
        Branch b = state.end_branch;
        while (b != null)
        {
            b = CombineLeft(state, b).previous;
        }
    }

    private static bool Adjacent(State state, Branch branch0, Branch branch1)
    {
        if (branch1.finalset != null && branch0.finalset == branch1.finalset)
        {
            return true;
        }
        else if (branch0 == null || branch1 == null)
        {
            return false;
        }
        else
        {
            bool adjacent = branch0.targetFirst <= branch1.line;
            if (adjacent)
            {
                adjacent = !HasStatement(state, branch0.targetFirst, branch1.line - 1);
                adjacent = adjacent && !state.reverse_targets[branch1.line];
            }
            return adjacent;
        }
    }

    private static Branch CombineLeft(State state, Branch branch1)
    {
        if (IsConditional(branch1))
        {
            return CombineConditional(state, branch1);
        }
        else if (IsAssignment(branch1) || branch1.type == BranchType.FinalSet)
        {
            return CombineAssignment(state, branch1);
        }
        else
        {
            return branch1;
        }
    }

    private static Branch CombineConditional(State state, Branch branch1)
    {
        Branch branch0 = branch1.previous;
        Branch branchn = branch1;
        while (branch0 != null && branch0.line > branch1.line)
        {
            branch0 = branch0.previous;
        }
        while (branch0 != null && branchn == branch1 && Adjacent(state, branch0, branch1))
        {
            branchn = CombineConditionalHelper(state, branch0, branch1);
            if (branch0.targetSecond > branch1.targetFirst) break;
            branch0 = branch0.previous;
        }
        return branchn;
    }

    private static Branch CombineConditionalHelper(State state, Branch branch0, Branch branch1)
    {
        if (IsConditional(branch0) && IsConditional(branch1))
        {
            int branch0TargetSecond = branch0.targetSecond;
            if (IsJmp(state, branch1.targetFirst) && state.code.Target(branch1.targetFirst) == branch0TargetSecond)
            {
                branch0TargetSecond = branch1.targetFirst;
            }
            if (branch0TargetSecond == branch1.targetFirst)
            {
                branch0 = CombineConditional(state, branch0);
                Condition c = new OrCondition(branch0.cond.Inverse(), branch1.cond);
                Branch branchn = new Branch(branch0.line, branch1.line2, BranchType.Comparison, c, branch1.targetFirst, branch1.targetSecond, branch1.finalset);
                branchn.inverseValue = branch1.inverseValue;
                if (verbose) Console.Error.WriteLine("conditional or " + branchn.line);
                ReplaceBranch(state, branch0, branch1, branchn);
                return CombineConditional(state, branchn);
            }
            else if (branch0TargetSecond == branch1.targetSecond)
            {
                branch0 = CombineConditional(state, branch0);
                Condition c = new AndCondition(branch0.cond, branch1.cond);
                Branch branchn = new Branch(branch0.line, branch1.line2, BranchType.Comparison, c, branch1.targetFirst, branch1.targetSecond, branch1.finalset);
                branchn.inverseValue = branch1.inverseValue;
                if (verbose) Console.Error.WriteLine("conditional and " + branchn.line);
                ReplaceBranch(state, branch0, branch1, branchn);
                return CombineConditional(state, branchn);
            }
        }
        return branch1;
    }

    private static Branch CombineAssignment(State state, Branch branch1)
    {
        Branch branch0 = branch1.previous;
        Branch branchn = branch1;
        while (branch0 != null && branchn == branch1)
        {
            branchn = CombineAssignmentHelper(state, branch0, branch1);
            if (branch1.cond == branch1.finalset)
            {
                // keep searching for the first branch paired with a raw finalset
            }
            else if (branch0.cond == branch0.finalset)
            {
                // ignore duped finalset
            }
            else if (branch0.targetSecond > branch1.targetFirst)
            {
                break;
            }
            branch0 = branch0.previous;
        }
        return branchn;
    }

    private static Branch CombineAssignmentHelper(State state, Branch branch0, Branch branch1)
    {
        if (Adjacent(state, branch0, branch1))
        {
            int register = branch1.target;
            if (branch1.target == -1)
            {
                throw new InvalidOperationException();
            }
            if (IsConditional(branch0) && IsAssignment(branch1))
            {
                if (branch0.targetSecond == branch1.targetFirst)
                {
                    bool inverse = branch0.inverseValue;
                    if (verbose) Console.Error.WriteLine("bridge " + (inverse ? "or" : "and") + " " + branch1.line + " " + branch0.line);
                    branch0 = CombineConditional(state, branch0);
                    if (inverse != branch0.inverseValue) throw new InvalidOperationException();
                    Condition c;
                    if (!branch1.inverseValue)
                    {
                        c = new OrCondition(branch0.cond.Inverse(), branch1.cond);
                    }
                    else
                    {
                        c = new AndCondition(branch0.cond, branch1.cond);
                    }
                    Branch branchn = new Branch(branch0.line, branch1.line2, branch1.type, c, branch1.targetFirst, branch1.targetSecond, branch1.finalset);
                    branchn.inverseValue = branch1.inverseValue;
                    branchn.target = register;
                    ReplaceBranch(state, branch0, branch1, branchn);
                    return CombineAssignment(state, branchn);
                }
                else if (branch0.targetSecond == branch1.targetSecond)
                {
                    /* unluac comments out this branch */
                }
            }

            if (IsAssignment(branch0, register) && IsAssignment(branch1) && branch0.inverseValue == branch1.inverseValue)
            {
                if (branch0.targetSecond == branch1.targetSecond)
                {
                    Condition c;
                    if (verbose) Console.Error.WriteLine("assign " + (branch0.inverseValue ? "or" : "and") + " " + branch1.line + " " + branch0.line);
                    if (IsConditional(branch0))
                    {
                        branch0 = CombineConditional(state, branch0);
                        if (branch0.inverseValue)
                        {
                            branch0.cond = branch0.cond.Inverse();
                        }
                    }
                    else
                    {
                        bool inverse = branch0.inverseValue;
                        branch0 = CombineAssignment(state, branch0);
                        if (inverse != branch0.inverseValue) throw new InvalidOperationException();
                    }
                    if (branch0.inverseValue)
                    {
                        c = new OrCondition(branch0.cond, branch1.cond);
                    }
                    else
                    {
                        c = new AndCondition(branch0.cond, branch1.cond);
                    }
                    Branch branchn = new Branch(branch0.line, branch1.line2, branch1.type, c, branch1.targetFirst, branch1.targetSecond, branch1.finalset);
                    branchn.inverseValue = branch1.inverseValue;
                    branchn.target = register;
                    ReplaceBranch(state, branch0, branch1, branchn);
                    return CombineAssignment(state, branchn);
                }
            }
            if (IsAssignment(branch0, register) && branch1.type == BranchType.FinalSet)
            {
                if (branch0.targetSecond == branch1.targetSecond)
                {
                    Condition c;
                    if (branch0.finalset != null && branch0.finalset != branch1.finalset)
                    {
                        Branch b = branch0.next;
                        while (b != null)
                        {
                            if (b.cond == branch0.finalset)
                            {
                                RemoveBranch(state, b);
                                break;
                            }
                            b = b.next;
                        }
                    }

                    if (IsConditional(branch0))
                    {
                        branch0 = CombineConditional(state, branch0);
                        if (branch0.inverseValue)
                        {
                            branch0.cond = branch0.cond.Inverse();
                        }
                    }
                    else
                    {
                        bool inverse = branch0.inverseValue;
                        branch0 = CombineAssignment(state, branch0);
                        if (inverse != branch0.inverseValue) throw new InvalidOperationException();
                    }
                    if (verbose) Console.Error.WriteLine("final assign " + (branch0.inverseValue ? "or" : "and") + " " + branch1.line + " " + branch0.line);

                    if (branch0.inverseValue)
                    {
                        c = new OrCondition(branch0.cond, branch1.cond);
                    }
                    else
                    {
                        c = new AndCondition(branch0.cond, branch1.cond);
                    }
                    Branch branchn = new Branch(branch0.line, branch1.line2, BranchType.FinalSet, c, branch1.targetFirst, branch1.targetSecond, branch1.finalset);
                    branchn.target = register;
                    ReplaceBranch(state, branch0, branch1, branchn);
                    return CombineAssignment(state, branchn);
                }
            }
        }
        return branch1;
    }

    private static void ResolveLines(State state)
    {
        int[] resolved = new int[state.code.length + 1];
        for (int i = 0; i < resolved.Length; i++) resolved[i] = -1;
        for (int line = 1; line <= state.code.length; line++)
        {
            int r = line;
            Branch b = state.branches[line];
            while (b != null && b.type == BranchType.Jump)
            {
                if (resolved[r] >= 1)
                {
                    r = resolved[r];
                    break;
                }
                else if (resolved[r] == -2)
                {
                    r = b.targetSecond;
                    break;
                }
                else
                {
                    resolved[r] = -2;
                    r = b.targetSecond;
                    b = state.branches[r];
                }
            }
            if (r == line && state.code.Op(line) == Op.JMP52 && IsClose(state, line))
            {
                r = line + 1;
            }
            resolved[line] = r;
        }
        state.resolved = resolved;
    }

    private static void InitializeBlocks(State state)
    {
        state.blocks = new List<Block>();
    }

    private static void Unredirect(State state, int begin, int end, int line, int target)
    {
        Branch b = state.begin_branch;
        while (b != null)
        {
            if (b.line >= begin && b.line < end && b.targetSecond == target)
            {
                if (b.type == BranchType.FinalSet)
                {
                    b.targetFirst = line - 1;
                    b.targetSecond = line;
                    if (b.finalset != null)
                    {
                        b.finalset.line = line - 1;
                    }
                }
                else
                {
                    b.targetSecond = line;
                    if (b.targetFirst == target)
                    {
                        b.targetFirst = line;
                    }
                }
            }
            b = b.next;
        }
    }

    private static void UnredirectFinalsets(State state, int target, int line, int begin)
    {
        Branch b = state.begin_branch;
        while (b != null)
        {
            if (b.type == BranchType.FinalSet)
            {
                if (b.targetSecond == target && b.line < line && b.line >= begin)
                {
                    b.targetFirst = line - 1;
                    b.targetSecond = line;
                    if (b.finalset != null)
                    {
                        b.finalset.line = line - 1;
                    }
                }
            }
            b = b.next;
        }
    }

    private static void FindFixedBlocks(State state)
    {
        List<Block> blocks = state.blocks;
        Registers r = state.r;
        Code code = state.code;
        Op tforTarget = state.function.header.version.tfortarget.Get();
        Op forTarget = state.function.header.version.fortarget.Get();
        blocks.Add(new OuterBlock(state.function, state.code.length));

        bool[] loop = new bool[state.code.length + 1];

        Branch b = state.begin_branch;
        while (b != null)
        {
            if (b.type == BranchType.Jump)
            {
                int line = b.line;
                int target = b.targetFirst;
                if (code.Op(target) == tforTarget && !loop[target])
                {
                    loop[target] = true;
                    int A = code.A(target);
                    int C = code.C(target);
                    if (C == 0) throw new InvalidOperationException();
                    RemoveBranch(state, state.branches[line]);
                    if (state.branches[target + 1] != null)
                    {
                        RemoveBranch(state, state.branches[target + 1]);
                    }

                    bool forvarClose = false;
                    bool innerClose = false;
                    int close = target - 1;
                    if (close >= line + 1 && IsClose(state, close) && GetCloseValue(state, close) == A + 3)
                    {
                        forvarClose = true;
                        close--;
                    }
                    if (close >= line + 1 && IsClose(state, close) && GetCloseValue(state, close) <= A + 3 + C)
                    {
                        innerClose = true;
                    }

                    TForBlock block = TForBlock.Make51(state.function, line + 1, target + 2, A, C, forvarClose, innerClose);
                    block.HandleVariableDeclarations(r);
                    blocks.Add(block);
                }
                else if (code.Op(target) == forTarget && !loop[target])
                {
                    loop[target] = true;
                    int A = code.A(target);

                    ForBlock block = new ForBlock50(
                        state.function, line + 1, target + 1, A,
                        GetCloseType(state, target - 1), target - 1
                    );

                    block.HandleVariableDeclarations(r);

                    blocks.Add(block);
                    RemoveBranch(state, b);
                }
            }
            b = b.next;
        }

        for (int line = 1; line <= code.length; line++)
        {
            Op op = code.Op(line);
            OpT t = op != null ? op.Type : OpT.DEFAULT;
            switch (t)
            {
                case OpT.FORPREP:
                case OpT.FORPREP54:
                case OpT.FORPREP55:
                {
                    int A = code.A(line);
                    int target = code.Target(line);
                    int begin = line + 1;
                    int end = target + 1;
                    int varCount = 3;
                    if (op == Op.FORPREP55)
                    {
                        varCount = 2;
                    }

                    bool forvarPreClose = false;
                    bool forvarPostClose = false;
                    bool closeIsInScope = false;
                    int closeLine = target - 1;
                    if (closeLine >= line + 1 && IsClose(state, closeLine) && GetCloseValue(state, closeLine) == A + varCount)
                    {
                        forvarPreClose = true;
                        if (!state.r.isNoDebug)
                        {
                            int declScopeEnd = r.GetDeclaration(A + varCount, line).end;
                            if (GetCloseType(state, closeLine) == CloseType.CLOSE54)
                            {
                                if (declScopeEnd == closeLine) closeIsInScope = true;
                            }
                        }
                        closeLine--;
                    }
                    else if (end <= code.length && IsClose(state, end) && GetCloseValue(state, end) == A + varCount)
                    {
                        forvarPostClose = true;
                    }

                    ForBlock block = new ForBlock51(
                        state.function, begin, end, A, varCount,
                        GetCloseType(state, closeLine), closeLine, forvarPreClose, forvarPostClose, closeIsInScope
                    );

                    block.HandleVariableDeclarations(r);
                    blocks.Add(block);
                    break;
                }
                case OpT.TFORPREP:
                {
                    int target = code.Target(line);
                    int A = code.A(target);
                    int C = code.C(target);

                    bool innerClose = false;
                    int close = target - 1;
                    if (close >= line + 1 && IsClose(state, close) && GetCloseValue(state, close) == A + 3 + C)
                    {
                        innerClose = true;
                    }

                    TForBlock block = TForBlock.Make50(state.function, line + 1, target + 2, A, C + 1, innerClose);
                    block.HandleVariableDeclarations(r);
                    blocks.Add(block);
                    RemoveBranch(state, state.branches[target + 1]);
                    break;
                }
                case OpT.TFORPREP54:
                case OpT.TFORPREP55:
                {
                    bool v55 = (op == Op.TFORPREP55);

                    int target = code.Target(line);
                    int A = code.A(line);
                    int C = code.C(target);

                    int controlCount = 4;
                    if (v55) controlCount = 3;

                    bool forvarClose = false;
                    int close = target - 1;
                    if (close >= line + 1 && IsClose(state, close) && GetCloseValue(state, close) == A + controlCount)
                    {
                        forvarClose = true;
                        close--;
                    }

                    bool closeIsInScope = false;
                    if (v55)
                    {
                        closeIsInScope = true;
                    }
                    else if (!state.r.isNoDebug && target + 2 <= code.length && IsClose(state, target + 2))
                    {
                        // Prior to 5.4.5, scope ends on the close line; after 5.4.5, scope ends before the close line.
                        // (In 5.4, there is always a close for tfor.)
                        closeIsInScope = (r.GetDeclaration(A, target).end == target + 2);
                    }

                    TForBlock block = TForBlock.Make54(
                        state.function, line + 1, target + 2, A, C,
                        GetCloseType(state, close), close,
                        forvarClose, closeIsInScope,
                        controlCount
                    );
                    block.HandleVariableDeclarations(r);
                    blocks.Add(block);
                    break;
                }
                default:
                    break;
            }
        }
    }

    private static void FindWhileLoops(State state, Declaration[] declList)
    {
        List<Block> blocks = state.blocks;
        Branch j = state.end_branch;
        while (j != null)
        {
            if (j.type == BranchType.Jump && j.targetFirst <= j.line && !SplitsDecl(j.targetFirst, j.targetFirst, j.line + 1, declList))
            {
                int line = j.targetFirst;
                int loopback = line;
                int end = j.line + 1;
                Branch b = state.begin_branch;
                int extent = -1;
                while (b != null)
                {
                    if (IsConditional(b) && b.line >= loopback && b.line < j.line && state.resolved[b.targetSecond] == state.resolved[end] && extent <= b.line)
                    {
                        break;
                    }
                    if (b.line >= loopback)
                    {
                        extent = Math.Max(extent, b.targetSecond);
                    }
                    b = b.next;
                }
                if (b != null)
                {
                    bool reverse = state.reverse_targets[loopback];
                    state.reverse_targets[loopback] = false;
                    if (HasStatement(state, loopback, b.line - 1))
                    {
                        b = null;
                    }
                    state.reverse_targets[loopback] = reverse;
                }
                if (state.function.header.version.whileformat.Get() == Version.WhileFormat.BOTTOM_CONDITION)
                {
                    b = null; // while loop aren't this style
                }
                Block loop = null;
                if (b != null)
                {
                    b.targetSecond = end;
                    RemoveBranch(state, b);
                    loop = new WhileBlock51(
                        state.function, b.cond, b.targetFirst, b.targetSecond, loopback,
                        GetCloseType(state, end - 2), end - 2
                    );
                    Unredirect(state, loopback, end, j.line, loopback);
                }
                if (loop == null && j.line - 5 >= 1 && IsNonjumpClose(state, j.line - 3)
                    && IsJmpRaw(state, j.line - 2) && state.code.Target(j.line - 2) == end
                    && IsNonjumpClose(state, j.line - 1))
                {
                    b = j.previous;
                    while (b != null && !(IsConditional(b) && b.line2 == j.line - 5))
                    {
                        b = b.previous;
                    }
                    if (b != null)
                    {
                        Branch skip = state.branches[j.line - 2];
                        if (skip == null) throw new InvalidOperationException();
                        int closeLine = state.function.header.version.closesemantics.Get() == Version.CloseSemantics.LUA54 ? j.line - 3 : j.line - 1;
                        loop = new RepeatBlock(
                            state.function, b.cond, j.targetFirst, j.line + 1,
                            GetCloseType(state, closeLine), closeLine,
                            false, -1
                        );
                        RemoveBranch(state, b);
                        RemoveBranch(state, skip);
                    }
                }
                if (loop == null)
                {
                    bool repeat = false;
                    if (state.function.header.version.whileformat.Get() == Version.WhileFormat.BOTTOM_CONDITION)
                    {
                        repeat = true;
                        if (loopback - 1 >= 1 && state.branches[loopback - 1] != null)
                        {
                            Branch head = state.branches[loopback - 1];
                            if (head.type == BranchType.Jump && head.targetFirst == j.line)
                            {
                                RemoveBranch(state, head);
                                repeat = false;
                            }
                        }
                    }
                    bool gotoheuristic = false;
                    if (state.function.header.version.usegoto.Get())
                    {
                        Branch k = j.previous;
                        while (k != null && k.line >= loopback)
                        {
                            if ((k.type == BranchType.Jump
                                    || (k.type == BranchType.Test || k.type == BranchType.Comparison)
                                    && state.function.header.version.useifgotorewrite.Get() != Version.Maybe.NO)
                                && (k.targetFirst < loopback || k.targetSecond > end))
                            {
                                // Decompilation as loop may require a goto anyway to exit
                                gotoheuristic = true;
                            }
                            k = k.previous;
                        }
                    }
                    if (!gotoheuristic)
                    {
                        loop = new AlwaysLoop(state.function, loopback, end, GetCloseType(state, end - 2), end - 2, repeat);
                        Unredirect(state, loopback, end, j.line, loopback);
                    }
                }
                if (loop != null)
                {
                    RemoveBranch(state, j);
                    blocks.Add(loop);
                }
            }
            j = j.previous;
        }
    }

    private static void FindRepeatLoops(State state)
    {
        List<Block> blocks = state.blocks;
        Branch b = state.begin_branch;
        while (b != null)
        {
            if (IsConditional(b))
            {
                if (b.targetSecond < b.targetFirst)
                {
                    Block block = null;
                    if (state.function.header.version.whileformat.Get() == Version.WhileFormat.BOTTOM_CONDITION)
                    {
                        int head = b.targetSecond - 1;
                        if (head >= 1 && state.branches[head] != null && state.branches[head].type == BranchType.Jump)
                        {
                            Branch headb = state.branches[head];
                            if (headb.targetSecond <= b.line)
                            {
                                if (HasStatement(state, headb.targetSecond, b.line - 1))
                                {
                                    headb = null;
                                }
                                if (headb != null)
                                {
                                    block = new WhileBlock50(
                                        state.function, b.cond.Inverse(), head + 1, b.targetFirst, headb.targetFirst,
                                        GetCloseType(state, headb.targetFirst - 1), headb.targetFirst - 1
                                    );
                                    RemoveBranch(state, headb);
                                    Unredirect(state, 1, headb.line, headb.line, headb.targetSecond);
                                }
                            }
                        }
                    }
                    if (block == null)
                    {
                        if (state.function.header.version.extendedrepeatscope.Get())
                        {
                            int statementLine = b.line - 1;
                            while (statementLine >= 1 && !IsStatement(state, statementLine))
                            {
                                statementLine--;
                            }
                            block = new RepeatBlock(
                                state.function, b.cond, b.targetSecond, b.targetFirst,
                                GetCloseType(state, statementLine), statementLine,
                                true, statementLine
                            );
                        }
                        else if (state.function.header.version.closesemantics.Get() == Version.CloseSemantics.JUMP && IsClose(state, b.targetFirst))
                        {
                            block = new RepeatBlock(
                                state.function, b.cond, b.targetSecond, b.targetFirst + 1,
                                GetCloseType(state, b.targetFirst), b.targetFirst,
                                false, -1
                            );
                        }
                        else
                        {
                            block = new RepeatBlock(
                                state.function, b.cond, b.targetSecond, b.targetFirst,
                                CloseType.NONE, -1,
                                false, -1
                            );
                        }
                    }
                    RemoveBranch(state, b);
                    blocks.Add(block);
                }
            }
            b = b.next;
        }
    }

    private static bool SplitsDecl(int line, int begin, int end, Declaration[] declList)
    {
        foreach (Declaration decl in declList)
        {
            if (decl.IsSplitBy(line, begin, end))
            {
                return true;
            }
        }
        return false;
    }

    private static int StackReach(State state, Util.Stack<Branch> stack)
    {
        for (int i = 0; i < stack.Size(); i++)
        {
            Branch b = stack.Peek(i);
            Block breakable = EnclosingBreakableBlock(state, b.line);
            if (breakable != null && breakable.end == b.targetSecond)
            {
                // next
            }
            else
            {
                return b.targetSecond;
            }
        }
        return int.MaxValue;
    }

    private static Block ResolveIfStack(State state, Util.Stack<Branch> stack, int line)
    {
        Block block = null;
        if (!stack.IsEmpty() && StackReach(state, stack) <= line)
        {
            Branch top = stack.Pop();
            int literalEnd = state.code.Target(top.targetFirst - 1);
            if (state.function.header.version.useifgotorewrite.Get() != Version.Maybe.NO && top.targetFirst + 1 == top.targetSecond && IsJmp(state, top.targetFirst))
            {
                bool isbreakrewrite = state.function.header.version.useifbreakrewrite.Get() && IsBreakJmp(state, top.targetFirst);
                bool isgotorewrite = !isbreakrewrite && state.function.header.version.useifgotorewrite.Get() == Version.Maybe.YES;
                if (isbreakrewrite || isgotorewrite)
                {
                    // If this were actually an if statement, it would have been rewritten. It hasn't been, so it isn't...
                    block = new IfThenEndBlock(state.function, state.r, top.cond.Inverse(), top.targetFirst - 1, top.targetFirst - 1);
                    block.AddStatement(new Goto(state.function, top.targetFirst - 1, top.targetSecond));
                    state.labels[top.targetSecond] = true;
                }
            }
            if (block == null)
            {
                block = new IfThenEndBlock(
                    state.function, state.r, top.cond, top.targetFirst, top.targetSecond,
                    GetCloseType(state, top.targetSecond - 1), top.targetSecond - 1,
                    literalEnd != top.targetSecond
                );
            }
            state.blocks.Add(block);
            RemoveBranch(state, top);
        }
        return block;
    }

    private static void ResolveElse(State state, Util.Stack<Branch> stack, Util.Stack<Branch> hanging, Util.Stack<ElseEndBlock> elseStack, Branch top, Branch b, int tailTargetSecond)
    {
        while (!elseStack.IsEmpty() && elseStack.Peek().end == tailTargetSecond && elseStack.Peek().begin >= top.targetFirst)
        {
            elseStack.Pop().end = b.line;
        }

        Util.Stack<Branch> replace = new Util.Stack<Branch>();
        while (!hanging.IsEmpty() && hanging.Peek().targetSecond == tailTargetSecond && hanging.Peek().line > top.line)
        {
            Branch hanger = hanging.Pop();
            hanger.targetSecond = b.line;
            Block breakable = EnclosingBreakableBlock(state, hanger.line);
            if (breakable != null && hanger.targetSecond >= breakable.end)
            {
                replace.Push(hanger);
            }
            else
            {
                stack.Push(hanger);
                Block if_block = ResolveIfStack(state, stack, b.line);
                if (if_block == null) throw new InvalidOperationException();
            }
        }
        while (!replace.IsEmpty())
        {
            hanging.Push(replace.Pop());
        }

        UnredirectFinalsets(state, tailTargetSecond, b.line, top.targetFirst);

        Util.Stack<Branch> restore = new Util.Stack<Branch>();
        while (!stack.IsEmpty() && stack.Peek().line > top.line && stack.Peek().targetSecond == b.targetSecond)
        {
            stack.Peek().targetSecond = b.line;
            restore.Push(stack.Pop());
        }
        while (!restore.IsEmpty())
        {
            stack.Push(restore.Pop());
        }

        b.targetSecond = tailTargetSecond;
        state.blocks.Add(new IfThenElseBlock(
            state.function, top.cond, top.targetFirst, top.targetSecond, b.targetSecond,
            GetCloseType(state, top.targetSecond - 2), top.targetSecond - 2
        ));
        ElseEndBlock elseBlock = new ElseEndBlock(
            state.function, top.targetSecond, b.targetSecond,
            GetCloseType(state, b.targetSecond - 1), b.targetSecond - 1
        );
        state.blocks.Add(elseBlock);
        elseStack.Push(elseBlock);
        RemoveBranch(state, b);
    }

    private static bool IsHangerResolvable(State state, Declaration[] declList, Branch hanging, Branch resolver)
    {
        if (
            hanging.targetSecond == resolver.targetFirst
            && EnclosingBlock(state, hanging.line) == EnclosingBlock(state, resolver.line)
            && !SplitsDecl(hanging.line, hanging.targetFirst, resolver.line, declList)
            && !(
                state.function.header.version.useifbreakrewrite.Get()
                && hanging.targetFirst == resolver.line - 1
                && IsBreakJmp(state, resolver.line - 1)
            )
            && !(
                state.function.header.version.useifgotorewrite.Get() == Version.Maybe.YES
                && hanging.targetFirst == resolver.line - 1
                && IsJmp(state, resolver.line - 1)
            ))
        {
            return true;
        }
        return false;
    }

    private static bool IsHangerResolvable(State state, Declaration[] declList, Branch hanging, Util.Stack<Branch> resolvers)
    {
        for (int i = 0; i < resolvers.Size(); i++)
        {
            if (IsHangerResolvable(state, declList, hanging, resolvers.Peek(i)))
            {
                return true;
            }
        }
        return false;
    }

    private static void ResolveHanger(State state, Declaration[] declList, Util.Stack<Branch> stack, Branch hanger, Branch b)
    {
        hanger.targetSecond = b.line;
        stack.Push(hanger);
        Block if_block = ResolveIfStack(state, stack, b.line);
        if (if_block == null) throw new InvalidOperationException();
    }

    private static void ResolveHangers(State state, Declaration[] declList, Util.Stack<Branch> stack, Util.Stack<Branch> hanging, Branch b)
    {
        while (!hanging.IsEmpty() && IsHangerResolvable(state, declList, hanging.Peek(), b))
        {
            ResolveHanger(state, declList, stack, hanging.Pop(), b);
        }
    }

    private static void FindIfBreak(State state, Declaration[] declList)
    {
        Util.Stack<Branch> stack = new Util.Stack<Branch>();
        Util.Stack<Branch> hanging = new Util.Stack<Branch>();
        Util.Stack<ElseEndBlock> elseStack = new Util.Stack<ElseEndBlock>();
        Branch b = state.begin_branch;
        Util.Stack<Branch> hangingResolver = new Util.Stack<Branch>();

        while (b != null)
        {
            while (ResolveIfStack(state, stack, b.line2) != null) { }

            while (!elseStack.IsEmpty() && elseStack.Peek().end <= b.line)
            {
                elseStack.Pop();
            }

            while (!hangingResolver.IsEmpty() && !EnclosingBlock(state, hangingResolver.Peek().line).Contains(b.line))
            {
                ResolveHangers(state, declList, stack, hanging, hangingResolver.Pop());
            }

            if (IsConditional(b))
            {
                Block unprotected = EnclosingUnprotectedBlock(state, b.line);
                if (b.targetFirst > b.targetSecond) throw new InvalidOperationException();
                if (unprotected != null && !unprotected.Contains(b.targetSecond))
                {
                    if (b.targetSecond == unprotected.GetUnprotectedTarget())
                    {
                        b.targetSecond = unprotected.GetUnprotectedLine();
                    }
                }

                Block breakable = EnclosingBreakableBlock(state, b.line);
                if (!stack.IsEmpty() && stack.Peek().targetSecond < b.targetSecond
                    || breakable != null && !breakable.Contains(b.targetSecond))
                {
                    hanging.Push(b);
                }
                else
                {
                    stack.Push(b);
                }
            }
            else if (b.type == BranchType.Jump)
            {
                int line = b.line;

                Block enclosing = EnclosingBlock(state, b.line);

                int tailTargetSecond = b.targetSecond;
                Block unprotected = EnclosingUnprotectedBlock(state, b.line);
                if (unprotected != null && !unprotected.Contains(b.targetSecond))
                {
                    if (tailTargetSecond == state.resolved[unprotected.GetUnprotectedTarget()])
                    {
                        tailTargetSecond = unprotected.GetUnprotectedLine();
                    }
                }

                bool handled = false;
                bool isbreakgotocandidate = true;
                bool iselsecandidate = true;
                if (state.function.header.version.usedeadclose.Get())
                {
                    if (b.deadclose)
                    {
                        iselsecandidate = false;
                    }
                    else if (b.line - 1 >= 1 && IsClose(state, b.line - 1))
                    {
                        /* do nothing */
                    }
                    else
                    {
                        isbreakgotocandidate = false;
                    }
                }

                Block breakable = EnclosingBreakableBlock(state, line);
                if (isbreakgotocandidate && breakable != null && (b.targetFirst == breakable.end || b.targetFirst == state.resolved[breakable.end]))
                {
                    Break block = new Break(state.function, b.line, b.targetFirst);
                    if (!hanging.IsEmpty() && hanging.Peek().targetSecond == b.targetFirst
                        && EnclosingBlock(state, hanging.Peek().line) == enclosing
                        && (stack.IsEmpty()
                            || stack.Peek().line < hanging.Peek().line
                            || hanging.Peek().line > stack.Peek().line))
                    {
                        hangingResolver.Push(b);
                    }
                    UnredirectFinalsets(state, b.targetFirst, line, breakable.begin);
                    state.blocks.Add(block);
                    RemoveBranch(state, b);
                    handled = true;
                }

                if (!handled && isbreakgotocandidate && state.function.header.version.usegoto.Get() && breakable != null && !breakable.Contains(b.targetFirst) && state.resolved[b.targetFirst] != state.resolved[breakable.end])
                {
                    Goto block = new Goto(state.function, b.line, b.targetFirst);
                    if (!hanging.IsEmpty() && hanging.Peek().targetSecond == b.targetFirst
                        && EnclosingBlock(state, hanging.Peek().line) == enclosing
                        && (stack.IsEmpty() || hanging.Peek().line > stack.Peek().line))
                    {
                        hangingResolver.Push(b);
                    }
                    UnredirectFinalsets(state, b.targetFirst, line, 1);
                    state.blocks.Add(block);
                    state.labels[b.targetFirst] = true;
                    RemoveBranch(state, b);
                    handled = true;
                }

                if (!handled && iselsecandidate && !stack.IsEmpty() && stack.Peek().targetSecond - 1 == b.line && enclosing.Contains(b.line, b.targetSecond) && b.targetSecond > b.line)
                {
                    Branch top = stack.Peek();
                    while (top != null && top.targetSecond - 1 == b.line && SplitsDecl(top.line, top.targetFirst, top.targetSecond, declList))
                    {
                        Block if_block = ResolveIfStack(state, stack, top.targetSecond);
                        if (if_block == null) throw new InvalidOperationException();
                        top = stack.IsEmpty() ? null : stack.Peek();
                    }
                    if (top != null && top.targetSecond - 1 == b.line)
                    {
                        if (top.targetSecond != b.targetSecond)
                        {
                            // resolve intervening hangers
                            while (!hangingResolver.IsEmpty() && !hanging.IsEmpty() && IsHangerResolvable(state, declList, hanging.Peek(), hangingResolver.Peek()))
                            {
                                ResolveHanger(state, declList, stack, hanging.Pop(), hangingResolver.Peek());
                            }

                            ResolveElse(state, stack, hanging, elseStack, top, b, tailTargetSecond);
                            stack.Pop();
                        }
                        else if (!SplitsDecl(top.line, top.targetFirst, top.targetSecond - 1, declList))
                        {
                            // "empty else" case
                            b.targetSecond = tailTargetSecond;
                            state.blocks.Add(new IfThenElseBlock(
                                state.function, top.cond, top.targetFirst, top.targetSecond, b.targetSecond,
                                GetCloseType(state, top.targetSecond - 2), top.targetSecond - 2
                            ));
                            RemoveBranch(state, b);
                            stack.Pop();
                        }
                    }
                    handled = true; // TODO: should this always count as handled?
                }

                if (
                    !handled
                    && iselsecandidate
                    && breakable != null
                    && line + 1 < state.branches.Length && state.branches[line + 1] != null
                    && state.branches[line + 1].type == BranchType.Jump)
                {
                    for (int i = 0; i < hanging.Size(); i++)
                    {
                        Branch hanger = hanging.Peek(i);
                        if (
                            (
                                state.resolved[hanger.targetSecond] == state.resolved[breakable.end]
                                || state.function.header.version.usegoto.Get()
                                    && !breakable.Contains(hanger.targetSecond)
                            )
                            && line + 1 < state.branches.Length && state.branches[line + 1] != null
                            && state.branches[line + 1].targetFirst == hanger.targetSecond
                            && !SplitsDecl(hanger.line, hanger.targetFirst, b.line, declList) // if else
                            && !SplitsDecl(b.line, b.line + 1, b.line + 2, declList) // else break/goto
                            && !SplitsDecl(hanger.line, hanger.targetFirst, b.line + 2, declList) // full
                        )
                        {
                            // resolve intervening hangers
                            for (int j = i; j > 0; j--)
                            {
                                while (!IsHangerResolvable(state, declList, hanging.Peek(), hangingResolver.Peek()))
                                {
                                    hangingResolver.Pop();
                                }
                                ResolveHanger(state, declList, stack, hanging.Pop(), hangingResolver.Peek());
                            }

                            // else break or else goto
                            Branch top = hanging.Pop();
                            if (!hangingResolver.IsEmpty() && hangingResolver.Peek().targetFirst == top.targetSecond)
                            {
                                hangingResolver.Pop();
                            }
                            top.targetSecond = line + 1;
                            ResolveElse(state, stack, hanging, elseStack, top, b, tailTargetSecond);
                            handled = true;
                            break;
                        }
                        else if (!IsHangerResolvable(state, declList, hanger, hangingResolver))
                        {
                            break;
                        }
                    }
                }

                if (
                    !handled
                    && iselsecandidate
                    && line - 1 >= 1)
                {
                    Block splittable = EnclosingBlock(state, line - 1);
                    if (
                        splittable != null && !splittable.Breakable() && splittable.IsSplitable()
                        && state.resolved[b.targetFirst] == splittable.end + 1)
                    {
                        // split if else
                        Block[] split = splittable.Split(b.line - 1, GetCloseType(state, b.line - 2));
                        foreach (Block block in split)
                        {
                            state.blocks.Add(block);
                        }
                        RemoveBranch(state, b);
                        handled = true;
                    }
                }

                if (
                    !handled
                    && iselsecandidate
                    && breakable != null && breakable.IsSplitable()
                    && state.resolved[b.targetFirst] == breakable.GetUnprotectedTarget()
                    && line + 1 < state.branches.Length && state.branches[line + 1] != null
                    && state.branches[line + 1].type == BranchType.Jump
                    && state.resolved[state.branches[line + 1].targetFirst] == state.resolved[breakable.end])
                {
                    // split while condition (else break)
                    Block[] split = breakable.Split(b.line, GetCloseType(state, b.line - 1));
                    foreach (Block block in split)
                    {
                        state.blocks.Add(block);
                    }
                    RemoveBranch(state, b);
                    handled = true;
                }

                if (
                    !handled
                    && iselsecandidate
                    && !stack.IsEmpty() && stack.Peek().targetSecond == b.targetFirst
                    && line + 1 < state.branches.Length && state.branches[line + 1] != null
                    && state.branches[line + 1].type == BranchType.Jump
                    && state.branches[line + 1].targetFirst == b.targetFirst)
                {
                    // empty else (redirected)
                    Branch top = stack.Peek();
                    if (!SplitsDecl(top.line, top.targetFirst, b.line, declList))
                    {
                        top.targetSecond = line + 1;
                        b.targetSecond = line + 1;
                        state.blocks.Add(new IfThenElseBlock(
                            state.function, top.cond, top.targetFirst, top.targetSecond, b.targetSecond,
                            GetCloseType(state, line - 1), line - 1
                        ));
                        RemoveBranch(state, b);
                        stack.Pop();
                    }
                    handled = true; // TODO:
                }

                if (
                    !handled
                    && iselsecandidate
                    && !hanging.IsEmpty() && hanging.Peek().targetSecond == b.targetFirst
                    && line + 1 < state.branches.Length && state.branches[line + 1] != null
                    && state.branches[line + 1].type == BranchType.Jump
                    && state.branches[line + 1].targetFirst == b.targetFirst)
                {
                    // empty else (redirected)
                    Branch top = hanging.Peek();
                    if (!SplitsDecl(top.line, top.targetFirst, b.line, declList))
                    {
                        if (!hangingResolver.IsEmpty() && hangingResolver.Peek().targetFirst == top.targetSecond)
                        {
                            hangingResolver.Pop();
                        }
                        top.targetSecond = line + 1;
                        b.targetSecond = line + 1;
                        state.blocks.Add(new IfThenElseBlock(
                            state.function, top.cond, top.targetFirst, top.targetSecond, b.targetSecond,
                            GetCloseType(state, line - 1), line - 1
                        ));
                        RemoveBranch(state, b);
                        hanging.Pop();
                    }
                    handled = true; // TODO:
                }

                if (!handled && isbreakgotocandidate && (state.function.header.version.usegoto.Get() || state.r.isNoDebug))
                {
                    Goto block = new Goto(state.function, b.line, b.targetFirst);
                    if (!hanging.IsEmpty() && hanging.Peek().targetSecond == b.targetFirst && EnclosingBlock(state, hanging.Peek().line) == enclosing)
                    {
                        hangingResolver.Push(b);
                    }
                    state.blocks.Add(block);
                    state.labels[b.targetFirst] = true;
                    RemoveBranch(state, b);
                    handled = true;
                }
            }
            b = b.next;
        }
        while (!hangingResolver.IsEmpty())
        {
            ResolveHangers(state, declList, stack, hanging, hangingResolver.Pop());
        }
        while (!hanging.IsEmpty())
        {
            // if break (or if goto)
            Branch top = hanging.Pop();
            Block breakable = EnclosingBreakableBlock(state, top.line);
            if (breakable != null && breakable.end == top.targetSecond)
            {
                if (state.function.header.version.useifbreakrewrite.Get() || state.r.isNoDebug)
                {
                    Block block = new IfThenEndBlock(state.function, state.r, top.cond.Inverse(), top.targetFirst - 1, top.targetFirst - 1);
                    block.AddStatement(new Break(state.function, top.targetFirst - 1, top.targetSecond));
                    state.blocks.Add(block);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else if (state.function.header.version.usegoto.Get() || state.r.isNoDebug)
            {
                if (state.function.header.version.useifgotorewrite.Get() != Version.Maybe.NO || state.r.isNoDebug)
                {
                    Block block = new IfThenEndBlock(state.function, state.r, top.cond.Inverse(), top.targetFirst - 1, top.targetFirst - 1);
                    block.AddStatement(new Goto(state.function, top.targetFirst - 1, top.targetSecond));
                    state.blocks.Add(block);
                    state.labels[top.targetSecond] = true;
                }
                else
                {
                    // No version supports goto without if break rewrite
                    throw new InvalidOperationException();
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
            RemoveBranch(state, top);
        }
        while (ResolveIfStack(state, stack, int.MaxValue) != null) { }
    }

    private static void FindSetBlocks(State state)
    {
        List<Block> blocks = state.blocks;
        Branch b = state.begin_branch;
        while (b != null)
        {
            if (IsAssignment(b) || b.type == BranchType.FinalSet)
            {
                if (b.finalset != null)
                {
                    FinalSetCondition c = b.finalset;
                    Op op = state.code.Op(c.line);
                    if (c.line >= 2 && (op == Op.MMBIN || op == Op.MMBINI || op == Op.MMBINK || op == Op.EXTRAARG))
                    {
                        c.line--;
                        if (b.targetFirst == c.line + 1)
                        {
                            b.targetFirst = c.line;
                        }
                    }
                    while (state.code.IsUpvalueDeclaration(c.line))
                    {
                        c.line--;
                        if (b.targetFirst == c.line + 1)
                        {
                            b.targetFirst = c.line;
                        }
                    }

                    if (IsJmpRaw(state, c.line))
                    {
                        c.type = FinalSetCondition.Type.REGISTER;
                    }
                    else
                    {
                        c.type = FinalSetCondition.Type.VALUE;
                    }
                }
                if (b.cond == b.finalset)
                {
                    RemoveBranch(state, b);
                }
                else
                {
                    Block block = new SetBlock(state.function, b.cond, b.target, b.line, b.targetFirst, b.targetSecond, state.r);
                    blocks.Add(block);
                    RemoveBranch(state, b);
                }
            }
            b = b.next;
        }
    }

    private static void FindPseudoGotoStatements(State state, Declaration[] declList)
    {
        Branch b = state.begin_branch;
        while (b != null)
        {
            if (b.type == BranchType.Jump && b.targetFirst > b.line)
            {
                int end = b.targetFirst;
                Block smallestEnclosing = null;
                foreach (Block block in state.blocks)
                {
                    if (block.Contains(b.line) && block.Contains(end - 1))
                    {
                        if (smallestEnclosing == null || smallestEnclosing.Contains(block))
                        {
                            smallestEnclosing = block;
                        }
                    }
                }
                if (smallestEnclosing != null)
                {
                    // Should always find the outer block at least...
                    Block wrapping = null;
                    foreach (Block block in state.blocks)
                    {
                        if (block != smallestEnclosing && smallestEnclosing.Contains(block) && block.Contains(b.line))
                        {
                            if (wrapping == null || block.Contains(wrapping))
                            {
                                wrapping = block;
                            }
                        }
                    }
                    int begin = smallestEnclosing.begin;
                    if (wrapping != null)
                    {
                        begin = Math.Max(wrapping.begin - 1, smallestEnclosing.begin);
                    }
                    int lowerBound = int.MinValue;
                    int upperBound = int.MaxValue;
                    const int scopeAdjust = -1;
                    foreach (Declaration decl in declList)
                    {
                        if (decl.end >= begin && decl.end <= end + scopeAdjust)
                        {
                            if (decl.begin < begin)
                            {
                                upperBound = Math.Min(decl.begin, upperBound);
                            }
                        }
                        if (decl.begin >= begin && decl.begin <= end + scopeAdjust && decl.end > end + scopeAdjust)
                        {
                            lowerBound = Math.Max(decl.begin + 1, lowerBound);
                            begin = decl.begin + 1;
                        }
                    }
                    if (lowerBound > upperBound)
                    {
                        throw new InvalidOperationException();
                    }
                    begin = Math.Max(lowerBound, begin);
                    begin = Math.Min(upperBound, begin);
                    Block breakable = EnclosingBreakableBlock(state, b.line);
                    if (breakable != null)
                    {
                        begin = Math.Max(breakable.begin, begin);
                    }
                    bool containsBreak = false;
                    OnceLoop loop = new OnceLoop(state.function, begin, end);
                    foreach (Block block in state.blocks)
                    {
                        if (loop.Contains(block) && block is Break)
                        {
                            containsBreak = true;
                            break;
                        }
                    }
                    if (containsBreak)
                    {
                        // TODO: close type
                        state.blocks.Add(new IfThenElseBlock(state.function, FixedCondition.TRUE, begin, b.line + 1, end, CloseType.NONE, -1));
                        state.blocks.Add(new ElseEndBlock(state.function, b.line + 1, end, CloseType.NONE, -1));
                        RemoveBranch(state, b);
                    }
                    else if (!state.function.header.version.usegoto.Get())
                    {
                        state.blocks.Add(loop);
                        Branch b2 = b;
                        while (b2 != null)
                        {
                            if (b2.type == BranchType.Jump && b2.targetFirst > b2.line && b2.targetFirst == b.targetFirst)
                            {
                                Break breakStatement = new Break(state.function, b2.line, b2.targetFirst);
                                state.blocks.Add(breakStatement);
                                breakStatement.comment = "pseudo-goto";
                                RemoveBranch(state, b2);
                                if (b.next == b2)
                                {
                                    b = b2;
                                }
                            }
                            b2 = b2.next;
                        }
                    }
                }
            }
            b = b.next;
        }
    }

    private static void FindDoBlocks(State state, Declaration[] declList)
    {
        List<Block> newBlocks = new List<Block>();
        foreach (Block block in state.blocks)
        {
            if (block.HasCloseLine() && block.GetCloseLine() >= 1)
            {
                int closeLine = block.GetCloseLine();
                Block enclosing = EnclosingBlock(state, closeLine);
                if ((enclosing == block || enclosing.Contains(block)) && IsClose(state, closeLine))
                {
                    int register = GetCloseValue(state, closeLine);
                    bool close = true;
                    Declaration closeDecl = null;
                    foreach (Declaration decl in declList)
                    {
                        if (!decl.forLoop && !decl.forLoopExplicit && block.Contains(decl.begin))
                        {
                            if (decl.register < register)
                            {
                                close = false;
                            }
                            else if (decl.register == register)
                            {
                                closeDecl = decl;
                            }
                        }
                    }
                    if (close)
                    {
                        block.UseClose();
                    }
                    else if (closeDecl != null)
                    {
                        Block inner = new DoEndBlock(state.function, closeDecl.begin, closeDecl.end + 1);
                        inner.closeRegister = register;
                        newBlocks.Add(inner);
                        StrictScopeCheck(state);
                    }
                }
            }
        }
        state.blocks.AddRange(newBlocks);

        foreach (Declaration decl in declList)
        {
            int begin = decl.begin;
            if (!decl.forLoop && !decl.forLoopExplicit)
            {
                bool needsDoEnd = true;
                foreach (Block block in state.blocks)
                {
                    if (block.Contains(decl.begin))
                    {
                        int scopeEnd = block.ScopeEnd();
                        if (block.HasCloseLine())
                        {
                            int closeLine = block.GetCloseLine();
                            int closeRegister = GetCloseValue(state, closeLine);
                            if (closeRegister <= decl.register)
                            {
                                CloseType closeType = block.GetCloseType();
                                if (closeType == CloseType.CLOSE)
                                {
                                    scopeEnd = closeLine - 1;
                                }
                                else if (closeType == CloseType.CLOSE54)
                                {
                                    scopeEnd = closeLine - 1;
                                    if (decl.end == closeLine)
                                    {
                                        // Prior to 5.4.5, scope ends on the close line
                                        // After 5.4.5, scope ends before the close line
                                        // See: https://www.lua.org/bugs.html#5.4.4-6
                                        scopeEnd = closeLine;
                                    }
                                }
                            }
                        }
                        if (scopeEnd == decl.end)
                        {
                            block.UseScope();
                            needsDoEnd = false;
                            break;
                        }
                        else if (block.ScopeEnd() < decl.end)
                        {
                            begin = Math.Min(begin, block.begin);
                        }
                    }
                }
                if (needsDoEnd)
                {
                    // Without accounting for the order of declarations, we might
                    // create another do..end block later that would eliminate the
                    // need for this one. But order of decls should fix this.
                    state.blocks.Add(new DoEndBlock(state.function, begin, decl.end + 1));
                    StrictScopeCheck(state);
                }
            }
        }
    }

    // --- Simple opcode/condition predicates ----------------------------------------

    private static bool IsConditional(Branch b)
    {
        return b.type == BranchType.Comparison || b.type == BranchType.Test;
    }

    private static bool IsAssignment(Branch b)
    {
        return b.type == BranchType.TestSet;
    }

    private static bool IsAssignment(Branch b, int r)
    {
        return b.type == BranchType.TestSet || b.type == BranchType.Test && b.target == r;
    }

    private static bool IsJmpRaw(State state, int line)
    {
        Op op = state.code.Op(line);
        return op == Op.JMP || op == Op.JMP52 || op == Op.JMP54;
    }

    private static bool IsJmp(State state, int line)
    {
        Code code = state.code;
        Op op = code.Op(line);
        if (op == Op.JMP || op == Op.JMP54)
        {
            return true;
        }
        else if (op == Op.JMP52)
        {
            return !IsClose(state, line);
        }
        else
        {
            return false;
        }
    }

    private static bool IsBreakJmp(State state, int line)
    {
        if (IsJmp(state, line))
        {
            int target = state.code.Target(line);
            Block breakable = EnclosingBreakableBlock(state, line);
            return target == breakable.end || target == state.resolved[breakable.end];
        }
        return false;
    }

    private static bool IsClose(State state, int line)
    {
        Code code = state.code;
        Op op = code.Op(line);
        if (op == Op.CLOSE)
        {
            return true;
        }
        else if (op == Op.CLOSE55)
        {
            return code.B(line) == 0;
        }
        else if (op == Op.JMP52)
        {
            int target = code.Target(line);
            if (target == line + 1)
            {
                return code.A(line) != 0;
            }
            else
            {
                if (line + 1 <= code.length && code.Op(line + 1) == Op.JMP52)
                {
                    return target == code.Target(line + 1) && code.A(line) != 0;
                }
                else
                {
                    return false;
                }
            }
        }
        else
        {
            return false;
        }
    }

    private static bool IsNonjumpClose(State state, int line)
    {
        Code code = state.code;
        Op op = code.Op(line);
        if (op == Op.CLOSE)
        {
            return true;
        }
        else if (op == Op.CLOSE55)
        {
            return code.B(line) == 0;
        }
        else
        {
            return false;
        }
    }

    private static int GetCloseValue(State state, int line)
    {
        Code code = state.code;
        Op op = code.Op(line);
        if (op == Op.CLOSE || op == Op.CLOSE55)
        {
            return code.A(line);
        }
        else if (op == Op.JMP52)
        {
            return code.A(line) - 1;
        }
        else
        {
            throw new InvalidOperationException();
        }
    }

    private static CloseType GetCloseType(State state, int line)
    {
        if (line < 1 || !IsClose(state, line))
        {
            return CloseType.NONE;
        }
        else
        {
            Op op = state.code.Op(line);
            if (op == Op.CLOSE || op == Op.CLOSE55)
            {
                return state.function.header.version.closesemantics.Get() == Version.CloseSemantics.LUA54 ? CloseType.CLOSE54 : CloseType.CLOSE;
            }
            else
            {
                return CloseType.JMP;
            }
        }
    }

    // --- Block-search helpers ------------------------------------------------------

    private static Block EnclosingBlock(State state, int line)
    {
        Block enclosing = null;
        foreach (Block block in state.blocks)
        {
            if (block.Contains(line))
            {
                if (enclosing == null || enclosing.Contains(block))
                {
                    enclosing = block;
                }
            }
        }
        return enclosing;
    }

    private static Block EnclosingBreakableBlock(State state, int line)
    {
        Block enclosing = null;
        foreach (Block block in state.blocks)
        {
            if (block.Contains(line) && block.Breakable())
            {
                if (enclosing == null || enclosing.Contains(block))
                {
                    enclosing = block;
                }
            }
        }
        return enclosing;
    }

    private static Block EnclosingUnprotectedBlock(State state, int line)
    {
        Block enclosing = null;
        foreach (Block block in state.blocks)
        {
            if (block.Contains(line) && block.IsUnprotected())
            {
                if (enclosing == null || enclosing.Contains(block))
                {
                    enclosing = block;
                }
            }
        }
        return enclosing;
    }

    private static void StrictScopeCheck(State state)
    {
        if (state.function.header.config.strict_scope)
        {
            throw new System.Exception("Violation of strict scope rule");
        }
    }

    // --- Statement classifier ------------------------------------------------------
    // The has_statement / is_statement helpers land with the find_branches commit
    // since FindBranches/CombineBranches rely on adjacency tests that call them.
    // For the skeleton we expose conservative fall-back stubs.

    private static bool HasStatement(State state, int begin, int end)
    {
        for (int line = begin; line <= end; line++)
        {
            if (IsStatement(state, line)) return true;
        }
        return state.d.HasStatement(begin, end);
    }

    private static bool IsStatement(State state, int line)
    {
        if (state.reverse_targets[line]) return true;
        Registers r = state.r;
        if (r.GetNewLocals(line).Count > 0) return true;
        Code code = state.code;
        if (code.IsUpvalueDeclaration(line)) return false;
        Op op = code.Op(line);
        if (op == null) throw new InvalidOperationException("Illegal opcode at line " + line);
        switch (op.Type)
        {
            case OpT.MOVE:
            case OpT.LOADI:
            case OpT.LOADF:
            case OpT.LOADK:
            case OpT.LOADKX:
            case OpT.LOADBOOL:
            case OpT.LOADFALSE:
            case OpT.LOADTRUE:
            case OpT.LFALSESKIP:
            case OpT.GETGLOBAL:
            case OpT.GETUPVAL:
            case OpT.GETTABUP:
            case OpT.GETTABUP54:
            case OpT.GETTABLE:
            case OpT.GETTABLE54:
            case OpT.GETI:
            case OpT.GETFIELD:
            case OpT.NEWTABLE50:
            case OpT.NEWTABLE:
            case OpT.NEWTABLE54:
            case OpT.NEWTABLE55:
            case OpT.ADD:
            case OpT.SUB:
            case OpT.MUL:
            case OpT.DIV:
            case OpT.IDIV:
            case OpT.MOD:
            case OpT.POW:
            case OpT.BAND:
            case OpT.BOR:
            case OpT.BXOR:
            case OpT.SHL:
            case OpT.SHR:
            case OpT.UNM:
            case OpT.NOT:
            case OpT.LEN:
            case OpT.BNOT:
            case OpT.CONCAT:
            case OpT.CONCAT54:
            case OpT.CLOSURE:
            case OpT.TESTSET:
            case OpT.TESTSET54:
            case OpT.GETVARG:
                return r.IsLocal(code.A(line), line);
            case OpT.ADD54:
            case OpT.SUB54:
            case OpT.MUL54:
            case OpT.DIV54:
            case OpT.IDIV54:
            case OpT.MOD54:
            case OpT.POW54:
            case OpT.BAND54:
            case OpT.BOR54:
            case OpT.BXOR54:
            case OpT.SHL54:
            case OpT.SHR54:
            case OpT.ADDK:
            case OpT.SUBK:
            case OpT.MULK:
            case OpT.DIVK:
            case OpT.IDIVK:
            case OpT.MODK:
            case OpT.POWK:
            case OpT.BANDK:
            case OpT.BORK:
            case OpT.BXORK:
            case OpT.ADDI:
            case OpT.SHLI:
            case OpT.SHRI:
                return false; // only count following MMBIN* instruction
            case OpT.MMBIN:
            case OpT.MMBINI:
            case OpT.MMBINK:
                if (line <= 1) throw new InvalidOperationException();
                return r.IsLocal(code.A(line - 1), line - 1);
            case OpT.LOADNIL:
                for (int register = code.A(line); register <= code.B(line); register++)
                {
                    if (r.IsLocal(register, line))
                    {
                        return true;
                    }
                }
                return false;
            case OpT.LOADNIL52:
                for (int register = code.A(line); register <= code.A(line) + code.B(line); register++)
                {
                    if (r.IsLocal(register, line))
                    {
                        return true;
                    }
                }
                return false;
            case OpT.SETGLOBAL:
            case OpT.SETUPVAL:
            case OpT.SETTABUP:
            case OpT.SETTABUP54:
            case OpT.TAILCALL:
            case OpT.TAILCALL54:
            case OpT.RETURN:
            case OpT.RETURN54:
            case OpT.RETURN0:
            case OpT.RETURN1:
            case OpT.FORLOOP:
            case OpT.FORLOOP54:
            case OpT.FORPREP:
            case OpT.FORPREP54:
            case OpT.FORPREP55:
            case OpT.TFORCALL:
            case OpT.TFORCALL54:
            case OpT.TFORLOOP:
            case OpT.TFORLOOP52:
            case OpT.TFORLOOP54:
            case OpT.TFORPREP:
            case OpT.TFORPREP54:
            case OpT.TFORPREP55:
            case OpT.CLOSE:
            case OpT.CLOSE55:
            case OpT.ERRNNIL:
            case OpT.TBC:
                return true;
            case OpT.TEST50:
                return code.A(line) != code.B(line) && r.IsLocal(code.A(line), line);
            case OpT.SELF:
            case OpT.SELF54:
            case OpT.SELF55:
                return r.IsLocal(code.A(line), line) || r.IsLocal(code.A(line) + 1, line);
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
            case OpT.SETLIST50:
            case OpT.SETLISTO:
            case OpT.SETLIST:
            case OpT.SETLIST52:
            case OpT.SETLIST54:
            case OpT.SETLIST55:
            case OpT.VARARGPREP:
            case OpT.EXTRAARG:
            case OpT.EXTRABYTE:
                return false;
            case OpT.JMP:
            case OpT.JMP52:
            case OpT.JMP54:
                if (line == 1)
                {
                    return true;
                }
                else
                {
                    Op prev = line >= 2 ? code.Op(line - 1) : null;
                    Op next = line + 1 <= code.length ? code.Op(line + 1) : null;
                    if (prev == Op.EQ) return false;
                    if (prev == Op.LT) return false;
                    if (prev == Op.LE) return false;
                    if (prev == Op.EQ54) return false;
                    if (prev == Op.LT54) return false;
                    if (prev == Op.LE54) return false;
                    if (prev == Op.EQK) return false;
                    if (prev == Op.EQI) return false;
                    if (prev == Op.LTI) return false;
                    if (prev == Op.LEI) return false;
                    if (prev == Op.GTI) return false;
                    if (prev == Op.GEI) return false;
                    if (prev == Op.TEST50) return false;
                    if (prev == Op.TEST) return false;
                    if (prev == Op.TEST54) return false;
                    if (prev == Op.TESTSET) return false;
                    if (prev == Op.TESTSET54) return false;
                    if (next == Op.LOADBOOL && code.C(line + 1) != 0) return false;
                    if (next == Op.LFALSESKIP) return false;
                    return true;
                }
            case OpT.CALL:
            {
                int a = code.A(line);
                int c = code.C(line);
                if (c == 1)
                {
                    return true;
                }
                if (c == 0) c = r.registers - a + 1;
                for (int register = a; register < a + c - 1; register++)
                {
                    if (r.IsLocal(register, line))
                    {
                        return true;
                    }
                }
                return false;
            }
            case OpT.VARARG:
            {
                int a = code.A(line);
                int b = code.B(line);
                if (b == 0) b = r.registers - a + 1;
                for (int register = a; register < a + b - 1; register++)
                {
                    if (r.IsLocal(register, line))
                    {
                        return true;
                    }
                }
                return false;
            }
            case OpT.VARARG54:
            {
                int a = code.A(line);
                int c = code.C(line);
                if (c == 0) c = r.registers - a + 1;
                for (int register = a; register < a + c - 1; register++)
                {
                    if (r.IsLocal(register, line))
                    {
                        return true;
                    }
                }
                return false;
            }
            case OpT.SETTABLE:
            case OpT.SETTABLE54:
            case OpT.SETI:
            case OpT.SETFIELD:
                // special case -- this is actually ambiguous and must be resolved by the decompiler check
                return false;
            case OpT.DEFAULT:
            case OpT.DEFAULT54:
                throw new InvalidOperationException();
        }
        throw new InvalidOperationException("Illegal opcode: " + op);
    }
}
