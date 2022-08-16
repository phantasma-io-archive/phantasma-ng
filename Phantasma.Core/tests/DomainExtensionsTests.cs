using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Shouldly;

namespace Phantasma.Core.Tests;

//TODO
[TestClass]
public class DomainExtensionsTests
{
    [TestMethod]
    public void is_fungible()
    {
        var token = Substitute.For<IToken>();
        var fungible = token.IsFungible();
        fungible.ShouldBeFalse();
    }

    [TestMethod]
    public void is_burnable()
    {
        var token = Substitute.For<IToken>();
        var burnable = token.IsBurnable();
        burnable.ShouldBeFalse();
    }

    [TestMethod]
    public void is_transferable()
    {
        var token = Substitute.For<IToken>();
        var transferable = token.IsTransferable();
        transferable.ShouldBeFalse();
    }

    [TestMethod]
    public void is_capped()
    {
        var token = Substitute.For<IToken>();
        var capped = token.IsCapped();
        capped.ShouldBeFalse();
    }

    [TestMethod]
    public void get_last_block()
    {
        var runtime = Substitute.For<IRuntime>();
        var block = runtime.GetLastBlock();
        block.ShouldBeNull();
    }

    [TestMethod]
    public void get_chain_address()
    {
        //TODO
        var platform = Substitute.For<IPlatform>();
        var address = platform.GetChainAddress();
        //address.ShouldBe();
    }

    [TestMethod]
    public void get_root_chain()
    {
        var runtime = Substitute.For<IRuntime>();
        var chain = runtime.GetRootChain();
        chain.ShouldNotBeNull();
    }

    [TestMethod]
    public void is_readonly_mode()
    {
        var runtime = Substitute.For<IRuntime>();
        var isReadonly = runtime.IsReadOnlyMode();
        isReadonly.ShouldBeTrue();
    }

    [TestMethod]
    public void is_root_chain()
    {
        var runtime = Substitute.For<IRuntime>();
        var isRootChain = runtime.IsRootChain();
        isRootChain.ShouldBeTrue();
    }
}
