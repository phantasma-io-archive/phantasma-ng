using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Phantasma.Core.Tests;

[TestClass]
public class OpcodesTests
{
    [TestMethod]
    public void nop_test()
    {
        const int opcode = (int) Opcode.NOP;
        opcode.ShouldBe(0);
    }

    [TestMethod]
    public void move_test()
    {
        const int opcode = (int) Opcode.MOVE;
        opcode.ShouldBe(1);
    }

    [TestMethod]
    public void copy_test()
    {
        const int opcode = (int) Opcode.COPY;
        opcode.ShouldBe(2);
    }

    [TestMethod]
    public void push_test()
    {
        const int opcode = (int) Opcode.PUSH;
        opcode.ShouldBe(3);
    }

    [TestMethod]
    public void pop_test()
    {
        const int opcode = (int) Opcode.POP;
        opcode.ShouldBe(4);
    }

    [TestMethod]
    public void swap_test()
    {
        const int opcode = (int) Opcode.SWAP;
        opcode.ShouldBe(5);
    }

    [TestMethod]
    public void call_test()
    {
        const int opcode = (int) Opcode.CALL;
        opcode.ShouldBe(6);
    }

    [TestMethod]
    public void ext_call_test()
    {
        const int opcode = (int) Opcode.EXTCALL;
        opcode.ShouldBe(7);
    }

    [TestMethod]
    public void jmp_test()
    {
        const int opcode = (int) Opcode.JMP;
        opcode.ShouldBe(8);
    }

    [TestMethod]
    public void jmp_if_test()
    {
        const int opcode = (int) Opcode.JMPIF;
        opcode.ShouldBe(9);
    }

    [TestMethod]
    public void jmp_not_test()
    {
        const int opcode = (int) Opcode.JMPNOT;
        opcode.ShouldBe(10);
    }

    [TestMethod]
    public void ret_test()
    {
        const int opcode = (int) Opcode.RET;
        opcode.ShouldBe(11);
    }

    [TestMethod]
    public void throw_test()
    {
        const int opcode = (int) Opcode.THROW;
        opcode.ShouldBe(12);
    }

    [TestMethod]
    public void load_test()
    {
        const int opcode = (int) Opcode.LOAD;
        opcode.ShouldBe(13);
    }

    [TestMethod]
    public void cast_test()
    {
        const int opcode = (int) Opcode.CAST;
        opcode.ShouldBe(14);
    }

    [TestMethod]
    public void cat_test()
    {
        const int opcode = (int) Opcode.CAT;
        opcode.ShouldBe(15);
    }

    [TestMethod]
    public void range_test()
    {
        const int opcode = (int) Opcode.RANGE;
        opcode.ShouldBe(16);
    }

    [TestMethod]
    public void left_test()
    {
        const int opcode = (int) Opcode.LEFT;
        opcode.ShouldBe(17);
    }

    [TestMethod]
    public void right_test()
    {
        const int opcode = (int) Opcode.RIGHT;
        opcode.ShouldBe(18);
    }

    [TestMethod]
    public void size_test()
    {
        const int opcode = (int) Opcode.SIZE;
        opcode.ShouldBe(19);
    }

    [TestMethod]
    public void count_test()
    {
        const int opcode = (int) Opcode.COUNT;
        opcode.ShouldBe(20);
    }

    [TestMethod]
    public void not_test()
    {
        const int opcode = (int) Opcode.NOT;
        opcode.ShouldBe(21);
    }

    [TestMethod]
    public void and_test()
    {
        const int opcode = (int) Opcode.AND;
        opcode.ShouldBe(22);
    }

    [TestMethod]
    public void or_test()
    {
        const int opcode = (int) Opcode.OR;
        opcode.ShouldBe(23);
    }

    [TestMethod]
    public void xor_test()
    {
        const int opcode = (int) Opcode.XOR;
        opcode.ShouldBe(24);
    }

    [TestMethod]
    public void equal_test()
    {
        const int opcode = (int) Opcode.EQUAL;
        opcode.ShouldBe(25);
    }

