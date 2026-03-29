namespace PlayniteBridge.Server
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net;
    using System.Text;

    internal class RequestContext
    {
        public string Path { get; set; }
        public string Method { get; set; }
        public NameValueCollection QueryString { get; set; }
        public string RawBody { get; set; }

        private Dictionary<string, object> _body;
        public Dictionary<string, object> Body
        {
            get
            {
                if (_body == null && !string.IsNullOrEmpty(RawBody))
                {
                    var js = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    _body = js.Deserialize<Dictionary<string, object>>(RawBody);
                }
                return _body;
            }
            set { _body = value; }
        }

        public static RequestContext FromHttpRequest(HttpListenerRequest req)
        {
            string rawBody = null;
            if (req.HasEntityBody)
            {
                using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                    rawBody = reader.ReadToEnd();
            }

            return new RequestContext
            {
                Path = req.Url.AbsolutePath.TrimEnd('/').ToLower(),
                Method = req.HttpMethod.ToUpper(),
                QueryString = req.QueryString,
                RawBody = rawBody
            };
        }
    }
}
