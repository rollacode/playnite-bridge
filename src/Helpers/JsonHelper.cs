namespace PlayniteBridge.Helpers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Web.Script.Serialization;
    using Playnite.SDK.Models;

    internal class JsonHelper
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public string Serialize(object obj) => _json.Serialize(obj);

        public T Deserialize<T>(string json) => _json.Deserialize<T>(json);

        public Dictionary<string, object> ParseBody(string rawBody)
        {
            return _json.Deserialize<Dictionary<string, object>>(rawBody);
        }

        public string ReadRequestBody(System.Net.HttpListenerRequest req)
        {
            using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        public object SafeSerializeResult(object result, Func<Game, object> serializeFull, Func<Game, object> serializeCompact)
        {
            if (result == null) return null;
            if (result is string || result is bool || result is int || result is long
                || result is double || result is float || result is decimal || result is Guid)
                return result;

            if (result is Game g) return serializeFull(g);

            if (result is IEnumerable<Game> games)
                return games.Select(x => serializeCompact(x)).ToList();

            if (result is DatabaseObject dbo)
                return new { id = dbo.Id.ToString(), name = dbo.Name };

            if (result is IEnumerable enumerable && !(result is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                    list.Add(SafeSerializeResult(item, serializeFull, serializeCompact));
                return list;
            }

            try { _json.Serialize(result); return result; }
            catch { return result.ToString(); }
        }
    }
}
