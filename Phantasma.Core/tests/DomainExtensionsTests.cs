using NSubstitute;
using Phantasma.Core.Domain;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests;

public class DomainExtensionsTests
{
    [Fact]
    public void is_fungible()
    {
        var token = Substitute.For<IToken>();
        var fungible = token.IsFungible();
        fungible.ShouldBeFalse();
    }

    [Fact]
    public void is_burnable()
    {
        var token = Substitute.For<IToken>();
        var burnable = token.IsBurnable();
        burnable.ShouldBeFalse();
    }

    [Fact]
    public void is_transferable()
    {
        var token = Substitute.For<IToken>();
        var transferable = token.IsTransferable();
        transferable.ShouldBeFalse();
    }

    [Fact]
    public void is_capped()
    {
        var token = Substitute.For<IToken>();
        var capped = token.IsCapped();
        capped.ShouldBeFalse();
    }

    [Fact]
    public void get_last_block()
    {
        var runtime = Substitute.For<IRuntime>();
        var block = runtime.GetLastBlock();
        block.ShouldBeNull();
    }

    [Fact]
    public void get_chain_address()
    {
        //TODO
        var platform = Substitute.For<IPlatform>();
        var address = platform.GetChainAddress();
        //address.ShouldBe();
    }

    [Fact]
    public void get_root_chain()
    {
        var runtime = Substitute.For<IRuntime>();
        var chain = runtime.GetRootChain();
        chain.ShouldNotBeNull();
    }

    [Fact]
    public void is_readonly_mode()
    {
        var runtime = Substitute.For<IRuntime>();
        var isReadonly = runtime.IsReadOnlyMode();
        isReadonly.ShouldBeTrue();
    }

    [Fact]
    public void is_root_chain()
    {
        var runtime = Substitute.For<IRuntime>();
        var isRootChain = runtime.IsRootChain();
        isRootChain.ShouldBeTrue();
    }
}
