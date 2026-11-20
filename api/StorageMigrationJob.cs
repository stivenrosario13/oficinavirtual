partial class Database
{
    public async Task<StorageMigrationReport> CopyLegacyFiles(CancellationToken ct,Action<string> progress)
    {
        await using var connection=await Open(ct);
        return await StorageMigrationRunner.Run(connection,true,ct,progress);
    }
    public async Task<StorageMigrationRunner.ReleaseStatus> ReleaseStorageCopies(CancellationToken ct)
    {
        await using var connection=await Open(ct);
        return await StorageMigrationRunner.ReleaseVerified(connection,ct);
    }
    public async Task<(int Pending,int Verified)> FinalizeStorageCopies(CancellationToken ct)
    {
        await using var connection=await Open(ct);
        return await StorageMigrationRunner.FinalizePending(connection,ct);
    }
}

// The request and phase survive IIS restarts. File bytes live in the private queue;
// the SQL journal only declares a copy verified after its remote download succeeds.
sealed class StorageMigrationJob : BackgroundService
{
    readonly Database database;
    readonly IConfiguration config;
    readonly StorageUploadQueue queue;
    readonly string statePath;
    readonly SemaphoreSlim signal=new(0,1);
    readonly object sync=new();
    record JobState(string State,string Message,StorageMigrationReport? Report,DateTime? StartedAt,DateTime? FinishedAt,DateTime NextScan,long StagedBytesAtPause=0,StorageMigrationRunner.ReleaseStatus? Release=null);
    JobState state=new("idle","Sin iniciar",null,null,null,default);
    public StorageMigrationJob(Database database,IConfiguration config,StorageUploadQueue queue)
    {
        this.database=database;this.config=config;this.queue=queue;
        statePath=Path.Combine(queue.Root,"migration.json");
        if(File.Exists(statePath))state=StorageUploadQueue.Load<JobState>(statePath);
    }
    void Persist()=>StorageUploadQueue.Save(statePath,state);
    public object Status(){lock(sync)return new{enabled=StorageFiles.Enabled,configured=StorageFiles.Service is not null,state=state.State,message=state.Message,startedAt=state.StartedAt,finishedAt=state.FinishedAt,report=state.Report,release=state.Release};}
    public bool Start()
    {
        lock(sync)
        {
            if(state.State is "queued" or "running")return false;
            if(!StorageFiles.Enabled)throw new StorageServiceException("STORAGE_DISABLED","Activa STORAGE_ENABLED antes de copiar los archivos.");
            state=new("queued","Preparando cola; originales intactos",null,DateTime.UtcNow,null,default);Persist();
            if(signal.CurrentCount==0)signal.Release();return true;
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        if(StorageFiles.Enabled&&!string.Equals(config["STORAGE_MIGRATE_EXISTING"],"false",StringComparison.OrdinalIgnoreCase)&&state.State=="idle")Start();
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await signal.WaitAsync(TimeSpan.FromSeconds(10),stoppingToken);
                JobState snapshot;lock(sync)snapshot=state;
                if(!StorageFiles.Enabled)continue;
                if(snapshot.State is not ("queued" or "running" or "waiting") && !(snapshot.State=="complete"&&snapshot.Release is null))continue;
                if(snapshot.NextScan>DateTime.UtcNow)continue;
                if(snapshot.Report?.NeedsRescan==true)
                {
                    await database.FinalizeStorageCopies(stoppingToken);
                    var release=await database.ReleaseStorageCopies(stoppingToken);
                    lock(sync){state=state with{Release=release,NextScan=release.NextAttempt??default};Persist();}
                    if(release.Message is not null)continue;
                    var status=queue.Status();
                    if(status.Pending>0&&snapshot.StagedBytesAtPause-status.StagedBytes<Math.Min(50L*1024*1024,snapshot.StagedBytesAtPause))continue;
                }
                // A second app instance must not overwrite the first one's scan state.
                FileStream lease;
                try{lease=new FileStream(Path.Combine(queue.Root,"migration.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){continue;}
                using(lease)
                {
                    if(snapshot.State is "queued" or "running" || snapshot.Report?.NeedsRescan==true)
                    {
                        lock(sync){state=state with{State="running",Message="Preparando los archivos en la cola privada"};Persist();}
                        var result=await database.CopyLegacyFiles(stoppingToken,text=>{lock(sync)state=state with{Message=text};});
                        lock(sync){state=state with{Report=result,State=result.Queued>0||result.NeedsRescan?"waiting":result.Failed>0?"partial":"complete",Message=result.NeedsRescan?"Cola llena: se continuará cuando se libere espacio":result.Queued>0?"Cola preparada; esperando transferencias verificadas":result.Failed>0?"Hay archivos que requieren revisión":"Transferencias verificadas; comprobando liberación de SQL",NextScan=result.NeedsRescan?DateTime.UtcNow.AddMinutes(1):default,StagedBytesAtPause=result.NeedsRescan?queue.Status().StagedBytes:0,FinishedAt=result.Queued==0&&!result.NeedsRescan?DateTime.UtcNow:null};Persist();}
                    }
                    else
                    {
                        var totals=await database.FinalizeStorageCopies(stoppingToken);
                        lock(sync)
                        {
                            var report=(state.Report??new StorageMigrationReport(0,0,0,0,0,new())) with{Queued=totals.Pending,Copied=Math.Max(0,totals.Verified-(state.Report?.Existing??0))};
                            state=state with{Report=report,State=totals.Pending>0?"waiting":report.Failed>0?"partial":"complete",Message=totals.Pending>0?"Transferencia automática: los pendientes se conservan en cola":"Transferencias verificadas; comprobando liberación de SQL",FinishedAt=totals.Pending==0?DateTime.UtcNow:null};Persist();
                        }
                    }
                    var release=await database.ReleaseStorageCopies(stoppingToken);
                    lock(sync)
                    {
                        state=state with{Release=release};
                        if(release.Pending>0)state=state with{State="waiting",NextScan=release.NextAttempt??default,Message=release.Message??"Verificando descargas y liberando los originales de SQL"};
                        else if(state.State=="complete")state=state with{Message="Traslado finalizado: los archivos verificados se abren desde la API"};
                        Persist();
                    }
                }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Microsoft.Data.SqlClient.SqlException)
            {
                lock(sync){state=state with{NextScan=DateTime.UtcNow.AddMinutes(1),Message="Conexión SQL pendiente: se reintentará en un minuto"};Persist();}
            }
            catch(Exception e)
            {
                lock(sync){state=state with{State="failed",Message=e is StorageServiceException storage?storage.Message:"No se pudo continuar la cola de migración ("+e.GetType().Name+")",FinishedAt=DateTime.UtcNow};Persist();}
            }
        }
    }
}
