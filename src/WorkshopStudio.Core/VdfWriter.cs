using System.Text;
using System.Text.RegularExpressions;

namespace WorkshopStudio.Core;

public static class VdfWriter
{
    public static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    public static string Build(AppProfile app, ModProject mod, ReleaseChannel channel, string contentPath, string previewPath,
        IReadOnlyCollection<string>? updateFields = null)
    {
        if (app.AppId == 0) throw new InvalidDataException("AppID fehlt.");
        var result = new StringBuilder("\"workshopitem\"\n{\n");
        void Field(string key, string value) => result.Append("    \"").Append(key).Append("\" \"").Append(Escape(value)).Append("\"\n");
        Field("appid", app.AppId.ToString());
        Field("publishedfileid", mod.GetWorkshopId(channel).ToString());
        Field("contentfolder", Path.GetFullPath(contentPath));
        bool Include(string field) => updateFields is null || updateFields.Contains(field);
        if (Include("previewfile") && !string.IsNullOrWhiteSpace(previewPath)) Field("previewfile", Path.GetFullPath(previewPath));
        if (Include("visibility")) Field("visibility", (channel == ReleaseChannel.Test ? 2 : mod.Visibility).ToString());
        if (Include("title")) Field("title", mod.Name + (channel == ReleaseChannel.Test ? mod.TestTitleSuffix : ""));
        if (Include("description")) Field("description", mod.Description);
        if (Include("changenote") && !string.IsNullOrWhiteSpace(mod.ChangeNote))
            Field("changenote", mod.ChangeNote.Replace("{version}", mod.Version).Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd")));
        result.Append("}\n");
        return result.ToString();
    }
    public static ulong ReadPublishedId(string text)
    {
        var m = Regex.Match(text, "\"publishedfileid\"\\s+\"(?<id>\\d+)\"", RegexOptions.IgnoreCase);
        return m.Success && ulong.TryParse(m.Groups["id"].Value, out var id) ? id : 0;
    }
    public static Dictionary<string,string> ReadFlat(string text)
    {
        var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(text, "\"(?<key>[^\"]+)\"\\s+\"(?<value>(?:\\\\.|[^\"\\\\])*)\""))
            result[m.Groups["key"].Value] = Unescape(m.Groups["value"].Value);
        return result;
    }
    private static string Unescape(string input) => Regex.Replace(input, @"\\(.)", m => m.Groups[1].Value switch { "n" => "\n", "r" => "\r", _ => m.Groups[1].Value });
}
