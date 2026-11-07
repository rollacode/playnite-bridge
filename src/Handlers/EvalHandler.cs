namespace PlayniteBridge.Handlers
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using Playnite.SDK;
    using Playnite.SDK.Plugins;
    using PlayniteBridge.Helpers;
    using PlayniteBridge.Server;
    using PlayniteBridge.Services;

    internal class EvalHandler
    {
        private readonly IPlayniteAPI _api;
        private readonly GenericPlugin _plugin;
        private readonly EvalService _evalService;
        private readonly JsonHelper _jsonHelper;
        private readonly GameSerializationService _serializer;

        public EvalHandler(IPlayniteAPI api, GenericPlugin plugin, EvalService evalService, JsonHelper jsonHelper, GameSerializationService serializer)
        {
            _api = api;
            _plugin = plugin;
            _evalService = evalService;
            _jsonHelper = jsonHelper;
            _serializer = serializer;
        }

        private static bool TryReadTimeout(object value, out int timeoutMs)
        {
            timeoutMs = 0;
            if (value == null) return false;

            switch (value)
            {
                case int i: timeoutMs = i; return true;
                case long l when l >= int.MinValue && l <= int.MaxValue: timeoutMs = (int)l; return true;
                case double d when d >= int.MinValue && d <= int.MaxValue: timeoutMs = (int)d; return true;
                case decimal m when m >= int.MinValue && m <= int.MaxValue: timeoutMs = (int)m; return true;
                case string s:
                    return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out timeoutMs);
                default:
                    return false;
            }
        }

        public object HandleEval(RequestContext ctx)
        {
            var body = ctx.Body;

            if (!string.IsNullOrEmpty(ctx.BodyError))
                return new { error = ctx.BodyError, code = 400 };

            if (body == null)
                return new { error = "Missing 'code' field", code = 400 };

            var code = body.GetValueOrNull("code") as string;
            if (string.IsNullOrWhiteSpace(code))
                return new { error = "Missing 'code' field", code = 400 };

            var timeoutMs = 10000;
            object rawTimeout;
            if (body.TryGetValue("timeoutMs", out rawTimeout) && rawTimeout != null)
            {
                int requested;
                if (!TryReadTimeout(rawTimeout, out requested))
                    return new
                    {
                        error = "Invalid 'timeoutMs' value: expected an integer between 1000 and 30000",
                        code = 400,
                    };

                timeoutMs = Math.Max(1000, Math.Min(requested, 30000));
            }

            var onUiThread = body.ContainsKey("onUiThread") && body["onUiThread"] is bool ui && ui;

            var isExpression = _evalService.IsExpression(code);
            var source = _evalService.GenerateSource(code, isExpression);

            var compileResult = _evalService.Compile(source);
            if (!compileResult.Success)
            {
                return new
                {
                    success = false,
                    error = compileResult.Error,
                    errors = compileResult.CompilationErrors,
                    errorsDetailed = compileResult.CompilationErrorDetails,
                };
            }

            var method = (MethodInfo)compileResult.Result;
            var args = new object[] { _api, _plugin };

            Action<Action> uiDispatcher = null;
            if (onUiThread)
            {
                uiDispatcher = action => _api.MainView.UIDispatcher.Invoke(action,
                    TimeSpan.FromMilliseconds(timeoutMs),
                    System.Windows.Threading.DispatcherPriority.Normal);
            }

            var execResult = _evalService.Execute(method, args, timeoutMs, onUiThread, uiDispatcher);

            if (!execResult.Success)
            {
                return new
                {
                    success = false,
                    error = execResult.Error,
                    exceptionType = execResult.ExceptionType,
                    durationMs = execResult.DurationMs
                };
            }

            var serialized = _jsonHelper.SafeSerializeResult(
                execResult.Result,
                g => _serializer.SerializeFull(g),
                g => _serializer.SerializeCompact(g));

            return new
            {
                success = true,
                result = serialized,
                resultType = execResult.ResultType,
                durationMs = execResult.DurationMs
            };
        }
    }
}
