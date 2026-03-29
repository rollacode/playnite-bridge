namespace PlayniteBridge.Server
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Web.Script.Serialization;
    using Playnite.SDK;
    using PlayniteBridge.Handlers;

    internal class HttpApiServer
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private HttpListener _httpListener;
        private Thread _serverThread;
        private readonly int _port;
        private readonly AuthHandler _auth;
        private readonly Router _router;

        public bool IsNetworkBound { get; private set; }

        public HttpApiServer(int port, AuthHandler auth, Router router)
        {
            _port = port;
            _auth = auth;
            _router = router;
        }

        public void Start()
        {
            string[] prefixes = new[]
            {
                $"http://+:{_port}/",
                $"http://*:{_port}/",
                $"http://localhost:{_port}/"
            };

            IsNetworkBound = false;

            foreach (var prefix in prefixes)
            {
                try
                {
                    _httpListener = new HttpListener();
                    _httpListener.Prefixes.Add(prefix);
                    _httpListener.Start();
                    _serverThread = new Thread(ServerLoop) { IsBackground = true };
                    _serverThread.Start();
                    IsNetworkBound = prefix.Contains("+") || prefix.Contains("*");
                    Logger.Info($"HTTP server listening on {prefix}");
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to bind {prefix}: {ex.Message}");
                    try { _httpListener?.Close(); } catch { }
                }
            }

            Logger.Error("Failed to start HTTP server on any prefix");
        }

        public void Stop()
        {
            try { _httpListener?.Stop(); _httpListener?.Close(); } catch { }
        }

        public void Restart()
        {
            Stop();
            Start();
        }

        private void ServerLoop()
        {
            while (_httpListener?.IsListening == true)
            {
                try
                {
                    var ctx = _httpListener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex) { Logger.Error(ex, "HTTP server error"); }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            res.ContentType = "application/json; charset=utf-8";
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Headers", "Authorization, Content-Type");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.OutputStream.Close();
                return;
            }

            try
            {
                // Auth check
                var authHeader = req.Headers["Authorization"];
                if (!_auth.ValidateAuth(authHeader))
                {
                    res.StatusCode = 401;
                    WriteJson(res, new { error = "Unauthorized. Header required: Authorization: Bearer <token>" });
                    return;
                }

                var requestContext = RequestContext.FromHttpRequest(req);
                var result = _router.Route(requestContext);

                if (result == null)
                {
                    res.StatusCode = 404;
                    result = new { error = "Not found", help = "GET /api for endpoint list" };
                }

                WriteJson(res, result);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"HTTP request error: {req.RawUrl}");
                res.StatusCode = 500;
                WriteJson(res, new { error = ex.Message });
            }
            finally
            {
                try { res.OutputStream.Close(); } catch { }
            }
        }

        private void WriteJson(HttpListenerResponse res, object obj)
        {
            var json = _json.Serialize(obj);
            var buffer = Encoding.UTF8.GetBytes(json);
            res.ContentLength64 = buffer.Length;
            res.OutputStream.Write(buffer, 0, buffer.Length);
        }
    }
}
