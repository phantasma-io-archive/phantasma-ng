using Phantasma.Core.Domain;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Domain;


public class OpcodesTests
{
    [Fact]
    public void nop_test()
    {
        const int opcode = (int) Opcode.NOP;
        opcode.ShouldBe(0);
    }

    [Fact]
    public void move_test()
    {
        const int opcode = (int) Opcode.MOVE;
        opcode.ShouldBe(1);
    }

    [Fact]
    public void copy_test()
    {
        const int opcode = (int) Opcode.COPY;
        opcode.ShouldBe(2);
    }

    [Fact]
    public void push_test()
    {
        const int opcode = (int) Opcode.PUSH;
        opcode.ShouldBe(3);
    }

    [Fact]
    public void pop_test()
    {
        const int opcode = (int) Opcode.POP;
        opcode.ShouldBe(4);
    }

    [Fact]
    public void swap_test()
    {
        const int opcode = (int) Opcode.SWAP;
        opcode.ShouldBe(5);
    }

    [Fact]
    public void call_test()
    {
        const int opcode = (int) Opcode.CALL;
        opcode.ShouldBe(6);
    }

    [Fact]
    public void ext_call_test()
    {
        const int opcode = (int) Opcode.EXTCALL;
        opcode.ShouldBe(7);
    }

    [Fact]
    public void jmp_test()
    {
        const int opcode = (int) Opcode.JMP;
        opcode.ShouldBe(8);
    }

    [Fact]
    public void jmp_if_test()
    {
        const int opcode = (int) Opcode.JMPIF;
        opcode.ShouldBe(9);
    }

    [Fact]
    public void jmp_not_test()
    {
        const int opcode = (int) Opcode.JMPNOT;
        opcode.ShouldBe(10);
    }

    [Fact]
    public void ret_test()
    {
        const int opcode = (int) Opcode.RET;
        opcode.ShouldBe(11);
    }

    [Fact]
    public void throw_test()
    {
        const int opcode = (int) Opcode.THROW;
        opcode.ShouldBe(12);
    }

    [Fact]
    public void load_test()
    {
        const int opcode = (int) Opcode.LOAD;
        opcode.ShouldBe(13);
    }

    [Fact]
    public void cast_test()
    {
        const int opcode = (int) Opcode.CAST;
        opcode.ShouldBe(14);
    }

    [Fact]
    public void cat_test()
    {
        const int opcode = (int) Opcode.CAT;
        opcode.ShouldBe(15);
    }

    [Fact]
    public void range_test()
    {
        const int opcode = (int) Opcode.RANGE;
        opcode.ShouldBe(16);
    }

    [Fact]
    public void left_test()
    {
        const int opcode = (int) Opcode.LEFT;
        opcode.ShouldBe(17);
    }

    [Fact]
    public void right_test()
    {
        const int opcode = (int) Opcode.RIGHT;
        opcode.ShouldBe(18);
    }

    [Fact]
    public void size_test()
    {
        const int opcode = (int) Opcode.SIZE;
        opcode.ShouldBe(19);
    }

    [Fact]
    public void count_test()
    {
        const int opcode = (int) Opcode.COUNT;
        opcode.ShouldBe(20);
    }

    [Fact]
    public void not_test()
    {
        const int opcode = (int) Opcode.NOT;
        opcode.ShouldBe(21);
    }

    [Fact]
    public void and_test()
    {
        const int opcode = (int) Opcode.AND;
        opcode.ShouldBe(22);
    }

    [Fact]
    public void or_test()
    {
        const int opcode = (int) Opcode.OR;
        opcode.ShouldBe(23);
    }

    [Fact]
    public void xor_test()
    {
        const int opcode = (int) Opcode.XOR;
        opcode.ShouldBe(24);
    }

    [Fact]
    public void equal_test()
    {
        const int opcode = (int) Opcode.EQUAL;
        opcode.ShouldBe(25);
    }

