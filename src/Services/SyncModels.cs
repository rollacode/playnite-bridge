namespace PlayniteBridge.Services
{
    using System.Collections.Generic;

    internal class SyncRegisterResponse
    {
        public string client_id { get; set; }
        public string api_key { get; set; }
    }

    internal class SyncPushResponse
    {
        public int accepted { get; set; }
        public int skipped { get; set; }
        public int conflicts { get; set; }
        public string newCursor { get; set; }
    }

    internal class SyncPullResponse
    {
        public List<Dictionary<string, object>> items { get; set; }
        public string cursor { get; set; }
        public bool hasMore { get; set; }
    }

    internal class SyncCommand
    {
        public int id { get; set; }
        public string command { get; set; }
        public string payload { get; set; }
    }

    internal class SyncConnectionResponse
    {
        public string clientId { get; set; }
        public string status { get; set; }
    }

    internal class SyncClientStatus
    {
        public string status { get; set; }
        public string apiKey { get; set; }
    }

    public class DiscoveredBackend
    {
        public string Url { get; set; }
        public string Hostname { get; set; }
        public string TailscaleIp { get; set; }
        public bool ViaTailscale { get; set; }
    }

    internal class SyncNetworkInfo
    {
        public string localIp { get; set; }
        public string tailscaleIp { get; set; }
        public string tailscaleHostname { get; set; }
        public int port { get; set; }
        public string connectUrl { get; set; }
        public bool hasTailscale { get; set; }
        public bool openRegistration { get; set; }
    }

    internal class SyncPushResponseFull
    {
        public int accepted { get; set; }
        public int skipped { get; set; }
        public int conflicts { get; set; }
        public string newCursor { get; set; }
        public List<string> missingImages { get; set; }
    }

    internal class SyncResult
    {
        public bool Success { get; set; }
        public int GamesPushed { get; set; }
        public int GamesSkipped { get; set; }
        public int GamesPulled { get; set; }
        public int GamesUpdated { get; set; }
        public int GamesCreated { get; set; }
        public string Error { get; set; }
    }
}
