using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ParentControl.Service;

public sealed class LoginLimiter
{
    private readonly ConcurrentDictionary<string,(int Count,DateTimeOffset Since)> _items=new();
    public bool IsBlocked(string key){var x=_items.GetOrAdd(key,_=>(0,DateTimeOffset.UtcNow));if(DateTimeOffset.UtcNow-x.Since>TimeSpan.FromMinutes(15)){_items[key]=(0,DateTimeOffset.UtcNow);return false;}return x.Count>=5;}
    public void Fail(string key)=>_items.AddOrUpdate(key,_=>(1,DateTimeOffset.UtcNow),(_,x)=>(x.Count+1,x.Since));
    public void Success(string key)=>_items.TryRemove(key,out _);
}

public static class SecurityExtensions
{
    public static bool IsAllowed(this IPAddress? ip,IEnumerable<string> networks)
    {
        if(ip is null)return false;if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();foreach(var n in networks){var p=n.Split('/');if(p.Length!=2||!IPAddress.TryParse(p[0],out var net)||!int.TryParse(p[1],out var bits))continue;if(net.AddressFamily!=ip.AddressFamily)continue;var a=ip.GetAddressBytes();var b=net.GetAddressBytes();var full=bits/8;var rem=bits%8;if(a.Take(full).SequenceEqual(b.Take(full))&&(rem==0||(a[full]&(byte)(0xff<<(8-rem)))==(b[full]&(byte)(0xff<<(8-rem)))))return true;}return false;
    }
    public static string NewCsrf()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