    [Fact]
    public void lt_test()
    {
        const int opcode = (int) Opcode.LT;
        opcode.ShouldBe(26);
    }

    [Fact]
    public void gt_test()
    {
        const int opcode = (int) Opcode.GT;
        opcode.ShouldBe(27);
    }

    [Fact]
    public void lte_test()
    {
        const int opcode = (int) Opcode.LTE;
        opcode.ShouldBe(28);
    }

    [Fact]
    public void gte_test()
    {
        const int opcode = (int) Opcode.GTE;
        opcode.ShouldBe(29);
    }

    [Fact]
    public void inc_test()
    {
        const int opcode = (int) Opcode.INC;
        opcode.ShouldBe(30);
    }

    [Fact]
    public void dec_test()
    {
        const int opcode = (int) Opcode.DEC;
        opcode.ShouldBe(31);
    }

    [Fact]
    public void sign_test()
    {
        const int opcode = (int) Opcode.SIGN;
        opcode.ShouldBe(32);
    }

    [Fact]
    public void negate_test()
    {
        const int opcode = (int) Opcode.NEGATE;
        opcode.ShouldBe(33);
    }

    [Fact]
    public void abs_test()
    {
        const int opcode = (int) Opcode.ABS;
        opcode.ShouldBe(34);
    }

    [Fact]
    public void add_test()
    {
        const int opcode = (int) Opcode.ADD;
        opcode.ShouldBe(35);
    }

    [Fact]
    public void sub_test()
    {
        const int opcode = (int) Opcode.SUB;
        opcode.ShouldBe(36);
    }

    [Fact]
    public void mul_test()
    {
        const int opcode = (int) Opcode.MUL;
        opcode.ShouldBe(37);
    }

    [Fact]
    public void div_test()
    {
        const int opcode = (int) Opcode.DIV;
        opcode.ShouldBe(38);
    }

    [Fact]
    public void mod_test()
    {
        const int opcode = (int) Opcode.MOD;
        opcode.ShouldBe(39);
    }

    [Fact]
    public void shl_test()
    {
        const int opcode = (int) Opcode.SHL;
        opcode.ShouldBe(40);
    }

    [Fact]
    public void shr_test()
    {
        const int opcode = (int) Opcode.SHR;
        opcode.ShouldBe(41);
    }

    [Fact]
    public void min_test()
    {
        const int opcode = (int) Opcode.MIN;
        opcode.ShouldBe(42);
    }

    [Fact]
    public void max_test()
    {
        const int opcode = (int) Opcode.MAX;
        opcode.ShouldBe(43);
    }

    [Fact]
    public void pow_test()
    {
        const int opcode = (int) Opcode.POW;
        opcode.ShouldBe(44);
    }


    [Fact]
    public void ctx_test()
    {
        const int opcode = (int) Opcode.CTX;
        opcode.ShouldBe(45);
    }

    [Fact]
    public void switch_test()
    {
        const int opcode = (int) Opcode.SWITCH;
        opcode.ShouldBe(46);
    }

    [Fact]
    public void put_test()
    {
        const int opcode = (int) Opcode.PUT;
        opcode.ShouldBe(47);
    }

    [Fact]
    public void get_test()
    {
        const int opcode = (int) Opcode.GET;
        opcode.ShouldBe(48);
    }

    [Fact]
    public void clear_test()
    {
        const int opcode = (int) Opcode.CLEAR;
        opcode.ShouldBe(49);
    }

    [Fact]
    public void unpack_test()
    {
        const int opcode = (int) Opcode.UNPACK;
        opcode.ShouldBe(50);
    }

    [Fact]
    public void pack_test()
    {
        const int opcode = (int) Opcode.PACK;
        opcode.ShouldBe(51);
    }

    [Fact]
    public void debug_test()
    {
        const int opcode = (int) Opcode.DEBUG;
        opcode.ShouldBe(52);
    }

    [Fact]
    public void substr_test()
    {
        const int opcode = (int) Opcode.SUBSTR;
        opcode.ShouldBe(53);
    }
}
