using System.Collections.Generic;
using System.Numerics;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Shared.Types;

namespace Phantasma.Business.Tests.VM;

internal class ScriptContextConstants
{
    public static Address DefaultFromAddress = Address.FromText("P2KAPiHoaW4hp5b8wSUC1tojrihdRR56FU8tPoNVmNVDPYp");
    public static Address DefaultToAddress = Address.FromText("P2KA2x8P5sLfj75pAzQeSYA3QrFE2MzyV1WaHgVEcaEXGn6");

    public static byte[] TransferNftScript =>
        ScriptUtils.BeginScript().AllowGas(DefaultFromAddress, Address.Null, 100000, 6000).TransferNFT("GHOST",
                DefaultFromAddress, DefaultToAddress,
                BigInteger.Parse("80807712912753409015029052615541912663228133032695758696669246580757047529373"))
            .SpendGas(DefaultFromAddress).EndScript();

    public static byte[] CustomContractScript =>
        ScriptUtils.BeginScript().AllowGas(DefaultFromAddress, DefaultToAddress, 100000, 6000).CallContract("TEST",
            "mintToken", new List<object>
            {
                1,
                1,
                1,
                "P2KA2x8P5sLfj75pAzQeSYA3QrFE2MzyV1WaHgVEcaEXGn6",
                10,
                "TEST",
                1,
                "Test NFT",
                "This is a test NFT.",
                1,
                "ipfs://bafybeidsqqsvffcqsxvq3h4gnwybt4gzshnmqm2ie4ui74db3gwnwp6vp4",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                0,
                "",
                0,
                "",
                0,
                false
            }.ToArray()).SpendGas(DefaultFromAddress).EndScript();

    public static byte[] MigrateContractScript =>
        ScriptUtils.BeginScript().AllowGas(DefaultFromAddress, Address.Null, 100000, 6000)
            .CallContract("validator", "Migrate", DefaultFromAddress, DefaultToAddress).SpendGas(DefaultFromAddress)
            .EndScript();

