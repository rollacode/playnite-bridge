namespace PlayniteBridge.Handlers
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Web.Script.Serialization;
    using PlayniteBridge.Helpers;

    internal class AuthHandler
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private readonly Func<string> _getPluginDataPath;
        private readonly Func<bool> _getIsNetworkBound;
        private readonly Func<string> _generateSkillMd;

        private string _apiToken;

        public string ApiToken => _apiToken;

        public AuthHandler(Func<string> getPluginDataPath, Func<bool> getIsNetworkBound, Func<string> generateSkillMd)
        {
            _getPluginDataPath = getPluginDataPath;
            _getIsNetworkBound = getIsNetworkBound;
            _generateSkillMd = generateSkillMd;
        }

        public void LoadOrCreateToken()
        {
            var dir = _getPluginDataPath();
            Directory.CreateDirectory(dir);
            var authPath = Path.Combine(dir, "auth.json");

            if (File.Exists(authPath))
            {
                try
                {
                    var data = _json.Deserialize<System.Collections.Generic.Dictionary<string, string>>(File.ReadAllText(authPath));
                    if (data != null && data.ContainsKey("token") && !string.IsNullOrEmpty(data["token"]))
                    {
                        _apiToken = data["token"];
                        return;
                    }
                }
                catch { }
            }
            RotateToken();
        }

        public object HandleRotateToken()
        {
            RotateToken();
            return new { ok = true, token = _apiToken };
        }

        public object HandleGetSkillMd()
        {
            return new { content = _generateSkillMd() };
        }

        public void RotateToken()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[24];
                rng.GetBytes(bytes);
                _apiToken = "pb_" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }

            var dir = _getPluginDataPath();
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "auth.json"),
                _json.Serialize(new { token = _apiToken, created = DateTime.UtcNow.ToString("o") }));
        }

        public bool ValidateAuth(string authHeader)
        {
            return !string.IsNullOrEmpty(authHeader)
                && authHeader.StartsWith("Bearer ")
                && authHeader.Substring(7).Trim() == _apiToken;
        }
    }
}
