using System.Text.Json;
using WebPush;

public sealed record PushDeliveryRow(Guid NotificationId,string Endpoint,string P256dh,string Auth,long TicketNumber,string Subject,string? Message,Guid TicketId,string AssignedDepartment,string TicketType,string TicketStatus,DateTime CreatedAt);
public sealed record ChatPushDeliveryRow(Guid MessageId,string Endpoint,string P256dh,string Auth,Guid ConversationId,string AssignedDepartment,string SenderName,string Message,DateTime CreatedAt);
public sealed record PushTestResult(int Sent,int Registered,string? Error);

sealed class PwaPushService : BackgroundService
{
    readonly Database db;
    readonly ILogger<PwaPushService> log;
    readonly string publicKey;
    readonly string privateKey;
    readonly string subject;
    public bool UsesPersistedFallbackKeys { get; }

    public PwaPushService(Database db,ILogger<PwaPushService> log,IWebHostEnvironment environment)
    {
        this.db=db;
        this.log=log;
        subject=ValidSubject(Clean(Environment.GetEnvironmentVariable("VAPID_SUBJECT")));
        var configuredPublic=Clean(Environment.GetEnvironmentVariable("VAPID_PUBLIC_KEY"));
        var configuredPrivate=Clean(Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY"));
        if(ValidKeys(configuredPublic,configuredPrivate))
        {
            publicKey=configuredPublic!;
            privateKey=configuredPrivate!;
            return;
        }

        if(!string.IsNullOrWhiteSpace(configuredPublic)||!string.IsNullOrWhiteSpace(configuredPrivate))
            log.LogWarning("Las claves VAPID configuradas en MonsterASP no son válidas; se usará la identidad push persistente de App_Data.");
        UsesPersistedFallbackKeys=true;
        var keysPath=Clean(Environment.GetEnvironmentVariable("VAPID_KEYS_PATH"));
        if(string.IsNullOrWhiteSpace(keysPath))keysPath=Path.Combine(environment.ContentRootPath,"App_Data","Push","vapid-keys.json");
        try
        {
            if(File.Exists(keysPath))
            {
                var saved=JsonSerializer.Deserialize<VapidKeyFile>(File.ReadAllText(keysPath));
                if(saved is not null&&ValidKeys(saved.PublicKey,saved.PrivateKey))
                {
                    publicKey=saved.PublicKey;
                    privateKey=saved.PrivateKey;
                    return;
                }
            }
        }
        catch(Exception error){log.LogWarning(error,"No fue posible leer la identidad push persistente.");}

        var generated=VapidHelper.GenerateVapidKeys();
        publicKey=generated.PublicKey;
        privateKey=generated.PrivateKey;
        try
        {
            var directory=Path.GetDirectoryName(keysPath);
            if(!string.IsNullOrWhiteSpace(directory))Directory.CreateDirectory(directory);
            var temporary=$"{keysPath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(temporary,JsonSerializer.Serialize(new VapidKeyFile(publicKey,privateKey)));
            File.Move(temporary,keysPath,true);
        }
        catch(Exception error){log.LogError(error,"No fue posible guardar la identidad push. Las suscripciones podrían renovarse tras reiniciar el servidor.");}
    }

    public string PublicKey=>publicKey;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try{await Dispatch(stoppingToken);}
            catch(Exception error) when(!stoppingToken.IsCancellationRequested){log.LogWarning(error,"No fue posible consultar notificaciones push pendientes");}
            try{await Task.Delay(TimeSpan.FromSeconds(10),stoppingToken);}
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
        }
    }

    async Task Dispatch(CancellationToken ct)
    {
        var client=new WebPushClient();
        var vapid=new VapidDetails(subject,publicKey,privateKey);
        foreach(var item in await db.PendingPushDeliveries(ct))
        {
            try
            {
                var url=$"/?notification=ticket&ticketId={item.TicketId:D}&ticket={item.TicketNumber}&department={Uri.EscapeDataString(item.AssignedDepartment)}&ticketType={Uri.EscapeDataString(item.TicketType)}&ticketStatus={Uri.EscapeDataString(item.TicketStatus)}&notificationKey={Uri.EscapeDataString($"TICKET_EVENT:{item.NotificationId:D}")}&notificationAt={Uri.EscapeDataString(item.CreatedAt.ToUniversalTime().ToString("O"))}";
                var payload=JsonSerializer.Serialize(new{title=$"Ticket #{item.TicketNumber}",body=string.IsNullOrWhiteSpace(item.Message)?item.Subject:item.Message,icon="/loto-real-logo-transparent.png",tag=$"ticket-{item.TicketNumber}-{item.NotificationId:N}",url,requireInteraction=true,renotify=false,vibrate=new[]{180,80,180}});
                await client.SendNotificationAsync(new PushSubscription(item.Endpoint,item.P256dh,item.Auth),payload,vapid);
                await db.MarkPushDelivered(item.NotificationId,item.Endpoint,ct);
            }
            catch(WebPushException error) when(error.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                await db.DeletePushSubscription(item.Endpoint,ct);
            }
            catch(Exception error) when(!ct.IsCancellationRequested)
            {
                log.LogWarning(error,"No se pudo entregar la notificación push para {Endpoint}",item.Endpoint);
            }
        }
        foreach(var item in await db.PendingChatPushDeliveries(ct))
        {
            try
            {
                var url=$"/?notification=chat&conversationId={item.ConversationId:D}&department={Uri.EscapeDataString(item.AssignedDepartment)}&notificationKey={Uri.EscapeDataString($"CHAT_MESSAGE:{item.MessageId:D}")}&notificationAt={Uri.EscapeDataString(item.CreatedAt.ToUniversalTime().ToString("O"))}";
                var payload=JsonSerializer.Serialize(new{title=$"Mensaje de {item.SenderName}",body=item.Message,icon="/loto-real-logo-transparent.png",tag=$"chat-{item.MessageId:N}",url,requireInteraction=true,renotify=false,vibrate=new[]{180,80,180}});
                await client.SendNotificationAsync(new PushSubscription(item.Endpoint,item.P256dh,item.Auth),payload,vapid);
                await db.MarkPushDelivered(item.MessageId,item.Endpoint,ct);
            }
            catch(WebPushException error) when(error.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                await db.DeletePushSubscription(item.Endpoint,ct);
            }
            catch(Exception error) when(!ct.IsCancellationRequested)
            {
                log.LogWarning(error,"No se pudo entregar la notificación push del mensaje para {Endpoint}",item.Endpoint);
            }
        }
    }

    public async Task<PushTestResult> SendTest(string userId,CancellationToken ct)
    {
        var subscriptions=await db.PushSubscriptions(userId,ct);
        var client=new WebPushClient();
        var vapid=new VapidDetails(subject,publicKey,privateKey);
        var sent=0;
        string? failure=null;
        foreach(var subscription in subscriptions)
        {
            try
            {
                var payload=JsonSerializer.Serialize(new{title="Prueba de notificación",body="Las notificaciones push de Grupo Tejeda están activas en este dispositivo.",icon="/loto-real-logo-transparent.png",tag=$"push-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",url="/",requireInteraction=true,renotify=true,vibrate=new[]{180,80,180}});
                await client.SendNotificationAsync(new PushSubscription(subscription.Endpoint,subscription.P256dh,subscription.Auth),payload,vapid);
                sent++;
            }
            catch(WebPushException error) when(error.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                await db.DeletePushSubscription(subscription.Endpoint,ct);
                failure="La suscripción anterior expiró. El dispositivo se registrará nuevamente al abrir la app.";
            }
            catch(WebPushException error)
            {
                failure=$"El proveedor push rechazó el envío (HTTP {(int?)error.StatusCode??0}). Activa nuevamente las notificaciones en este dispositivo.";
                log.LogWarning(error,"El proveedor push rechazó la prueba para {Endpoint}",subscription.Endpoint);
            }
            catch(Exception error) when(!ct.IsCancellationRequested)
            {
                failure="El servidor no pudo completar el envío push. Revisa la conexión saliente HTTPS de MonsterASP.";
                log.LogWarning(error,"Falló la prueba push para {Endpoint}",subscription.Endpoint);
            }
        }
        return new PushTestResult(sent,subscriptions.Count,failure);
    }

    static string? Clean(string? value)
    {
        value=value?.Trim();
        if(value?.Length>=2&&((value[0]=='\"'&&value[^1]=='\"')||(value[0]=='\''&&value[^1]=='\'')))value=value[1..^1].Trim();
        return value;
    }

    static string ValidSubject(string? value)
    {
        value=string.IsNullOrWhiteSpace(value)?"mailto:soporte@grupotejeda.com":value;
        try{VapidHelper.ValidateSubject(value);return value;}
        catch{return "mailto:soporte@grupotejeda.com";}
    }

    static bool ValidKeys(string? publicValue,string? privateValue)
    {
        if(string.IsNullOrWhiteSpace(publicValue)||string.IsNullOrWhiteSpace(privateValue))return false;
        try{VapidHelper.ValidatePublicKey(publicValue);VapidHelper.ValidatePrivateKey(privateValue);return true;}
        catch{return false;}
    }

    sealed record VapidKeyFile(string PublicKey,string PrivateKey);
}
