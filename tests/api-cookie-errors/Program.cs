using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net;

foreach(var corrected in new[]{false,true}){
 var builder=WebApplication.CreateBuilder();
 builder.WebHost.UseUrls("http://127.0.0.1:0");
 builder.Logging.ClearProviders();
 builder.Services.AddAuthentication("Cookies").AddCookie("Cookies",options=>{if(corrected)options.Events.OnRedirectToAccessDenied=ApiCookieEvents.AccessDenied;});
 builder.Services.AddAuthorization();
 await using var app=builder.Build();
 app.UseAuthentication();app.UseAuthorization();
 app.MapGet("/signin/{role}",async(string role,HttpContext context)=>{await context.SignInAsync("Cookies",new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"qa"),new Claim(ClaimTypes.Role,role)},"Cookies")));});
 app.MapPost("/api/request",()=>Results.Forbid()).RequireAuthorization();
 app.MapGet("/Account/AccessDenied",()=>Results.Content("<!doctype html><html>SPA</html>","text/html"));
 await app.StartAsync();
 try{
  foreach(var role in new[]{"Technology","GroupAdministrator","Department","Administrator"}){
   using var client=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=true}){BaseAddress=new Uri(app.Urls.Single())};
   await client.GetAsync("/signin/"+role);
   var response=await client.PostAsync("/api/request",new StringContent("QA"));
   if(corrected){
    if(response.StatusCode!=HttpStatusCode.Forbidden||response.Content.Headers.ContentType?.MediaType!="application/json"||!(await response.Content.ReadAsStringAsync()).Contains("ACCESS_DENIED")||response.Headers.Location is not null)throw new Exception("403 JSON not preserved for "+role);
   }else if(response.StatusCode!=HttpStatusCode.OK||response.Content.Headers.ContentType?.MediaType!="text/html")throw new Exception("Did not reproduce HTML redirect");
  }
  Console.WriteLine(corrected?"PASS HTTP 403 JSON retained across four roles":"PASS reproduced four forbidden requests silently redirected to HTTP 200 HTML");
 }finally{await app.StopAsync();}
}
