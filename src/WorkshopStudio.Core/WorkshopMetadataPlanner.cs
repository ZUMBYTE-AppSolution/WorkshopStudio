using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

public sealed record MetadataDecision(string Field,string Label,bool Update,string Reason);
public sealed record WorkshopMetadataPlan(ModProject Mod,IReadOnlyList<MetadataDecision> Decisions,string NoteHash,string PreviewHash)
{
    public IReadOnlyCollection<string> Fields=>Decisions.Where(d=>d.Update).Select(d=>d.Field).ToArray();
    public string Summary=>string.Join("\n",Decisions.Select(d=>$"{d.Label}: {(d.Update?"aktualisieren":"beibehalten")} · {d.Reason}"));
}

/// <summary>Deterministic metadata decisions. Missing local values never request deletion.</summary>
public static class WorkshopMetadataPlanner
{
    public static string Normalize(string text)=>text.Replace("\r\n","\n").Replace('\r','\n').Trim();
    public static string HashText(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(text))));
    public static string RenderNote(ModProject mod)=>Normalize(mod.ChangeNote.Replace("{version}",mod.Version)
        .Replace("{date}",DateTime.Now.ToString("yyyy-MM-dd")));
    public static async Task<string> HashFileAsync(string path,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(path))return "";
        await using var stream=File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream,ct));
    }
    public static string LatestChangeNote(string changelog)
    {
        var lines=Normalize(changelog).Split('\n');
        for(var index=0;index<lines.Length;index++) {
            var unreleased=Regex.Match(lines[index],@"^(#{1,3})\s+\[?(?:Unreleased|Unveröffentlicht)\]?",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
            if(!unreleased.Success)continue;
            var next=index+1;
            while(next<lines.Length&&!Regex.IsMatch(lines[next],@"^#{1,"+unreleased.Groups[1].Length+@"}\s+",RegexOptions.None,TimeSpan.FromMilliseconds(100)))next++;
            var body=string.Join('\n',lines[(index+1)..next]).Trim();
            if(body.Length>0&&!body.StartsWith("Noch keine",StringComparison.OrdinalIgnoreCase))return string.Join('\n',lines[index..next]).Trim();
        }
        var start=Array.FindIndex(lines,l=>Regex.IsMatch(l,@"^#{1,3}\s+\[?v?\d",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100)));
        if(start<0)return Normalize(changelog);
        var level=lines[start].TakeWhile(c=>c=='#').Count();
        var end=start+1;
        while(end<lines.Length) {
            var heading=Regex.Match(lines[end],@"^(#{1,3})\s+",RegexOptions.None,TimeSpan.FromMilliseconds(100));
            if(heading.Success&&heading.Groups[1].Length<=level)break;
            end++;
        }
        return string.Join('\n',lines[start..end]).Trim();
    }
    private static string LocalTitle(ModProject mod,ReleaseChannel channel,string remoteTitle)=>channel==ReleaseChannel.Test&&mod.TestTitleSuffix.Length>0&&remoteTitle.EndsWith(mod.TestTitleSuffix,StringComparison.Ordinal)?remoteTitle[..^mod.TestTitleSuffix.Length]:remoteTitle;
    public static void RememberBaseline(ModProject profile,ReleaseChannel channel,RemoteWorkshopItem remote,bool mergeUnedited=false)
    {
        var previous=profile.GetMetadataBaseline(channel);var title=LocalTitle(profile,channel,remote.Title);
        if(mergeUnedited&&previous is not null) {
            if(HashText(profile.Name)==previous.TitleHash&&title.Length>0)profile.Name=title;
            if(HashText(profile.Description)==previous.DescriptionHash)profile.Description=remote.Description;
        }
        profile.SetMetadataBaseline(channel,new(HashText(title),HashText(remote.Description)));
    }
    public static void SeedBaselines(Workspace workspace)
    {
        foreach(var mod in workspace.Mods)foreach(var channel in Enum.GetValues<ReleaseChannel>()) {
            if(mod.GetMetadataBaseline(channel) is not null)continue;
            var remote=workspace.RemoteItems.FirstOrDefault(r=>r.Id==mod.GetWorkshopId(channel)&&r.FieldStatus.GetValueOrDefault("owner")==RemoteFieldStatus.Known);
            if(remote is not null)RememberBaseline(mod,channel,remote);
        }
    }
    public static ModProject ResolveLocal(ModProject profile,LocalWorkshopSchema schema,RemoteWorkshopItem? baseline,bool automatic,ReleaseChannel channel=ReleaseChannel.Live)
    {
        var effective=WorkspaceStore.Clone(profile);
        if(!automatic)return effective;
        // Compare against the previously accepted remote value, not only the newest scan.
        var previous=profile.GetMetadataBaseline(channel);
        var nameEdited=previous is not null?HashText(profile.Name)!=previous.TitleHash:baseline is null||Normalize(profile.Name)!=Normalize(LocalTitle(profile,channel,baseline.Title));
        var descriptionEdited=previous is not null?HashText(profile.Description)!=previous.DescriptionHash:baseline is not null&&Normalize(profile.Description)!=Normalize(baseline.Description)&&profile.Description.Length>0;
        if(!nameEdited)effective.Name=schema.Title.Length>0?schema.Title:baseline is not null?LocalTitle(profile,channel,baseline.Title):profile.Name;
        if(!descriptionEdited)effective.Description=schema.Description.Length>0?schema.Description:baseline?.Description??profile.Description;
        if(schema.Version.Length>0) {
            effective.Version=schema.Version;
            effective.Description=Regex.Replace(effective.Description,@"(?im)^([ \t]*(?:latest[ \t]+)?version[ \t]*[:=][ \t]*)[^\r\n\[]+",
                match=>match.Groups[1].Value+schema.Version,RegexOptions.None,TimeSpan.FromMilliseconds(200));
        }
        if(schema.Changelog.Length>0&&(profile.ChangeNote.Trim().Length==0||profile.ChangeNote.Trim()=="Update {version}"))effective.ChangeNote=LatestChangeNote(schema.Changelog);
        // A mod's poster is not necessarily its Workshop preview. Only an explicitly chosen preview is written.
        return effective;
    }
    public static WorkshopMetadataPlan Compare(ModProject mod,ReleaseChannel channel,RemoteWorkshopItem remote,
        string localPreviewHash,string remotePreviewHash,bool historyRead)
    {
        var result=new List<MetadataDecision>();
        void Text(string field,string label,string local,string current) {
            var changed=Normalize(local).Length>0&&Normalize(local)!=Normalize(current);
            result.Add(new(field,label,changed,Normalize(local).Length==0?"lokal nicht angegeben":changed?"lokaler Wert unterscheidet sich von Steam":"entspricht Steam"));
        }
        Text("title","Titel",mod.Name+(channel==ReleaseChannel.Test?mod.TestTitleSuffix:""),remote.Title);
        Text("description","Beschreibung",mod.Description,remote.Description);
        var visibility=channel==ReleaseChannel.Test?2:mod.Visibility;
        var allowVisibility=channel==ReleaseChannel.Test||mod.SynchronizeVisibility;
        result.Add(new("visibility","Sichtbarkeit",allowVisibility&&remote.Visibility is not null&&visibility!=remote.Visibility,
            !allowVisibility?"automatische Sichtbarkeitsänderung im Mod-Profil nicht freigegeben":remote.Visibility is null?"Steam-Wert nicht ermittelbar":visibility==remote.Visibility?"entspricht Steam":"lokal ausdrücklich anders eingestellt"));
        var previewChanged=localPreviewHash.Length>0 && localPreviewHash!=remotePreviewHash && localPreviewHash!=mod.GetLastPreviewHash(channel);
        result.Add(new("previewfile","Vorschaubild",previewChanged,localPreviewHash.Length==0?"kein Workshop-Bild ausgewählt":previewChanged?"ausgewähltes Bild hat andere oder noch nicht veröffentlichte Daten":"Bilddaten unverändert"));
        var note=RenderNote(mod);var noteHash=note.Length==0?"":HashText(note);
        var known=note.Length>0 && (noteHash==mod.GetLastNoteHash(channel) ||
            historyRead&&remote.RemoteChangeNotes.Any(existing=>Normalize(existing)==note));
        var noteChanged=note.Length>0&&!known&&(historyRead||mod.GetLastNoteHash(channel).Length>0);
        result.Add(new("changenote","Änderungsnotiz",noteChanged,note.Length==0?"keine lokale Notiz":known?"bereits veröffentlicht":
            noteChanged?"neue lokale Release-Notiz erkannt":"Steam-Verlauf nicht lesbar; erste Notiz zur Vermeidung von Duplikaten zurückgestellt"));
        if(Encoding.UTF8.GetByteCount(note)>8000)throw new InvalidDataException("Die aktuelle Änderungsnotiz ist länger als 8000 UTF-8-Bytes. Eine einzelne Release-Notiz im Changelog verwenden.");
        return new(mod,result,noteHash,localPreviewHash);
    }
}
