using System.Security.Claims;
using System.Text.Json;
int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
ClaimsPrincipal User(string role,string? team,Dictionary<string,bool> permissions)=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,role),new Claim("support_team",team??""),new Claim("permissions_json",JsonSerializer.Serialize(permissions))},"QA"));
foreach(var code in DepartmentAccounts.Names.Keys){
 var role=code switch{"TECHNOLOGY" or "WORKSHOP" or "CALL_CENTER" or "TECHNICAL_FAILURE"=>"Technology","GENERAL_SERVICES"=>"GeneralServices","HUMAN_RESOURCES"=>"HumanResources","WAREHOUSE"=>"Warehouse",_=>"Department"};
 var team=role is "Department"?code:code is "WORKSHOP" or "CALL_CENTER" or "TECHNICAL_FAILURE" or "WAREHOUSE"?code:null;
 var defaults=OperationalPermissions.Defaults(role,team);
 Check(defaults["canRequestWarehouse"],"Request default: "+code);
 Check(defaults["canViewWarehouseInventory"],"Inventory default: "+code);
 foreach(var key in defaults.Keys){
  var saved=new Dictionary<string,bool>{{key,false}};var effective=new Dictionary<string,bool>();
  OperationalPermissions.Apply(effective,saved,role,team);
  var warehouseQr=(role=="Warehouse"||team=="WAREHOUSE")&&key is "canViewWorkshop" or "canViewMaintenance" or "canWithdrawWarehouseRequests";
  Check(effective[key]==(key=="canRequestWarehouse"||warehouseQr),"Role defaults and warehouse QR access: "+code+" "+key);
  Check(OperationalPermissions.Allowed(User(role,team,effective),key)==(key=="canRequestWarehouse"||warehouseQr),"HTTP request access matches session: "+code+" "+key);
  if(!defaults[key])Check(!OperationalPermissions.Allowed(User(role,team,new(){{key,true}}),key),"Cannot widen role: "+code+" "+key);
 }
 if(role=="Department"){
  Check(DepartmentAccounts.ApiAllowed("/api/warehouse/fulfillment/inventory","GET"),"Department reads catalog");
  Check(!DepartmentAccounts.ApiAllowed("/api/warehouse/fulfillment/withdraw","POST"),"Department cannot withdraw");
 }
}
var routes=new Dictionary<string,string>{["/api/workshop/intake"]="canOperateWorkshop",["/api/warehouse/fulfillment/requests/1/prepare"]="canPrepareWarehouseRequests",["/api/warehouse/fulfillment/withdraw"]="canWithdrawWarehouseRequests",["/api/institutional-documents/1/share"]="canShareDocuments",["/api/institutional-documents/1/sign"]="canSignDocuments",["/api/institutional-documents/1/receive"]="canReceiveDocuments",["/api/institutional-documents/1/trash"]="canDeleteDocuments",["/api/agency-transitions"]="canManageAgencyTransitions"};
foreach(var (path,key) in routes)Check(OperationalPermissions.Required(path,"POST")==key,"Route guard: "+path);
Check(!OperationalPermissions.Allowed(new ClaimsPrincipal(),"canViewDocuments"),"Anonymous denied");
Check(!OperationalPermissions.Allowed(new ClaimsPrincipal(),"canRequestWarehouse"),"Anonymous request denied");
var explicitSaved=new Dictionary<string,bool>{{ExplicitUserPermissions.Marker,true},{"canRequestWarehouse",false},{"canUploadDocuments",false}};
var explicitEffective=new Dictionary<string,bool>();
OperationalPermissions.Apply(explicitEffective,explicitSaved,"Technology","TECHNICIANS");
ExplicitUserPermissions.Apply(explicitEffective,explicitSaved,"Technology");
Check(explicitEffective["canRequestWarehouse"]&&!explicitEffective["canUploadDocuments"],"Explicit profile preserves universal requests and unrelated denials");
foreach(var role in new[]{"Technology","GeneralServices"}){
 Check(OperationalPermissions.Allowed(User(role,"TECHNICIANS",new(){{"canRequestWarehouse",false},{"canUploadDocuments",false},{"canViewDocuments",false}}),"canRequestWarehouse"),"Legacy technician request permission: "+role);
 Check(!OperationalPermissions.Allowed(User(role,"TECHNICIANS",new()),"canPrepareWarehouseRequests"),"Requesters cannot dispatch: "+role);
}
Check(OperationalPermissions.Required("/api/warehouse/fulfillment/inventory","GET")=="canRequestWarehouse","Requesters can load catalog");
foreach(var (role,team) in new[]{("Warehouse",(string?)"WAREHOUSE"),("Warehouse",null),("Technology","WAREHOUSE")}){
 var legacy=new Dictionary<string,bool>{{"canViewWorkshop",false},{"canViewMaintenance",false},{"canWithdrawWarehouseRequests",false},{"canAuthorizeWarehouseRequests",false},{"canLoadWarehouseInventory",false}};
 var effective=new Dictionary<string,bool>();OperationalPermissions.Apply(effective,legacy,role,team);
 foreach(var key in new[]{"canViewWorkshop","canViewMaintenance","canWithdrawWarehouseRequests"}){
  Check(effective[key],"Warehouse QR enabled in session: "+role+" "+team+" "+key);
  Check(OperationalPermissions.Allowed(User(role,team,legacy),key),"Warehouse QR enabled at HTTP guard: "+role+" "+team+" "+key);
 }
 Check(!effective.GetValueOrDefault("canAuthorizeWarehouseRequests")&&!effective.GetValueOrDefault("canLoadWarehouseInventory"),"QR does not grant approval or inventory import");
}
Check(!OperationalPermissions.Allowed(User("Technology","TECHNICIANS",new(){{"canWithdrawWarehouseRequests",false}}),"canWithdrawWarehouseRequests"),"Non-warehouse cannot use warehouse pickup QR");
Check(OperationalPermissions.Required("/api/institutional-documents/warehouse-groups","GET")=="canRequestWarehouse","Requesters can select groups");
Check(OperationalPermissions.Required("/api/institutional-documents/warehouse-submission/SOL-ALM-QA","GET")=="canRequestWarehouse","Request confirmation uses request permission");
Check(OperationalPermissions.Required("/api/institutional-documents/123","GET")=="canViewDocuments","General document access remains protected");
Check(OperationalPermissions.Allowed(User("Technology",null,new(){{"canRequestWarehouse",true},{"canViewDocuments",false}}),"canRequestWarehouse"),"Requester can confirm own submission without browsing documents");
Check(OperationalPermissions.Required("/api/admin/login","POST") is null,"Login unaffected");
Check(OperationalPermissions.Required("/api/institutional-documents","POST") is null,"Multipart request permission is selected by the endpoint, not generic upload permission");
Check(OperationalPermissions.Allowed(User("Technology",null,new(){{"canRequestWarehouse",true},{"canUploadDocuments",false}}),"canRequestWarehouse"),"Warehouse requests independent of generic document upload");
Console.WriteLine($"SUCCESS {checks} operational permission checks across 13 departments");
