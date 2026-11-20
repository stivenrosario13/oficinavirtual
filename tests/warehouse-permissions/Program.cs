using System.Security.Claims;
var cases=new (string Role,string? Json,bool Allowed)[]{
 ("Administrator",null,true),("Administrator","{}",true),
 ("Administrator","{\"canAuthorizeWarehouseRequests\":false}",false),
 ("Administrator","{\"canAuthorizeWarehouseRequests\":true}",true),
 ("Warehouse","{\"canAuthorizeWarehouseRequests\":true}",true),
 ("Warehouse","{\"canAuthorizeWarehouseRequests\":false}",false),
 ("Warehouse",null,false),("Technology",null,false),
 ("Administrator","{bad",false),("Administrator","[]",false),
 ("Administrator","{\"canAuthorizeWarehouseRequests\":\"true\"}",false)
};
foreach(var test in cases){
 var claims=new List<Claim>{new(ClaimTypes.Role,test.Role)};
 if(test.Json is not null)claims.Add(new("permissions_json",test.Json));
 var user=new ClaimsPrincipal(new ClaimsIdentity(claims,"Cookies"));
 if(WarehousePermission.Has(user,"canAuthorizeWarehouseRequests")!=test.Allowed)throw new Exception($"Failed {test.Role} {test.Json}");
}
if(WarehousePermission.Has(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,"Administrator")})),"canAuthorizeWarehouseRequests"))throw new Exception("Anonymous access allowed");
Console.WriteLine("PASS 12 permission checks: legacy administrator, explicit grants/denials, malformed claims, anonymous access.");
