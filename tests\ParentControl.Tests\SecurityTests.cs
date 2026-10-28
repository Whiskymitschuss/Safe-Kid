using ParentControl.Service;
using System.Net;

namespace ParentControl.Tests;
public class SecurityTests
{
    [Theory][InlineData("100.64.0.1",true)][InlineData("100.127.255.254",true)][InlineData("100.128.0.1",false)][InlineData("192.168.1.2",false)][InlineData("127.0.0.1",true)]
    public void NetworkFilter_accepts_only_configured_ranges(string address,bool expected)=>Assert.Equal(expected,IPAddress.Parse(address).IsAllowed(["100.64.0.0/10","127.0.0.0/8"]));
    [Fact] public void LoginLimiter_blocks_after_five_failures(){var l=new LoginLimiter();for(var i=0;i<5;i++)l.Fail("client");Assert.True(l.IsBlocked("client"));l.Success("client");Assert.False(l.IsBlocked("client"));}
}
