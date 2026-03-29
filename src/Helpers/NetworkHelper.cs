namespace PlayniteBridge.Helpers
{
    internal static class NetworkHelper
    {
        public static string GetLocalIpAddress()
        {
            try
            {
                using (var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var ep = socket.LocalEndPoint as System.Net.IPEndPoint;
                    return ep?.Address.ToString() ?? "localhost";
                }
            }
            catch
            {
                return "localhost";
            }
        }
    }
}
