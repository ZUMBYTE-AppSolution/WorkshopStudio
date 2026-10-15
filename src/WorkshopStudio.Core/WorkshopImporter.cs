namespace WorkshopStudio.Core;

public sealed record WorkshopImportSummary(int Seen, int AddedRemote, int AddedMods, int AddedCollections, int ReadOnly);

/// <summary>Non-destructive import of a verified signed-in account's portfolio.</summary>
public static class WorkshopImporter
{
    public static WorkshopImportSummary Merge(Workspace workspace, SteamAccount account, PortfolioResult result, ulong uploadSteamId)
    {
        if(result.Report.SteamId64==0 || result.Report.SteamId64!=uploadSteamId)
            throw new InvalidOperationException("Webprofil, Einträge und Upload-Konto stimmen nicht überein.");
        if(result.Report.PagesRead==0)throw new InvalidOperationException("Es wurden keine Workshop-Seiten geprüft.");
        var addedRemote=0;var addedMods=0;var addedCollections=0;var readOnly=0;
        foreach(var incoming in result.Items) {
            if(incoming.Id==0||incoming.OwnerSteamId64!=uploadSteamId)
                throw new InvalidDataException("Workshop-Eintrag besitzt eine fremde oder fehlende Eigentümer-ID.");
            var old=workspace.RemoteItems.FirstOrDefault(i=>i.Id==incoming.Id&&i.OwnerSteamId64==uploadSteamId);
            if(old is null){workspace.RemoteItems.Add(incoming);addedRemote++;}
            else if(incoming.FieldStatus.GetValueOrDefault("owner")==RemoteFieldStatus.Known) {
                incoming.LocalModId=old.LocalModId;
                if(incoming.PreviewUrl==old.PreviewUrl)incoming.PreviewCachePath=old.PreviewCachePath;
                if(incoming.UpdatedAt==old.UpdatedAt){incoming.RemoteChangelog=old.RemoteChangelog;incoming.RemoteChangelogStatus=old.RemoteChangelogStatus;}
                workspace.RemoteItems[workspace.RemoteItems.IndexOf(old)]=incoming;
            }
            if(incoming.FieldStatus.GetValueOrDefault("owner")!=RemoteFieldStatus.Known){readOnly++;continue;}
            var app=workspace.Apps.FirstOrDefault(a=>a.AppId==incoming.ConsumerAppId);
            if(app is null && incoming.ConsumerAppId>0){
                app=new AppProfile{AppId=incoming.ConsumerAppId,Name="Steam App "+incoming.ConsumerAppId,
                    Template=incoming.ConsumerAppId==108600?GameTemplate.ProjectZomboid42:GameTemplate.Generic};
                workspace.Apps.Add(app);
            }
            if(app is null){readOnly++;continue;}
            if(incoming.FileType==2) {
                if(!workspace.Collections.Any(c=>c.SteamCollectionId==incoming.Id)) {
                    workspace.Collections.Add(new WorkshopCollection{AppProfileId=app.Id,Name=incoming.Title,
                        Description=incoming.Description,SteamCollectionId=incoming.Id});
                    addedCollections++;
                }
            } else if(incoming.FileType==0) {
                var existing=workspace.Mods.FirstOrDefault(m=>m.WorkshopId==incoming.Id||m.TestWorkshopId==incoming.Id);
                if(existing is null) {
                    existing=new ModProject{AppProfileId=app.Id,Name=incoming.Title,Description=incoming.Description,
                        Version="",PreviewFile="",Tags=string.Join("; ",incoming.Tags),ChangeNote="",
                        Visibility=incoming.Visibility??2,WorkshopId=incoming.Id,OwnerSteamId64=uploadSteamId,
                        ImportedFromWorkshop=true,RemoteBaselineUpdatedAt=incoming.UpdatedAt,
                        ContentFolder="",AutoPublish=false};
                    workspace.Mods.Add(existing);addedMods++;
                }
                var remote=workspace.RemoteItems.First(i=>i.Id==incoming.Id&&i.OwnerSteamId64==uploadSteamId);
                remote.LocalModId=existing.Id;
            } else readOnly++;
        }
        account.VerifiedWebSteamId64=uploadSteamId;account.WebIdentityCheckedAt=DateTimeOffset.UtcNow;
        account.VerifiedUploadSteamId64=uploadSteamId;account.UploadIdentityCheckedAt=DateTimeOffset.UtcNow;
        workspace.LastWorkshopScan=result.Report;
        WorkspaceStore.Validate(workspace);
        return new(result.Items.Count,addedRemote,addedMods,addedCollections,readOnly);
    }
}
