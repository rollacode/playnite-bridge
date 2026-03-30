namespace PlayniteBridge.Services
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Script.Serialization;
    using Playnite.SDK;

    internal class BackendDiscovery
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private const int SyncPort = 19822;

        /// <summary>
        /// Discover all sync backends on the network.
        /// Priority: Tailscale > LAN > localhost.
        /// </summary>
        public static List<DiscoveredBackend> DiscoverAll()
        {
            var backends = new List<DiscoveredBackend>();
            var foundHostnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Try localhost FIRST (fastest, most common case)
            var localUrl = $"http://localhost:{SyncPort}";
            var localInfo = TryBackend(localUrl);
            if (localInfo != null)
            {
                var hostname = localInfo.tailscaleHostname ?? Environment.MachineName;
                foundHostnames.Add(hostname);
                // If backend has Tailscale, prefer TS URL even for localhost
                if (!string.IsNullOrEmpty(localInfo.tailscaleIp))
                {
                    backends.Add(new DiscoveredBackend
                    {
                        Url = $"http://{localInfo.tailscaleIp}:{SyncPort}",
                        Hostname = hostname,
                        TailscaleIp = localInfo.tailscaleIp,
                        ViaTailscale = true,
                    });
                }
                else
                {
                    backends.Add(new DiscoveredBackend
                    {
                        Url = localUrl,
                        Hostname = hostname,
                        TailscaleIp = null,
                        ViaTailscale = false,
                    });
                }
            }

            // 2. Scan Tailscale peers in PARALLEL (skip already found)
            var peers = GetTailscalePeers();
            if (peers.Count > 0)
            {
                var tasks = peers.Select(peer => Task.Run(() =>
                {
                    var url = $"http://{peer.Ip}:{SyncPort}";
                    var info = TryBackend(url);
                    if (info != null)
                        return new DiscoveredBackend
                        {
                            Url = url,
                            Hostname = info.tailscaleHostname ?? peer.Hostname,
                            TailscaleIp = peer.Ip,
                            ViaTailscale = true,
                        };
                    return null;
                })).ToArray();

                Task.WaitAll(tasks, TimeSpan.FromSeconds(8));

                foreach (var t in tasks)
                {
                    if (t.IsCompleted && t.Result != null)
                    {
                        var b = t.Result;
                        if (foundHostnames.Add(b.Hostname))
                            backends.Add(b);
                    }
                }
            }

            return backends;
        }

        /// <summary>Try to reach a backend at the given URL.</summary>
        public static SyncNetworkInfo TryBackend(string baseUrl)
        {
            try
            {
                Logger.Info($"Trying backend at {baseUrl}...");
                var response = Http.GetAsync($"{baseUrl}/api/network").GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Info($"Backend at {baseUrl}: HTTP {(int)response.StatusCode}");
                    return null;
                }
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json.Deserialize<SyncNetworkInfo>(body);
            }
            catch (Exception ex)
            {
                Logger.Info($"Backend at {baseUrl}: {ex.GetType().Name} - {ex.InnerException?.Message ?? ex.Message}");
                return null;
            }
        }

        /// <summary>Get list of online Tailscale peers.</summary>
        private static List<TailscalePeer> GetTailscalePeers()
        {
            var peers = new List<TailscalePeer>();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "tailscale",
                    Arguments = "status --json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                if (proc == null) return peers;

                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);
                if (proc.ExitCode != 0) return peers;

                var data = Json.Deserialize<Dictionary<string, object>>(output);
                if (data == null || !data.ContainsKey("Peer")) return peers;

                var peerMap = data["Peer"] as Dictionary<string, object>;
                if (peerMap == null) return peers;

                foreach (var kvp in peerMap)
                {
                    var peer = kvp.Value as Dictionary<string, object>;
                    if (peer == null) continue;

                    var online = peer.ContainsKey("Online") && Convert.ToBoolean(peer["Online"]);
                    if (!online) continue;

                    var hostname = peer.ContainsKey("HostName") ? peer["HostName"]?.ToString() : "";
                    var addrs = peer.ContainsKey("TailscaleIPs") ? peer["TailscaleIPs"] as ArrayList : null;
                    var ip = addrs?.Count > 0 ? addrs[0]?.ToString() : null;

                    if (!string.IsNullOrEmpty(ip))
                    {
                        peers.Add(new TailscalePeer { Ip = ip, Hostname = hostname });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Tailscale discovery failed: {ex.Message}");
            }

            return peers;
        }

        private class TailscalePeer
        {
            public string Ip { get; set; }
            public string Hostname { get; set; }
        }
    }
}
