using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

var publish=Path.GetFullPath(args.Single());
var scratch=Path.GetFullPath(Path.Combine("outputs","storage-access-"+Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(scratch);
var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=publish,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
start.ArgumentList.Add(Path.Combine(publish,"RegistroAgencias.SqlServer.V448.Storage.dll"));start.ArgumentList.Add("--urls");start.ArgumentList.Add($"http://127.0.0.1:{port}");
start.Environment["STORAGE_ENABLED"]="false";start.Environment["STORAGE_MIGRATE_EXISTING"]="false";start.Environment["STORAGE_QUEUE_PATH"]=Path.Combine(scratch,"queue");start.Environment["DATA_PROTECTION_KEYS_PATH"]=Path.Combine(scratch,"keys");start.Environment["LICENSE_ENFORCEMENT_ENABLED"]="false";
using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
try
{
    using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri($"http://127.0.0.1:{port}")};
    for(var i=0;i<50;i++){try{await client.GetAsync("/api/version");break;}catch(HttpRequestException){await Task.Delay(200);}}
    var services=new ServiceCollection();services.AddLogging();services.AddDataProtection().SetApplicationName("GrupoTejeda.OficinaVirtual").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(scratch,"keys")));
    using var provider=services.BuildServiceProvider();
    var format=new TicketDataFormat(provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware","Cookies","v2"));
    var checks=0;
    foreach(var role in new[]{"anonymous","GroupAdministrator","Technology","Department","Administrator"})
    {
        var paths=role=="Administrator"?new[]{"status","queue","analytics","usage","documents","verification"}:new[]{"status","queue","analytics","usage","documents","verification","verify","migrate","queue/resume"};
        foreach(var path in paths)
        {
            using var request=new HttpRequestMessage(path is "verify" or "migrate" or "queue/resume"?HttpMethod.Post:HttpMethod.Get,"/api/admin/storage/"+path);
            if(role!="anonymous")
            {
                // Signed local test identity exercises the published authorization
                // pipeline without reading any production account or database.
                var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"local-storage-qa"),new Claim(ClaimTypes.Role,role)},"Cookies"));
                var ticket=new AuthenticationTicket(principal,new AuthenticationProperties{IssuedUtc=DateTimeOffset.UtcNow,ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(5)},"Cookies");
                request.Headers.Add("Cookie","oficina_admin="+format.Protect(ticket));
            }
            using var response=await client.SendAsync(request);
            var expected=role=="anonymous"?HttpStatusCode.Unauthorized:role=="Administrator"?HttpStatusCode.OK:HttpStatusCode.Forbidden;
            if(response.StatusCode!=expected)throw new Exception($"{role} {path}: {response.StatusCode}; expected {expected}");
            checks++;
        }
        Console.WriteLine("PASS published storage access: "+role);
    }
    var admin=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"local-storage-qa"),new Claim(ClaimTypes.Role,"Administrator")},"Cookies"));
    var adminTicket=new AuthenticationTicket(admin,new AuthenticationProperties{IssuedUtc=DateTimeOffset.UtcNow,ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(5)},"Cookies");
    var controlPath=Path.Combine(scratch,"queue","control.json");
    foreach(var mandatory in new[]{false,true})
    {
        await File.WriteAllTextAsync(controlPath,System.Text.Json.JsonSerializer.Serialize(new{NextAttempt=DateTime.UtcNow.AddHours(1),ProviderRetryUntil=mandatory?DateTime.UtcNow.AddHours(1):DateTime.MinValue,Blocked=true}));
        using var verify=new HttpRequestMessage(HttpMethod.Post,"/api/admin/storage/verify");verify.Headers.Add("Cookie","oficina_admin="+format.Protect(adminTicket));
        using var response=await client.SendAsync(verify);
        var expected=mandatory?HttpStatusCode.Conflict:HttpStatusCode.Accepted;
        if(response.StatusCode!=expected)throw new Exception($"Manual verification mandatory={mandatory}: {response.StatusCode}; expected {expected}");
        checks++;
        if(!mandatory)
        {
            // Wait for the accepted background job before writing the next fixture.
            // It persists control state on failure and otherwise races that write.
            var finished=false;
            for(int attempt=0;attempt<100;attempt++)
            {
                using var status=new HttpRequestMessage(HttpMethod.Get,"/api/admin/storage/verification");status.Headers.Add("Cookie","oficina_admin="+format.Protect(adminTicket));
                using var stateResponse=await client.SendAsync(status);
                using var state=System.Text.Json.JsonDocument.Parse(await stateResponse.Content.ReadAsStringAsync());
                if(state.RootElement.GetProperty("state").GetString() is not ("running" or "queued")){finished=true;break;}
                await Task.Delay(100);
            }
            if(!finished)throw new Exception("Local verification did not finish before the next test fixture");
        }
    }
    Console.WriteLine($"{checks} HTTP access/manual verification checks passed; no production credentials used.");
}
finally{if(!process.HasExited)process.Kill(entireProcessTree:true);await process.WaitForExitAsync();await stdout;await stderr;}
