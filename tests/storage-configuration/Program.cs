
using Microsoft.Extensions.Configuration;
var root=Path.Combine(Path.GetTempPath(),"Codex_StorageConfig_QA_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);int count=0;
void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
IConfiguration Config(string? key=null)=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"STORAGE_KEY",key}}).Build();
try{
 var config=Config();Check(StorageServerConfiguration.RestoreAndPersist(config,root,null)=="missing","Missing credential is not invented");
 config=Config("qa-secret-first");Check(StorageServerConfiguration.RestoreAndPersist(config,root,null)=="server","Explicit configuration takes precedence");
 Check(!Directory.Exists(Path.Combine(root,"wwwroot")),"Credential never written in static web root");
 config=Config();Check(StorageServerConfiguration.RestoreAndPersist(config,root,null)=="private-backup"&&config["STORAGE_KEY"]=="qa-secret-first","Replacing web.config does not lose storage access");
 config=Config("qa-secret-rotated");StorageServerConfiguration.RestoreAndPersist(config,root,null);
 config=Config();StorageServerConfiguration.RestoreAndPersist(config,root,null);Check(config["STORAGE_KEY"]=="qa-secret-rotated","Explicit key rotation updates durable backup");
 var rejected=false;try{StorageServerConfiguration.RestoreAndPersist(Config("test"),root,root);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Public directory cannot contain credential backup");
 File.WriteAllText(Path.Combine(root,"App_Data","storage-credentials.json"),"invalid json");Check(StorageServerConfiguration.RestoreAndPersist(Config(),root,null)=="missing","Corrupt backup does not crash the rest of the site");
 Console.WriteLine($"{count} checks passed");
}finally{if(Path.GetFileName(root).StartsWith("Codex_StorageConfig_QA_"))Directory.Delete(root,true);}
