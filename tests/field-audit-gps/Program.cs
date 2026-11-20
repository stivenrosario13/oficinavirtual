void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
var now=DateTimeOffset.UtcNow;
Check(FieldAuditGps.ValidCapture(18.5,-71.1,950,now,now),"GPS rural válido conserva precisión real superior a 100 m");
Check(!FieldAuditGps.ValidCapture(0,0,950,now,now),"Coordenadas 0,0 continúan bloqueadas");
Check(!FieldAuditGps.ValidCapture(18.5,-71.1,0,now,now),"Precisión inválida continúa bloqueada");
Check(FieldAuditGps.Matches(18.5,-71.1,500,18.504,-71.1,500),"Fotos rurales coinciden dentro de sus márgenes GPS");
Check(!FieldAuditGps.Matches(18.5,-71.1,30,18.51,-71.1,30),"Fotos precisas distantes continúan bloqueadas");