    [TestMethod]
    public void lt_test()
    {
        const int opcode = (int) Opcode.LT;
        opcode.ShouldBe(26);
    }

    [TestMethod]
    public void gt_test()
    {
        const int opcode = (int) Opcode.GT;
        opcode.ShouldBe(27);
    }

    [TestMethod]
    public void lte_test()
    {
        const int opcode = (int) Opcode.LTE;
        opcode.ShouldBe(28);
    }

    [TestMethod]
    public void gte_test()
    {
        const int opcode = (int) Opcode.GTE;
        opcode.ShouldBe(29);
    }

    [TestMethod]
    public void inc_test()
    {
        const int opcode = (int) Opcode.INC;
        opcode.ShouldBe(30);
    }

    [TestMethod]
    public void dec_test()
    {
        const int opcode = (int) Opcode.DEC;
        opcode.ShouldBe(31);
    }

    [TestMethod]
    public void sign_test()
    {
        const int opcode = (int) Opcode.SIGN;
        opcode.ShouldBe(32);
    }

    [TestMethod]
    public void negate_test()
    {
        const int opcode = (int) Opcode.NEGATE;
        opcode.ShouldBe(33);
    }

    [TestMethod]
    public void abs_test()
    {
        const int opcode = (int) Opcode.ABS;
        opcode.ShouldBe(34);
    }

    [TestMethod]
    public void add_test()
    {
        const int opcode = (int) Opcode.ADD;
        opcode.ShouldBe(35);
    }

    [TestMethod]
    public void sub_test()
    {
        const int opcode = (int) Opcode.SUB;
        opcode.ShouldBe(36);
    }

    [TestMethod]
    public void mul_test()
    {
        const int opcode = (int) Opcode.MUL;
        opcode.ShouldBe(37);
    }

    [TestMethod]
    public void div_test()
    {
        const int opcode = (int) Opcode.DIV;
        opcode.ShouldBe(38);
    }

    [TestMethod]
    public void mod_test()
    {
        const int opcode = (int) Opcode.MOD;
        opcode.ShouldBe(39);
    }

    [TestMethod]
    public void shl_test()
    {
        const int opcode = (int) Opcode.SHL;
        opcode.ShouldBe(40);
    }

    [TestMethod]
    public void shr_test()
    {
        const int opcode = (int) Opcode.SHR;
        opcode.ShouldBe(41);
    }

    [TestMethod]
    public void min_test()
    {
        const int opcode = (int) Opcode.MIN;
        opcode.ShouldBe(42);
    }

    [TestMethod]
    public void max_test()
    {
        const int opcode = (int) Opcode.MAX;
        opcode.ShouldBe(43);
    }

    [TestMethod]
    public void pow_test()
    {
        const int opcode = (int) Opcode.POW;
        opcode.ShouldBe(44);
    }


    [TestMethod]
    public void ctx_test()
    {
        const int opcode = (int) Opcode.CTX;
        opcode.ShouldBe(45);
    }

    [TestMethod]
    public void switch_test()
    {
        const int opcode = (int) Opcode.SWITCH;
        opcode.ShouldBe(46);
    }

    [TestMethod]
    public void put_test()
    {
        const int opcode = (int) Opcode.PUT;
        opcode.ShouldBe(47);
    }

    [TestMethod]
    public void get_test()
    {
        const int opcode = (int) Opcode.GET;
        opcode.ShouldBe(48);
    }

    [TestMethod]
    public void clear_test()
    {
        const int opcode = (int) Opcode.CLEAR;
        opcode.ShouldBe(49);
    }

    [TestMethod]
    public void unpack_test()
    {
        const int opcode = (int) Opcode.UNPACK;
        opcode.ShouldBe(50);
    }

    [TestMethod]
    public void pack_test()
    {
        const int opcode = (int) Opcode.PACK;
        opcode.ShouldBe(51);
    }

    [TestMethod]
    public void debug_test()
    {
        const int opcode = (int) Opcode.DEBUG;
        opcode.ShouldBe(52);
    }

    [TestMethod]
    public void substr_test()
    {
        const int opcode = (int) Opcode.SUBSTR;
        opcode.ShouldBe(53);
    }
}
