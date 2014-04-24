namespace V8SignalRDebugging.Debugger
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;

    public sealed class WebSocketNetworkClient : INetworkClient
    {
        private readonly ClientWebSocket m_webSocket;
        private readonly WebSocketStream m_stream;

        public WebSocketNetworkClient(Uri uri)
        {
            try
            {
                var httpRequest = WebRequest.Create(new UriBuilder(uri) { Scheme = "http", Port = -1, Path = "/" }.Uri);
                httpRequest.Method = WebRequestMethods.Http.Head;
                httpRequest.Timeout = 5000;
                httpRequest.GetResponse().Dispose();
            }
            catch (WebException)
            {
                // If it fails or times out, just go ahead and try to connect anyway, and rely on normal error reporting path.
            }

            m_webSocket = new ClientWebSocket();
            m_webSocket.ConnectAsync(uri, CancellationToken.None).GetAwaiter().GetResult();
            m_stream = new WebSocketStream(m_webSocket);
        }

        public bool Connected
        {
            get { return m_webSocket.State == WebSocketState.Open; }
        }

        public void Dispose()
        {
            try
            {
                m_webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait();
                m_webSocket.Dispose();
            }
            catch (WebSocketException)
            {
                // We don't care about any errors when cleaning up and closing connection.
            }
        }

        public Stream GetStream()
        {
            return m_stream;
        }
    }
}