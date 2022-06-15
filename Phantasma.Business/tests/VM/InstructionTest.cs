using System;
using System.Text;
using Phantasma.Core;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.VM;

public class InstructionTest
{
    [Fact]
    public void null_test_test()
    {
        Instruction instruction = new Instruction();

        Should.NotThrow(() =>
        {
            instruction.ToString();
        });
    }
    
    #region CTX
    /// <summary>
    /// It could be Opcodes
    /// MOVE
    /// COPY
    /// SWAP
    /// SIZE
    /// SIGN
    /// NOT
    /// NEGATE
    /// ABS
    /// CTX
    /// </summary>
    [Fact]
    public void opcode_move_error_no_args_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.MOVE;
        instruction.Args = new object[] {};

        Should.Throw<Exception>(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_move_error_invalid_args_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.MOVE;
        instruction.Args = new object[] {"arg1"};

        Should.Throw<Exception>(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_move_not_throw_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.MOVE;
        byte arg1 = 0;
        byte arg2 = 1;
        instruction.Args = new object[] {arg1, arg2};

        Should.NotThrow(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_move_result_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.MOVE;
        byte arg1 = 0;
        byte arg2 = 1;
        instruction.Args = new object[] {arg1, arg2};

        Should.NotThrow(() =>
        {
            instruction.ToString().ShouldBe("000: MOVE r0, r1");
        });
    }
    #endregion
    
    #region Math
    /// <summary>
    /// It could be Opcodes
    /// ADD
    /// SUB
    /// MUL
    /// DIV
    /// MOD
    /// SHR
    /// SHL
    /// MIN
    /// MAX
    /// </summary>
    [Fact]
    public void opcode_add_error_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.ADD;
        byte arg1 = 0;
        byte arg2 = 1;
        byte arg3 = 1;
        instruction.Args = new object[] {arg1, arg2, ""};

        Should.Throw<Exception>(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_add_not_throw_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.ADD;
        byte arg1 = 0;
        byte arg2 = 1;
        byte arg3 = 1;
        instruction.Args = new object[] {arg1, arg2, arg3};

        Should.NotThrow(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_add_result_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.ADD;
        byte arg1 = 0;
        byte arg2 = 1;
        byte arg3 = 2;
        instruction.Args = new object[] {arg1, arg2, arg3};

        Should.NotThrow(() =>
        {
            instruction.ToString().ShouldBe("000: ADD r0, r1, r2");
        });
    }
    #endregion
    
    #region Arrays
    /// <summary>
    /// Opcodes : 
    /// POP
    /// PUSH
    /// EXTCALL
    /// DEC
    /// INC
    /// SWITCH
    /// </summary>
    [Fact]
    public void opcode_pop_error_no_args_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.POP;
        instruction.Args = new object[] {};

        Should.Throw<Exception>(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_pop_error_invalid_args_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.POP;
        instruction.Args = new object[] {"arg1"};

        Should.Throw<Exception>(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_pop_not_throw_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.POP;
        byte arg1 = 0;
        instruction.Args = new object[] {arg1};

        Should.NotThrow(() =>
        {
            instruction.ToString();//.ShouldBe("000: ADD r0, r1, r2");
        });
    }
    
    [Fact]
    public void opcode_pop_result_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.POP;
        byte arg1 = 0;
        instruction.Args = new object[] {arg1};

        Should.NotThrow(() =>
        {
            instruction.ToString().ShouldBe("000: POP r0");
        });
    }
    #endregion
    
    #region Call
    [Fact]
    public void opcode_call_error_no_args_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.CALL;
        instruction.Args = new object[] {};

        Should.Throw<Exception>(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_call_error_invalid_args_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.CALL;
        instruction.Args = new object[] {"arg1"};

        Should.Throw<Exception>(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_call_not_throw_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.CALL;
        byte arg1 = 0;
        ushort arg2 = 1;
        instruction.Args = new object[] {arg1, arg2};

        Should.NotThrow(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_call_result_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.CALL;
        byte arg1 = 0;
        ushort arg2 = 1;
        instruction.Args = new object[] {arg1, arg2};

        Should.NotThrow(() =>
        {
            instruction.ToString().ShouldBe("000: CALL0, 1");
        });
    }
    #endregion
    
    #region LOAD
    [Fact]
    public void opcode_load_error_no_args_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.LOAD;
        instruction.Args = new object[] {};

        Should.Throw<Exception>(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_load_error_invalid_args_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.LOAD;
        instruction.Args = new object[] {"arg1"};

        Should.Throw<Exception>(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_load_not_throw_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.LOAD;
        byte arg1 = 0;
        VMType arg2 = VMType.String;
        byte[] arg3 = Encoding.UTF8.GetBytes("test");
        instruction.Args = new object[] {arg1, arg2, arg3};

        Should.NotThrow(() =>
        {
            instruction.ToString();
        });
    }
    
    [Fact]
    public void opcode_load_result_string_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.LOAD;
        byte arg1 = 0;
        VMType arg2 = VMType.String;
        byte[] arg3 = Encoding.UTF8.GetBytes("test");
        instruction.Args = new object[] {arg1, arg2, arg3};

        Should.NotThrow(() =>
        {
            instruction.ToString().ShouldBe("000: LOAD r0, \"test\"");
        });
    }
    
    [Fact]
    public void opcode_load_result_number_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.LOAD;
        byte arg1 = 0;
        VMType arg2 = VMType.Number;
        byte[] arg3 = new byte[] { 2}; // 512 | 255 | 127 
        instruction.Args = new object[] {arg1, arg2, arg3};

        Should.NotThrow(() =>
        {
            instruction.ToString().ShouldBe("000: LOAD r0, 2");
        });
    }
    
    [Fact]
    public void opcode_load_result_other_test()
    {
        Instruction instruction = new Instruction();
        instruction.Offset = 0;
        instruction.Opcode = Opcode.LOAD;
        byte arg1 = 0;
        VMType arg2 = VMType.Bool;
        bool test = true;
        byte[] arg3 = BitConverter.GetBytes(test);
        instruction.Args = new object[] {arg1, arg2, arg3};

        Should.NotThrow(() =>
        {
            instruction.ToString().ShouldBe("000: LOAD r0, 01");
        });
    }
    #endregion

}
