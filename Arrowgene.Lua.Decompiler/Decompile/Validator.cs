namespace Arrowgene.Lua.Decompiler.Decompile;

/// <summary>
/// Port of unluac.decompile.Validator. Walks a function's bytecode after
/// parsing and rejects malformed instruction streams: missing implicit return,
/// out-of-range jump targets, conditions not followed by a jump, register or
/// constant operands referencing non-existent slots, and unknown opcodes.
/// </summary>
public static class Validator
{
    public static void Process(Function f, Registers r)
    {
        Code code = f.code;

        Op tail = code.Op(code.length);
        if (tail == Op.RETURN || tail == Op.RETURN54)
        {
            if (code.B(code.length) != 1)
            {
                throw new DecompilerException(f, code.length, "Implicit return must have zero values");
            }
        }
        else if (tail == Op.RETURN0)
        {
            // ok
        }
        else
        {
            throw new DecompilerException(f, code.length, "Function doesn't end with implicit return");
        }

        // Having validated final op allows look-ahead.

        for (int line = 1; line <= code.length; line++)
        {
            Op op = code.Op(line);
            if (op == null)
            {
                throw new DecompilerException(f, line, "Unknown opcode: " + code.Opcode(line));
            }
            if (op.HasJump())
            {
                int target = code.Target(line);
                if (target < 1 || target > code.length)
                {
                    throw new DecompilerException(f, line, "Jump target out of range: " + target);
                }
            }
            if (op.IsCondition() && !code.Op(line + 1).IsJmp())
            {
                throw new DecompilerException(f, line, "Condition is not followed by jump");
            }
            foreach (OperandFormat operand in op.Operands)
            {
                int x = code.Field(operand.field, line);
                switch (operand.format)
                {
                    case OperandFormat.Format.RAW:
                        // always okay (well...)
                        break;
                    case OperandFormat.Format.REGISTER:
                        if (x > r.registers)
                        {
                            throw new DecompilerException(f, line, "Register r" + x + " is out of range");
                        }
                        break;
                    case OperandFormat.Format.CONSTANT:
                        if (x > f.constants.Length)
                        {
                            throw new DecompilerException(f, line, "Constant k" + x + " is out of range");
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