    public static byte[] SettleTransactionScript =>
        ScriptUtils.BeginScript()
            .CallContract("interop", "SettleTransaction", DefaultFromAddress, "platform",
                "0x000000000000000000000000000000000000dead")
            .CallContract("swap", "SwapFee", DefaultFromAddress, "TEST",
                UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals))
            .TransferBalance("TEST", DefaultFromAddress, DefaultToAddress)
            .AllowGas(DefaultFromAddress, Address.Null, 100000, 500).SpendGas(DefaultFromAddress).EndScript();

    public static byte[] AliasScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "alias r1, $hello",
            "alias r2, $world",
            "load $hello, 3",
            "load $world, 2",
            "add r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] EventNotify1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "@notify: NOP ",
            "load r11 0x0100D232CB23F13D68E06E52F6C8A43085DFC9A265398E72C5B2EE8BC2D82378FBCF",
            "push r11",
            @"extcall ""Address()""",
            "pop r11",
            $"load r10, {(int)EventKind.Custom}",
            "load r12, \"customEvent\"",
            "push r12",
            "push r11",
            "push r10",
            "extcall \"Runtime.Notify\"",
            "ret"
        }.ToArray());

    public static byte[] EventNotify2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"test\"",
            "ctx r1, r2",
            "load r3, \"notify\"",
            "push r3",
            "switch r2",
            "ret"
        }.ToArray());

    public static byte[] TokenTriggersScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "alias r1, $triggerSend",
            "alias r2, $triggerReceive",
            "alias r3, $triggerBurn",
            "alias r4, $triggerMint",
            "alias r5, $currentTrigger",
            "alias r6, $comparisonResult",
            $@"load $triggerSend, ""{TokenTrigger.OnSend}""",
            $@"load $triggerReceive, ""{TokenTrigger.OnReceive}""",
            $@"load $triggerBurn, ""{TokenTrigger.OnBurn}""",
            $@"load $triggerMint, ""{TokenTrigger.OnMint}""",
            "push r1",
            "push r2",
            "push r3",
            "push r4",
            "pop $currentTrigger",
            "equal $triggerSend, $currentTrigger, $comparisonResult",
            "jmpif $comparisonResult, @sendHandler",
            "equal $triggerReceive, $currentTrigger, $comparisonResult",
            "jmpif $comparisonResult, @receiveHandler",
            "equal $triggerBurn, $currentTrigger, $comparisonResult",
            "jmpif $comparisonResult, @burnHandler",
            "equal $triggerMint, $currentTrigger, $comparisonResult",
            "jmpif $comparisonResult, @OnMint",
            "ret",
            "@sendHandler: ret",
            "@receiveHandler: ret",
            "@burnHandler: load r7 \"test burn handler exception\"",
            "throw r7",
            "@OnMint: ret"
        }.ToArray());

    public static byte[] TokenTriggersEventScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "alias r1, $triggerSend",
            "alias r2, $triggerReceive",
            "alias r3, $triggerBurn",
            "alias r4, $triggerMint",
            "alias r5, $currentTrigger",
            "alias r6, $comparisonResult",
            $@"load $triggerSend, ""{TokenTrigger.OnSend}""",
            $@"load $triggerReceive, ""{TokenTrigger.OnReceive}""",
            $@"load $triggerBurn, ""{TokenTrigger.OnBurn}""",
            $@"load $triggerMint, ""{TokenTrigger.OnMint}""",
            "push r1",
            "push r2",
            "push r3",
            "push r4",
            "pop $currentTrigger",

            //$"equal $triggerSend, $currentTrigger, $comparisonResult",
            //$"jmpif $comparisonResult, @sendHandler",

            //$"equal $triggerReceive, $currentTrigger, $comparisonResult",
            //$"jmpif $comparisonResult, @receiveHandler",

            //$"equal $triggerBurn, $currentTrigger, $comparisonResult",
            //$"jmpif $comparisonResult, @burnHandler",

            //$"equal $triggerMint, $currentTrigger, $comparisonResult",
            //$"jmpif $comparisonResult, @OnMint",

            "jmp @return",
            "@sendHandler: load r7 \"test send handler exception\"",
            "throw r7",
            "@receiveHandler: load r7 \"test received handler exception\"",
            "throw r7",
            "@burnHandler: load r7 \"test burn handler exception\"",
            "throw r7",
            "@OnMint: load r11 0x0100D232CB23F13D68E06E52F6C8A43085DFC9A265398E72C5B2EE8BC2D82378FBCF",
            "push r11",
            "extcall \"Address()\"",
            "pop r11",
            $"load r10, {(int)EventKind.Custom}",
            "load r12, \"customEvent\"",
            "push r12",
            "push r11",
            "push r10",
            "extcall \"Runtime.Notify\"",
            "ret",
            "@return: ret"
        }.ToArray());

    public static byte[] AccountTriggersAllowanceScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "alias r1, $temp",
            "alias r2, $from",
            "alias r3, $to",
            "alias r4, $symbol",
            "alias r5, $amount",
            "jmp @end",
            "@OnReceive: nop",
            "pop $from",
            "pop $to",
            "pop $symbol",
            "pop $amount",
            "load r1 \"TEST\"",
            "equal r1, $symbol, $temp",
            "jmpnot $temp, @end",
            "load $temp 2",
            "div $amount $temp $temp",
            "push $temp",
            "push $symbol",
            "load r11 0x0100D232CB23F13D68E06E52F6C8A43085DFC9A265398E72C5B2EE8BC2D82378FBCF",
            "push r11",
            "extcall \"Address()\"",
            "push $to",
            "load r0 \"Runtime.TransferTokens\"",
            "extcall r0",
            "jmp @end",
            "@end: ret"
        }.ToArray());

    public static byte[] AccountTriggersEventPropagationScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "alias r1, $triggerSend",
            "alias r2, $triggerReceive",
            "alias r3, $triggerBurn",
            "alias r4, $triggerMint",
            "alias r5, $currentTrigger",
            "alias r6, $comparisonResult",
            "alias r7, $triggerWitness",
            "alias r8, $currentAddress",
            "alias r9, $sourceAddress",
            $@"load $triggerSend, ""{AccountTrigger.OnSend}""",
            $@"load $triggerReceive, ""{AccountTrigger.OnReceive}""",
            $@"load $triggerBurn, ""{AccountTrigger.OnBurn}""",
            $@"load $triggerMint, ""{AccountTrigger.OnMint}""",
            $@"load $triggerWitness, ""{AccountTrigger.OnWitness}""",
            "push r1",
            "push r2",
            "push r3",
            "push r4",
            "push r5",
            "push r6",
            "push r7",
            "push r8",
            "push r9",
            "pop $currentTrigger",
            "pop $currentAddress",
            "equal $triggerWitness, $currentTrigger, $comparisonResult",
            "jmpif $comparisonResult, @witnessHandler",
            "equal $triggerSend, $currentTrigger, $comparisonResult",
            "jmpif $comparisonResult, @sendHandler",
            "equal $triggerReceive, $currentTrigger, $comparisonResult",
            "jmpif $comparisonResult, @receiveHandler",
            "equal $triggerBurn, $currentTrigger, $comparisonResult",
            "jmpif $comparisonResult, @burnHandler",
            "equal $triggerMint, $currentTrigger, $comparisonResult",
            "jmpif $comparisonResult, @OnMint",
            "jmp @end",
            "@witnessHandler: ",
            "load r11 0x0100D232CB23F13D68E06E52F6C8A43085DFC9A265398E72C5B2EE8BC2D82378FBCF",
            "push r11",
            "extcall \"Address()\"",
            "pop $sourceAddress",
            "equal $sourceAddress, $currentAddress, $comparisonResult",
            "jmpif $comparisonResult, @endWitness",
            "load r1 \"test witness handler xception\"",
            "throw r1",
            "jmp @end",
            "@sendHandler: jmp @end",
            "@receiveHandler: jmp @end",
            "@burnHandler: jmp @end",
            "@OnMint: load r11 0x0100D232CB23F13D68E06E52F6C8A43085DFC9A265398E72C5B2EE8BC2D82378FBCF",
            "push r11",
            "extcall \"Address()\"",
            "pop r11",
            $"load r10, {(int)EventKind.Custom}",
            "load r12, \"customEvent\"",
            "push r12",
            "push r11",
            "push r10",
            "extcall \"Runtime.Notify\"",
            "@endWitness: ret",
            "load r11 1",
            "push r11",
            "@end: ret"
        }.ToArray());

    public static byte[] MoveScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            //put a DebugClass with x = {r1} on register 1
            "load r1, 1",
            "push r1",
            "extcall \"PushDebugClass\"",
            "pop r1",

            //move it to r2, change its value on the stack and see if it changes on both registers
            "move r1, r2",
            "push r2",
            "push r1",
            "ret"
        }.ToArray());

    public static byte[] CopyScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            //put a DebugClass with x = {r1} on register 1
            "load r1, 2",
            "load r5, 1",
            "push r5",
            "extcall \"PushDebugStruct\"",
            "pop r1",
            "load r3, \"key\"",
            "put r1, r2, r3",

            //move it to r2, change its value on the stack and see if it changes on both registers
            "copy r1, r2",
            "push r2",
            "extcall \"IncrementDebugStruct\"",
            "push r1",
            "ret"
        }.ToArray());

    public static byte[] LoadScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"hello\"",
            "load r2, 123",
            "load r3, true",
            //load struct
            //load bytes
            //load enum
            //load object

            "push r3",
            "push r2",
            "push r1",
            "ret"
        }.ToArray());

    public static byte[] PopScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"hello\"",
            "load r2, 123",
            "load r3, true",
            //load struct
            //load bytes
            //load enum
            //load object

            "push r3",
            "push r2",
            "push r1",
            "pop r11",
            "pop r12",
            "pop r13",
            "push r13",
            "push r12",
            "push r11",
            "ret"
        }.ToArray());

    public static byte[] SwapScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"hello\"",
            "load r2, 123",
            "swap r1, r2",
            "push r1",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] CallScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1 2",
            "push r1",
            "call @label",
            "ret",
            "@label: pop r1",
            "inc r1",
            "push r1",
            "ret"
        }.ToArray());

    public static byte[] ExtCallScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "push r1",
            "extcall \"Upper\"",
            "ret"
        }.ToArray());

    public static byte[] JmpScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "jmp @label",
            "inc r1",
            "@label: push r1",
            "ret"
        }.ToArray());

    public static byte[] JmpConditionalScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "load r2, false",
            "load r3, 1",
            "load r4, 1",
            "jmpif r1, @label",
            "inc r3",
            "@label: jmpnot r2, @label2",
            "inc r4",
            "@label2: push r3",
            "push r4",
            "ret"
        }.ToArray());

    public static byte[] ThrowScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "push r1",
            "load r1 \"test throw exception\"",
            "throw r1",
            "not r1, r1",
            "pop r2",
            "push r1",
            "ret"
        }.ToArray());

    public static byte[] LocalOps1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "not r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] LocalOps2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"", 
            "not r1, r2", 
            "push r2", 
            "ret"
        }.ToArray());

    public static byte[] ClearScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "push r1",
            "clear r1",
            "ret"
        }.ToArray());

    public static byte[] CastScript =>
        ScriptUtils.BeginScript().EmitLoad(1, "1").EmitPush(1).Emit(Opcode.CAST, new byte[] { 1, 2, 3 }).EndScript();

    public static byte[] Count1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "push r1",
            "count r1, r2",
            "ret"
        }.ToArray());

    public static byte[] Count2Script =>
        ScriptUtils.BeginScript().EmitLoad(1, new byte[1], VMType.None).EmitPush(1)
            .Emit(Opcode.COUNT, new byte[] { 1, 2 }).EndScript();

    public static byte[] And1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "load r2, true",
            "and r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] And2Script =>
        ScriptUtils.BeginScript().EmitLoad(1, TestEnum.Value0).EmitLoad(2, TestEnum.Value0)
            .Emit(Opcode.AND, new byte[] { 1, 2, 3 }).EmitPush(3).EndScript();

    public static byte[] And3Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 1",
            "and r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] AndExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "load r2, 2",
            "lte r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Or1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "load r2, true",
            "or r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Or2Script =>
        ScriptUtils.BeginScript().EmitLoad(1, TestEnum.Value0).EmitLoad(2, TestEnum.Value0)
            .Emit(Opcode.OR, new byte[] { 1, 2, 3 }).EmitPush(3).EndScript();

    public static byte[] Or3Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 1",
            "or r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Or4Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "load r2, false",
            "or r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] OrExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "load r2, false",
            "or r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Xor1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "load r2, true",
            "xor r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Xor2Script =>
        ScriptUtils.BeginScript().EmitLoad(1, TestEnum.Value0).EmitLoad(2, TestEnum.Value0)
            .Emit(Opcode.XOR, new byte[] { 1, 2, 3 }).EmitPush(3).EndScript();

    public static byte[] Xor3Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 1",
            "xor r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] XorExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "load r2, false",
            "xor r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Equals1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "load r2, true",
            "equal r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Equals2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 1",
            "equal r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] LessThanScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 0",
            "load r2, 1",
            "lt r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] LessThanExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "load r2, 2",
            "lt r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] GreaterThanScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 0",
            "gt r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] GreaterThanExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "load r2, 2",
            "gt r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] LessThanOrEquals1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 1",
            "lte r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] LessThanOrEquals2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 0",
            "load r2, 1",
            "lte r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] LessThanOrEqualsExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "load r2, 2",
            "lte r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] GreaterThanOrEquals1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 0",
            "gte r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] GreaterThanOrEquals2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 1",
            "gte r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] GreaterThanOrEqualsExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "load r2, 2",
            "gte r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] IncrementScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "inc r1",
            "push r1",
            "ret"
        }.ToArray());

    public static byte[] IncrementExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"hello\"",
            "inc r1",
            "push r1",
            "ret"
        }.ToArray());

    public static byte[] DecrementScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 2",
            "dec r1",
            "push r1",
            "ret"
        }.ToArray());

    public static byte[] DecrementExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"hello\"",
            "dec r1",
            "push r1",
            "ret"
        }.ToArray());

    public static byte[] Sign1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, -1123124",
            "sign r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] Sign2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 0",
            "sign r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] SignExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "sign r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] NegateScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, -1123124",
            "negate r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] NegateExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "negate r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] AbsScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, -1123124",
            "abs r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] AbsExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "abs r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] Add1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 123098123049830982903580234959875213840923849203758942357834091",
            "load r2, 123098123049830982903580234959875213840923849203758942357834091",
            "add r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Add2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "load r2, \"abc\"",
            "add r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] AddException1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, \"abc\"",
            "add r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] AddException2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"abc\"",
            "load r2, 1",
            "add r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] SubScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 123098123049830982903580234959875213840923849203758942357834091",
            "load r2, 123098123049830982903580234959875213840923849203758942357834091",
            "sub r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] SubExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, \"abc\"",
            "sub r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] MulScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 123098123049830982903580234959875213840923849203758942357834091",
            "load r2, 123098123049830982903580234959875213840923849203758942357834091",
            "mul r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] MulExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, \"abc\"",
            "mul r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] DivScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 123098123049830982903580234959875213840923849203758942357834091",
            "load r2, 123098123049830982903580234959875213840923849203758942357834091",
            "div r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] DivExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, \"abc\"",
            "div r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] ModScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 123098123049830982903580234959875213840923849203758942357834091",
            "load r2, 123098123049830982903580234959875213840923849203758942357834091",
            "mod r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] ModExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, \"abc\"",
            "mod r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] ShiftLeftScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 123098123049830982903580234959875213840923849203758942357834091",
            "load r2, 100",
            "shl r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] ShiftLeftExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, \"abc\"",
            "shl r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] ShiftRightScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 123098123049830982903580234959875213840923849203758942357834091",
            "load r2, 100",
            "shr r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] ShiftRightExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, \"abc\"",
            "shr r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] MinScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 0",
            "min r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] MinExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, \"abc\"",
            "min r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] MaxScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, 0",
            "max r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] MaxExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "load r2, \"abc\"",
            "max r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] PowScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 3",
            "load r2, 3",
            "pow r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] PowExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "load r2, \"abc\"",
            "pow r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] ContextSwitchingScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"test\"",
            "load r3, 1",
            "push r3",
            "ctx r1, r2",
            "switch r2",
            "load r5, 42",
            "push r5",
            "ret"
        }.ToArray());

    public static byte[] PutGetScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            //$"switch \"Test\"",
            "load r1 1",
            "load r2 \"key\"",
            "put r1 r3 r2",
            "get r3 r4 r2",
            "push r4",
            "ret"
        }.ToArray());

    public static byte[] StructInteropScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            // first field
            "load r1 \"ID\"",
            "load r2 1234",
            "put r2 r3 r1",
            "load r1 \"name\"",

            // second field
            "load r2 \"monkey\"",
            "put r2 r3 r1",
            "load r1 \"address\"",

            // third field
            // this one is more complex because it is not a primitive type supported in the VM
            "load r2 0x010072F7967B8EAE2227837327B9D1CE4F32F14A1C4000A75ACCD5900FB4BC70452A",
            "push r2",
            "extcall \"Address()\"",
            "pop r2",
            "put r2 r3 r1",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] ArrayInteropScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1 0",
            "load r2 1",
            "put r2 r3 r1",
            "load r1 1",
            "load r2 42",
            "put r2 r3 r1",
            "load r1 2",
            "load r2 1024",
            "put r2 r3 r1",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Cat1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"Hello\"",
            "cat r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Cat2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"world\"",
            "cat r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Cat3Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"\"",
            "load r2, \"\"",
            "cat r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] Cat4Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"hello\"",
            "load r2, \"world\"",
            "cat r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] CatExceptionScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"Hello\"",
            "load r2, 1",
            "cat r1, r2, r3",
            "push r3",
            "ret"
        }.ToArray());

    public static byte[] RangeScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"Hello funny world\"",
            "range r1, r2, 6, 5",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] LeftScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"Hello world\"",
            "left r1, r2, 5",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] RightScript =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"Hello world\"",
            "right r1, r2, 5",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] Size1Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, \"Hello world\"",
            "size r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] Size2Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 1",
            "size r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] Size3Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, true",
            "size r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] Size4Script =>
        AssemblerUtils.BuildScript(new List<string>
        {
            "load r1, 0x000000",
            "size r1, r2",
            "push r2",
            "ret"
        }.ToArray());

    public static byte[] Size5Script =>
        ScriptUtils.BeginScript().EmitLoad(1, TestEnum.Value0).Emit(Opcode.SIZE, new byte[] { 1, 2 }).EmitPush(3)
            .EndScript();

    public static byte[] Size6Script =>
        ScriptUtils.BeginScript().EmitLoad(1, Timestamp.Now).Emit(Opcode.SIZE, new byte[] { 1, 2 }).EmitPush(3)
            .EndScript();

    public static byte[] UnpackScript =>
        ScriptUtils.BeginScript().EmitLoad(1, new byte[] { 0, 0, 0, 0, 1, 1, 1, 1 })
            .Emit(Opcode.UNPACK, new byte[] { 1, 2 }).EndScript();

    private enum TestEnum
    {
        Value0
    }
}
