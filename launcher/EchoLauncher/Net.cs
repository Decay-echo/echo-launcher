// Echo Launcher -- HTTP fetches (get.bootnet.app JSON, GitHub release files), SHA-256, and the backend
// "ping" (a TCP connect to the backend's socket port, since the game's own ping is UDP inside a match).
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EchoLauncher
{
    public static class Net
    {
        static readonly HttpClient Http = MakeClient();

        static HttpClient MakeClient()
        {
            // TLS 1.2 explicitly: older Windows 10 builds still default .NET Framework to TLS 1.0/1.1.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            var c = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EchoLauncher/" + Program.Version + " (Windows)");
            return c;
        }

        public static async Task<string> GetText(string url, int timeoutSeconds = 15)
        {
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            using (var resp = await Http.GetAsync(url, cts.Token).ConfigureAwait(false))
            {
                if (!resp.IsSuccessStatusCode) throw new HttpRequestException("HTTP " + (int)resp.StatusCode + " for " + url);
                return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        // Downloads to `destination` (a .tmp path the caller chooses), reports progress 0..1, verifies size and
        // sha256 when given; the file is deleted on any failure.
        public static async Task Download(string url, string destination, string sha256, long size, Action<double> progress)
        {
            try
            {
                using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode) throw new HttpRequestException("HTTP " + (int)resp.StatusCode + " for " + url);
                    var total = resp.Content.Headers.ContentLength ?? size;
                    using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var dst = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buf = new byte[64 * 1024];
                        long done = 0;
                        int n;
                        while ((n = await src.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false)) > 0)
                        {
                            await dst.WriteAsync(buf, 0, n).ConfigureAwait(false);
                            done += n;
                            if (total > 0 && progress != null) progress(Math.Min(1.0, (double)done / total));
                            if (done > 512L * 1024 * 1024) throw new IOException("download larger than 512 MB; refusing");
                        }
                    }
                }
                var fi = new FileInfo(destination);
                if (size > 0 && fi.Length != size) throw new IOException("size mismatch: got " + fi.Length + " bytes, manifest says " + size);
                if (!string.IsNullOrEmpty(sha256))
                {
                    var got = Sha256File(destination);
                    if (!string.Equals(got, sha256, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("sha256 mismatch: got " + got + ", manifest says " + sha256);
                }
            }
            catch
            {
                try { if (File.Exists(destination)) File.Delete(destination); } catch { }
                throw;
            }
        }

        public static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var f = File.OpenRead(path))
            {
                var hash = sha.ComputeHash(f);
                var sb = new StringBuilder(64);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // Milliseconds to open a TCP connection to host:port, or -1.
        public static async Task<int> PingMs(string host, int port, int timeoutMs = 3000)
        {
            try
            {
                using (var tcp = new TcpClient())
                {
                    var sw = Stopwatch.StartNew();
                    var connect = tcp.ConnectAsync(host, port);
                    var done = await Task.WhenAny(connect, Task.Delay(timeoutMs)).ConfigureAwait(false);
                    if (done != connect || !tcp.Connected) return -1;
                    return (int)sw.ElapsedMilliseconds;
                }
            }
            catch { return -1; }
        }
    }
}
