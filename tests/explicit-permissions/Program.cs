void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
var existing=new Dictionary<string,bool>{{"canResolveTickets",true},{"canManageUsers",false}};
ExplicitUserPermissions.Apply(existing,new(){{"canResolveTickets",false}},"GeneralServices");
Check(existing["canResolveTickets"],"Existing accounts retain previous behavior");
var saved=new Dictionary<string,bool>{{ExplicitUserPermissions.Marker,true},{"canResolveTickets",false},{"canManageUsers",true}};
ExplicitUserPermissions.Apply(existing,saved,"GeneralServices");
Check(!existing["canResolveTickets"],"New explicit false survives role defaults");
Check(existing["canManageUsers"],"Saved true is honored for a customized account");
var admin=new Dictionary<string,bool>{{"canManageUsers",true}};
ExplicitUserPermissions.Apply(admin,new(){{ExplicitUserPermissions.Marker,true},{"canManageUsers",false}},"Administrator");
Check(admin["canManageUsers"],"Administrator access remains protected");
Check(ExistingUserLogin.Unchanged(" ADMIN@LOCAL ","admin@local"),"Unchanged legacy login may keep its current format");
Check(!ExistingUserLogin.Unchanged("other@local","admin@local"),"Changed login still requires current validation");
Check(!ExistingUserLogin.Unchanged("",null),"Empty login not accepted as an existing login");
foreach(var marker in new[]{"profile:ADMINISTRATIVE","profile:DEPARTMENT_ADMINISTRATIVE"})
{
    var department=new Dictionary<string,bool>{{"canManageUsers",false},{"canManageDepartment",false},{"canCreateTickets",true}};
    ExplicitUserPermissions.Apply(department,new(){{marker,true},{ExplicitUserPermissions.Marker,true},{"canCreateTickets",false},{"canManageUsers",true}},"Department");
    Check(department.GetValueOrDefault(marker),"Department session preserves "+marker);
    Check(department["canManageUsers"]&&!department["canManageDepartment"]&&!department["canCreateTickets"],"Administrative navigation honors the complete customized permission set");
    var technician=new Dictionary<string,bool>();
    ExplicitUserPermissions.Apply(technician,new(){{marker,true}},"Technology");
    Check(!technician.GetValueOrDefault(marker),"Other roles do not inherit administrative navigation");
}
