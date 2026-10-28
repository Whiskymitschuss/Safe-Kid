using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ParentControl.Service;
using System.Security.Claims;

var builder=WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(o=>o.ServiceName="ParentControl Remote");
builder.Services.Configure<ParentOptions>(builder.Configuration.GetSection("ParentControl"));
builder.Services.AddSingleton<IStateStore,SqliteStateStore>();builder.Services.AddSingleton<IAccountController,WindowsAccountController>();builder.Services.AddSingleton<ControlService>();builder.Services.AddSingleton<LoginLimiter>();builder.Services.AddHostedService<EnforcementWorker>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{o.Cookie.Name="__Host-ParentControl";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=true;o.LoginPath="/";});
builder.Services.AddAuthorization();builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="__Host-ParentControl-CSRF";o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;});
var app=builder.Build();
await app.Services.GetRequiredService<IStateStore>().InitializeAsync();
app.Use(async(ctx,next)=>{var allowed=ctx.RequestServices.GetRequiredService<IOptions<ParentOptions>>().Value.AllowedNetworks;if(!ctx.Connection.RemoteIpAddress.IsAllowed(allowed)){ctx.Response.StatusCode=403;await ctx.Response.WriteAsync("Zugriff nur über lokales Netz oder Tailscale.");return;}ctx.Response.Headers.Append("X-Content-Type-Options","nosniff");ctx.Response.Headers.Append("X-Frame-Options","DENY");ctx.Response.Headers.Append("Referrer-Policy","no-referrer");ctx.Response.Headers.Append("Content-Security-Policy","default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; connect-src 'self'");await next();});
app.UseDefaultFiles();app.UseStaticFiles();app.UseAuthentication();app.UseAuthorization();
app.MapGet("/api/setup",async(IStateStore s)=>Results.Ok(new{required=!await s.HasParentAsync()}));
app.MapPost("/api/setup",async(LoginRequest req,IStateStore s,HttpContext c)=>{if(await s.HasParentAsync())return Results.Conflict();await s.CreateParentAsync(req.UserName,req.Password);await SignIn(c,req.UserName);return Results.Ok();});
app.MapPost("/api/login",async(LoginRequest req,IStateStore s,LoginLimiter limiter,HttpContext c)=>{var key=c.Connection.RemoteIpAddress?.ToString()??"unknown";if(limiter.IsBlocked(key))return Results.StatusCode(429);if(!await s.VerifyParentAsync(req.UserName,req.Password)){limiter.Fail(key);await Task.Delay(400);return Results.Unauthorized();}limiter.Success(key);await SignIn(c,req.UserName);return Results.Ok();});
app.MapPost("/api/logout",[Authorize]async(HttpContext c)=>{await c.SignOutAsync();return Results.Ok();});
app.MapGet("/api/csrf",[Authorize](HttpContext c,Microsoft.AspNetCore.Antiforgery.IAntiforgery a)=>{var t=a.GetAndStoreTokens(c);return Results.Ok(new{token=t.RequestToken});});
app.MapGet("/api/status",[Authorize]async(IStateStore s)=>{var x=await s.GetStateAsync();return Results.Ok(new StatusDto(Environment.MachineName,x.Locked,x.LockedUntil,x.Reason,x.UpdatedAt,x.LastLockAt,x.Revision,0));});
var write=new AuthorizeAttribute();
app.MapPost("/api/lock",[Authorize]async(LockRequest req,ControlService control,IStateStore s,HttpContext c)=>{var until=req.Until??(req.Minutes is>0?DateTimeOffset.UtcNow.AddMinutes(req.Minutes.Value):null);var x=await control.LockAsync(until,req.Reason??"Manuell gesperrt",c.RequestAborted);await s.AddAuditAsync("lock",x.Reason,c.Connection.RemoteIpAddress?.ToString()??"");return Results.Ok(x);});
app.MapPost("/api/unlock",[Authorize]async(ControlService control,IStateStore s,HttpContext c)=>{var x=await control.UnlockAsync(c.RequestAborted);await s.AddAuditAsync("unlock","Manuell",c.Connection.RemoteIpAddress?.ToString()??"");return Results.Ok(x);});
app.MapPost("/api/shutdown",[Authorize]async(IStateStore s,HttpContext c)=>{await s.AddAuditAsync("shutdown","Bestätigt",c.Connection.RemoteIpAddress?.ToString()??"");System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown.exe","/s /t 30 /c \"ParentControl Remote: Herunterfahren bestätigt\""){UseShellExecute=false,CreateNoWindow=true});return Results.Accepted();});
app.MapGet("/api/schedule",[Authorize]async(IStateStore s)=>Results.Ok(await s.GetScheduleAsync()));
app.MapPut("/api/schedule",[Authorize]async(ScheduleRule[] rules,IStateStore s)=>{await s.SaveScheduleAsync(rules);return Results.NoContent();});
app.Use(async(ctx,next)=>{if(ctx.Request.Path.StartsWithSegments("/api")&&HttpMethods.IsPost(ctx.Request.Method)||HttpMethods.IsPut(ctx.Request.Method)){if(ctx.User.Identity?.IsAuthenticated==true)await ctx.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>().ValidateRequestAsync(ctx);}await next();});
app.MapFallbackToFile("index.html");app.Run();

static async Task SignIn(HttpContext c,string user){var claims=new[]{new Claim(ClaimTypes.Name,user),new Claim(ClaimTypes.Role,"Parent")};await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));}
public partial class Program { }
