using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using WorkshopStudio.Core;

internal static partial class Program
{
    private static void RegisterReleaseUpdaterTests(string root)
    {
        string Metadata(string tag = "v1.2.0", string? zipUrl = null, bool draft = false) =>
            System.Text.Json.JsonSerializer.Serialize(new {
                tag_name = tag, draft, prerelease = false, body = "Änderungen",
                assets = new object[] {
                    new { name = "WorkshopStudio-1.2.0-win-x64.zip", browser_download_url = zipUrl ??
                        "https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/releases/download/v1.2.0/WorkshopStudio-1.2.0-win-x64.zip", size = 123L },
                    new { name = "SHA256SUMS.txt", browser_download_url =
                        "https://github.com/ZUMBYTE-AppSolution/WorkshopStudio/releases/download/v1.2.0/SHA256SUMS.txt", size = 100L }
                }
            });
        Test("Updater: offizielles stabiles Release erkannt", () => {
            var update = ReleaseUpdater.ParseLatest(Encoding.UTF8.GetBytes(Metadata()));
            Assert(update.Version == new Version(1,2,0) && update.PackageBytes == 123 && update.Tag == "v1.2.0");
        });
        Test("Updater: Fremd-URL und Entwurf abgewiesen", () => {
            Throws<InvalidDataException>(() => ReleaseUpdater.ParseLatest(Encoding.UTF8.GetBytes(Metadata(zipUrl:
                "https://github.com/other/repo/releases/download/v1.2.0/WorkshopStudio-1.2.0-win-x64.zip"))));
            Throws<InvalidDataException>(() => ReleaseUpdater.ParseLatest(Encoding.UTF8.GetBytes(Metadata(draft:true))));
        });
        Test("Updater: SHA-256 nur für exakten Paketnamen", () => {
            var hash = new string('A',64);
            Assert(ReleaseUpdater.ParseChecksum(hash+"  WorkshopStudio-1.2.0-win-x64.zip\n",
                "WorkshopStudio-1.2.0-win-x64.zip") == hash);
            Throws<InvalidDataException>(() => ReleaseUpdater.ParseChecksum(hash+"  other.zip\n",
                "WorkshopStudio-1.2.0-win-x64.zip"));
        });
        Test("Updater: vollständiges Paket sicher entpackt", () => {
            var basePath = Path.Combine(root,"update-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);
            var zip = Path.Combine(basePath,"release.zip");
            using (var archive = ZipFile.Open(zip,ZipArchiveMode.Create)) {
                foreach (var file in new[] {"WorkshopStudio.exe","WorkshopStudio.dll","WorkshopStudio.Core.dll"})
                    using (var writer = new StreamWriter(archive.CreateEntry("WorkshopStudio-win-x64/"+file).Open())) writer.Write("test");
                using var marker = new StreamWriter(archive.CreateEntry("WorkshopStudio-win-x64/release-info.json").Open());
                marker.Write("{\"version\":\"1.2.0\",\"repository\":\"ZUMBYTE-AppSolution/WorkshopStudio\"}");
            }
            var payload = Path.Combine(basePath,"payload");
            ReleaseUpdater.ExtractPackage(zip,payload,new Version(1,2,0));
            Assert(File.Exists(Path.Combine(payload,"WorkshopStudio.exe")));
        });
        Test("Updater: ZIP-Pfad außerhalb des Zielordners abgewiesen", () => {
            var basePath = Path.Combine(root,"update-slip-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);
            var zip = Path.Combine(basePath,"release.zip");
            using (var archive = ZipFile.Open(zip,ZipArchiveMode.Create))
                using (var writer = new StreamWriter(archive.CreateEntry("WorkshopStudio-win-x64/../escape.txt").Open())) writer.Write("bad");
            Throws<InvalidDataException>(() => ReleaseUpdater.ExtractPackage(zip,Path.Combine(basePath,"payload"),new Version(1,2,0)));
            Assert(!File.Exists(Path.Combine(basePath,"escape.txt")));
        });
        Test("Updater: falsche Paketversion abgewiesen", () => {
            var basePath = Path.Combine(root,"update-version-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);
            var zip = Path.Combine(basePath,"release.zip");
            using (var archive = ZipFile.Open(zip,ZipArchiveMode.Create)) {
                foreach (var file in new[] {"WorkshopStudio.exe","WorkshopStudio.dll","WorkshopStudio.Core.dll"})
                    using (var writer = new StreamWriter(archive.CreateEntry("WorkshopStudio-win-x64/"+file).Open())) writer.Write("test");
                using var marker = new StreamWriter(archive.CreateEntry("WorkshopStudio-win-x64/release-info.json").Open());
                marker.Write("{\"version\":\"9.9.9\",\"repository\":\"ZUMBYTE-AppSolution/WorkshopStudio\"}");
            }
            Throws<InvalidDataException>(() => ReleaseUpdater.ExtractPackage(zip,Path.Combine(basePath,"payload"),new Version(1,2,0)));
        });
        AsyncTest("Updater: Download, SHA-256 und Staging aus Testantworten", async () => {
            var basePath = Path.Combine(root,"update-download-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(basePath);
            var zip = Path.Combine(basePath,"release.zip");
            using (var archive = ZipFile.Open(zip,ZipArchiveMode.Create)) {
                foreach (var file in new[] {"WorkshopStudio.exe","WorkshopStudio.dll","WorkshopStudio.Core.dll"})
                    using (var writer = new StreamWriter(archive.CreateEntry("WorkshopStudio-win-x64/"+file).Open())) writer.Write("test");
                using var marker = new StreamWriter(archive.CreateEntry("WorkshopStudio-win-x64/release-info.json").Open());
                marker.Write("{\"version\":\"1.2.0\",\"repository\":\"ZUMBYTE-AppSolution/WorkshopStudio\"}");
            }
            var bytes = File.ReadAllBytes(zip);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var release = ReleaseUpdater.ParseLatest(Encoding.UTF8.GetBytes(Metadata())) with { PackageBytes = bytes.Length };
            using var client = new HttpClient(new UpdateTestHandler(request =>
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(
                    request.RequestUri == release.ChecksumsUrl ? Encoding.UTF8.GetBytes(hash+"  "+release.PackageName+"\n") : bytes) }));
            using var updater = new ReleaseUpdater(client);
            var payload = await updater.DownloadAndStageAsync(release,Path.Combine(basePath,"staging"));
            Assert(File.Exists(Path.Combine(payload,"WorkshopStudio.exe")));
        });
        AsyncTest("Updater: falscher Download-Hash stoppt vor Entpackung", async () => {
            var basePath = Path.Combine(root,"update-hash-"+Guid.NewGuid().ToString("N"));
            var bytes = Encoding.UTF8.GetBytes("kein Paket");
            var release = ReleaseUpdater.ParseLatest(Encoding.UTF8.GetBytes(Metadata())) with { PackageBytes = bytes.Length };
            using var client = new HttpClient(new UpdateTestHandler(request =>
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(
                    request.RequestUri == release.ChecksumsUrl ? Encoding.UTF8.GetBytes(new string('0',64)+"  "+release.PackageName+"\n") : bytes) }));
            using var updater = new ReleaseUpdater(client);
            await ThrowsAsync<InvalidDataException>(() => updater.DownloadAndStageAsync(release,basePath));
        });
    }

    private sealed class UpdateTestHandler(Func<HttpRequestMessage,HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) =>
            Task.FromResult(response(request));
    }
}
