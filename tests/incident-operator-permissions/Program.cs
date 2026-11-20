using System.Reflection;

var assemblyPath=Directory.GetFiles(AppContext.BaseDirectory,"RegistroAgencias.SqlServer.V*.dll").OrderByDescending(File.GetLastWriteTimeUtc).First();
var assembly=Assembly.LoadFrom(assemblyPath);
var actorType=assembly.GetType("DocumentActor")??throw new Exception("DocumentActor no disponible");
var profiles=assembly.GetType("AdministrativeProfiles")??throw new Exception("AdministrativeProfiles no disponible");
var manage=profiles.GetMethod("CanManageIncidentReports",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)??throw new Exception("CanManageIncidentReports no disponible: "+string.Join(',',profiles.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Select(method=>method.Name)));
var decide=profiles.GetMethod("CanDecideIncidentTickets",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)??throw new Exception("CanDecideIncidentTickets no disponible");
object Actor(string role,string name,string login)=>Activator.CreateInstance(actorType,["qa-id",role,"CALL_CENTER",name,login,null,null,false,false])!;
void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}

foreach(var actor in new[]{
    Actor("Technology","Lucianny González","cuenta-legada@empresa.local"),
    Actor("Technology","Lucianny Gonzales","lucianny.gonzales@grupotejeda.local"),
    Actor("Technology","Yessica Mercedes","cuenta-yessica@empresa.local"),
    Actor("Administrator","Administrador principal","admin@local.com")})
{
    Check((bool)manage.Invoke(null,[actor])!,"operadora reconocida como administradora de incidentes");
    Check((bool)decide.Invoke(null,[actor])!,"operadora puede recibir y decidir incidentes");
}
var supervisor=Actor("GroupAdministrator","Steven Supervisor","steven@empresa.local");
Check(!(bool)decide.Invoke(null,[supervisor])!,"supervisor no puede autorizar incidentes");
