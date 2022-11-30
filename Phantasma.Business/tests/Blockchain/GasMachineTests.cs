using System;
using System.Linq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.VM;
using Phantasma.Core.Domain;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class GasMachineTests
{
    [Fact]
    public void gm_consume_gas_test()
    {
        var gm = CreateGasMachine();
        var consumed = 0;

        for (var i = 0; i < 100; i++)
        {
            gm.ConsumeGas(i);
            consumed += i;
        }

        gm.UsedGas.ShouldBe(consumed);
    }

    [Fact]
    public void gm_validate_opcode_get_test()
    {
        var gm = CreateGasMachine();

        gm.ValidateOpcode(Opcode.GET);

        gm.UsedGas.ShouldBe(5);
    }

    [Fact]
    public void gm_validate_opcode_LOAD_test()
    {
        var gm = CreateGasMachine();

        gm.ValidateOpcode(Opcode.LOAD);

        gm.UsedGas.ShouldBe(5);
    }

    [Fact]
    public void gm_validate_opcode_call_test()
    {
        var gm = CreateGasMachine();

        gm.ValidateOpcode(Opcode.CALL);

        gm.UsedGas.ShouldBe(5);
    }

    [Fact]
    public void gm_validate_opcode_put_test()
    {
        var gm = CreateGasMachine();

        gm.ValidateOpcode(Opcode.PUT);

        gm.UsedGas.ShouldBe(5);
    }

    [Fact]
    public void gm_validate_opcode_extcall_test()
    {
        var gm = CreateGasMachine();

        gm.ValidateOpcode(Opcode.EXTCALL);

        gm.UsedGas.ShouldBe(10);
    }

    [Fact]
    public void gm_validate_opcode_ctx_test()
    {
        var gm = CreateGasMachine();

        gm.ValidateOpcode(Opcode.CTX);

        gm.UsedGas.ShouldBe(10);
    }

    [Fact]
    public void gm_validate_opcode_switch_test()
    {
        var gm = CreateGasMachine();

        gm.ValidateOpcode(Opcode.SWITCH);

        gm.UsedGas.ShouldBe(100);
    }

    [Fact]
    public void gm_validate_opcode_nop_test()
    {
        var gm = CreateGasMachine();

        gm.ValidateOpcode(Opcode.NOP);

        gm.UsedGas.ShouldBe(0);
    }

    [Fact]
    public void gm_validate_opcode_ret_test()
    {
        var gm = CreateGasMachine();

        gm.ValidateOpcode(Opcode.RET);

        gm.UsedGas.ShouldBe(0);
    }

    [Fact]
    public void gm_validate_all_opcodes_test()
    {
        var gm = CreateGasMachine();

        var ops = Enum.GetValues(typeof(Opcode)).Cast<Opcode>();

        foreach (var op in ops)
        {
            gm.ValidateOpcode(op);
        }

        gm.UsedGas.ShouldBe(185);
    }

    [Fact]
    public void gm_execute_interop_constructor_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("something()");

        gm.UsedGas.ShouldBe(10);
    }

    [Fact]
    public void gm_execute_interop_runtime_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("Runtime.xx");

        gm.UsedGas.ShouldBe(50);
    }

    [Fact]
    public void gm_execute_interop_data_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("Data.xx");

        gm.UsedGas.ShouldBe(50);
    }

    [Fact]
    public void gm_execute_interop_map_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("Map.xx");

        gm.UsedGas.ShouldBe(50);
    }

    [Fact]
    public void gm_execute_interop_list_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("List.xx");

        gm.UsedGas.ShouldBe(50);
    }

    [Fact]
    public void gm_execute_interop_set_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("Set.xx");

        gm.UsedGas.ShouldBe(50);
    }

    [Fact]
    public void gm_execute_interop_nexus_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("Nexus.xx");

        gm.UsedGas.ShouldBe(1000);
    }

    [Fact]
    public void gm_execute_interop_org_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("Organization.xx");

        gm.UsedGas.ShouldBe(200);
    }

    [Fact]
    public void gm_execute_interop_oracle_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("Oracle.xx");

        gm.UsedGas.ShouldBe(200);
    }

    [Fact]
    public void gm_execute_interop_account_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("Account.xx");

        gm.UsedGas.ShouldBe(100);
    }

    [Fact]
    public void gm_execute_interop_leaderboard_test()
    {
        var gm = CreateGasMachine();

        var state = gm.ExecuteInterop("Leaderboard.xx");

        gm.UsedGas.ShouldBe(100);
    }

    [Fact]
    public void gm_execute_interop_invalid_test()
    {
        var gm = CreateGasMachine();

        Should.Throw<VMException>(() => gm.ExecuteInterop("Invalid.xx"), "invalid extcall namespace: Invalid @ ExecuteInterop");
    }

    private GasMachine CreateGasMachine()
    {
        return new GasMachine(new byte[1] { 0 }, 0);
    }

    public GasMachineTests()
    {
    }
}
