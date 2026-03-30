namespace PlayniteBridge.Services
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Web.Script.Serialization;
    using Playnite.SDK;

    internal class SyncClient
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private string _baseUrl;
        private string _apiKey;

        public SyncClient(string baseUrl, string apiKey)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _apiKey = apiKey ?? "";
        }

        public void UpdateCredentials(string baseUrl, string apiKey)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _apiKey = apiKey ?? "";
        }

        public SyncRegisterResponse Register(string clientName, string playniteVersion, string registrationCode = null)
        {
            var body = _json.Serialize(new { name = clientName, playniteVersion, registrationCode });
            var result = Post("/api/clients/register", body);
            return result != null ? _json.Deserialize<SyncRegisterResponse>(result) : null;
        }

        /// <summary>Request connection (no auth, creates pending client).</summary>
        public SyncConnectionResponse RequestConnection(string clientName, string playniteVersion)
        {
            var body = _json.Serialize(new { name = clientName, playniteVersion });
            var result = PostPublic("/api/clients/request", body);
            return result != null ? _json.Deserialize<SyncConnectionResponse>(result) : null;
        }

        /// <summary>Check connection status (no auth, by client ID).</summary>
        public SyncClientStatus PollStatus(string clientId)
        {
            try
            {
                var response = Http.GetAsync($"{_baseUrl}/api/clients/{clientId}/status").GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode) return null;
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return _json.Deserialize<SyncClientStatus>(body);
            }
            catch { return null; }
        }

        /// <summary>Approve with registration code (from frontend).</summary>
        public SyncClientStatus ApproveWithCode(string clientId, string registrationCode)
        {
            var body = _json.Serialize(new { registrationCode });
            var result = PostPublic($"/api/clients/{clientId}/approve", body);
            return result != null ? _json.Deserialize<SyncClientStatus>(result) : null;
        }

        public SyncPushResponse Push(string entityType, List<object> items, List<object> removals = null)
        {
            var payload = new Dictionary<string, object>
            {
                ["entityType"] = entityType,
                ["items"] = items,
                ["removals"] = removals ?? new List<object>()
            };
            var result = Post("/api/sync/push", _json.Serialize(payload));
            return result != null ? _json.Deserialize<SyncPushResponse>(result) : null;
        }

        public SyncPullResponse Pull(string entityType, string sinceCursor, int limit = 500)
        {
            var since = Uri.EscapeDataString(sinceCursor ?? "1970-01-01T00:00:00Z");
            var result = Get($"/api/sync/pull?entity_type={entityType}&since={since}&limit={limit}");
            return result != null ? _json.Deserialize<SyncPullResponse>(result) : null;
        }

        public List<SyncCommand> GetCommands()
        {
            var result = Get("/api/sync/commands");
            if (result == null) return new List<SyncCommand>();
            // JavaScriptSerializer returns ArrayList, need to convert
            var raw = _json.Deserialize<ArrayList>(result);
            var cmds = new List<SyncCommand>();
            if (raw == null) return cmds;
            foreach (Dictionary<string, object> item in raw)
            {
                cmds.Add(new SyncCommand
                {
                    id = Convert.ToInt32(item["id"]),
                    command = item["command"]?.ToString(),
                    payload = item.ContainsKey("payload") ? item["payload"]?.ToString() : null
                });
            }
            return cmds;
        }

        public bool AckCommand(int commandId)
        {
            var result = Post($"/api/sync/commands/{commandId}/ack", "{}");
            return result != null;
        }

        public SyncNetworkInfo GetNetworkInfo()
        {
            // Network endpoint is public (no auth)
            try
            {
                var url = $"{_baseUrl}/api/network";
                var response = Http.GetAsync(url).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode) return null;
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return _json.Deserialize<SyncNetworkInfo>(body);
            }
            catch { return null; }
        }

        public bool IsReachable()
        {
            try
            {
                var url = $"{_baseUrl}/api/network";
                var response = Http.GetAsync(url).GetAwaiter().GetResult();
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private string Get(string path)
        {
            return WithRetry(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}{path}");
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                var response = Http.SendAsync(request).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            });
        }

        private string PostPublic(string path, string jsonBody)
        {
            return WithRetry(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{path}");
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var response = Http.SendAsync(request).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            });
        }

        private string Post(string path, string jsonBody)
        {
            return WithRetry(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{path}");
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var response = Http.SendAsync(request).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            });
        }

        private T WithRetry<T>(Func<T> action, int maxRetries = 3) where T : class
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try { return action(); }
                catch (HttpRequestException ex)
                {
                    Logger.Warn($"Sync HTTP error (attempt {i + 1}/{maxRetries}): {ex.Message}");
                    if (i == maxRetries - 1) return null;
                    Thread.Sleep(1000 * (1 << i)); // 1s, 2s, 4s
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Sync error: {ex.Message}");
                    return null;
                }
            }
            return null;
        }
    }
}
