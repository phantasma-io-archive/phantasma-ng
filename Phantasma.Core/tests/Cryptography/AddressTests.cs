
using System;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;
using Xunit;

namespace Phantasma.Core.Tests.Cryptography;

public class AddressTests
{
    [Fact]
    public void null_address_test()
    {
        var address = Address.Null;
        Assert.Equal(address.ToByteArray().Length, Address.LengthInBytes);
        Assert.Equal(address.ToByteArray(), new byte[Address.LengthInBytes]);
    }
    
    [Fact]
    public void TestPhantasmaKeys()
    {
        var bytes = new byte[32];
        var key = new PhantasmaKeys(bytes);
        var user = PhantasmaKeys.Generate();
        var address = Address.FromWIF(user.ToWIF());
        Assert.Equal(address.Text, user.Address.Text);
        
        var address2 = Address.FromText(user.Address.Text);
        Assert.Equal(address2.Text, user.Address.Text);
        
        var address3 = Address.FromKey(key);
        Assert.Equal(address3.Text, key.Address.Text);
        
        var address4 = Address.FromBytes(address2.ToByteArray());
        Assert.Equal(address4.Text, address2.Text);
        
        var address5 = Address.Parse(address.Text);
        Assert.Equal(address5.Text, address.Text);
        
        Assert.True(Address.IsValidAddress(user.Address.Text));
        Assert.False(Address.IsValidAddress("P231jkansdaksndaasdas"));
        
        Assert.Equal(address5.ToString(), address.ToString());
        Assert.Equal(address5.GetSize(), address.GetSize());
        Assert.True(address5 == address);
        Assert.True(address5.Equals(address));
    }

    /*[Fact]
    public void TestAddress()
    {
        var keys = PhantasmaKeys.FromWIF("");
        Address address = keys.Address;
        Assert.Fail(address.Text);
    }*/
    
    [Fact]
    public void TestValidateSignData()
    {
        var key = PhantasmaKeys.Generate();
        var address = key.Address;
        var text = "Hello world";
        var data = Encoding.UTF8.GetBytes(text);
        string random = "";
        string mySignatureLocal = "";
        var signature = key.Sign(data, (signatureMine, myRandom, txError) =>
        {
            mySignatureLocal = Encoding.UTF8.GetString(signatureMine);
            random = Encoding.UTF8.GetString(myRandom);
            Assert.True(address.ValidateSignedData(mySignatureLocal, random,  text));
            return new byte[8];
        });
        
    }
    
    
}
