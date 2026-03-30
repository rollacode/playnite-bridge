namespace PlayniteBridge.Services
{
    using System.IO;
    using System.Web.Script.Serialization;
    using Playnite.SDK;

    internal class SyncState
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public string ClientId { get; set; }
        public string ApiKey { get; set; }
        public string BackendUrl { get; set; }
        public string LastPushCursor { get; set; }
        public string LastPullCursor { get; set; }
        public string LastSyncTime { get; set; }
        public string LastSyncError { get; set; }

        public bool IsRegistered => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ApiKey);

        private string _filePath;

        public static SyncState Load(string pluginDataPath)
        {
            var path = Path.Combine(pluginDataPath, "sync_state.json");
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                    var state = Json.Deserialize<SyncState>(json);
                    state._filePath = path;
                    return state;
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warn($"Failed to load sync state: {ex.Message}");
            }

            return new SyncState { _filePath = path };
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_filePath)) return;
            try
            {
                var json = Json.Serialize(this);
                File.WriteAllText(_filePath, json, System.Text.Encoding.UTF8);
            }
            catch (System.Exception ex)
            {
                Logger.Warn($"Failed to save sync state: {ex.Message}");
            }
        }

        public void Clear()
        {
            ClientId = null;
            ApiKey = null;
            LastPushCursor = null;
            LastPullCursor = null;
            LastSyncTime = null;
            LastSyncError = null;
            Save();
        }
    }
}
