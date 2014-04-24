namespace V8SignalRDebugging.Debugger
{
    using System.IO;
    using System.Net.Sockets;

    public sealed class TcpNetworkClient : INetworkClient
    {
        private readonly TcpClient m_tcpClient;

        public TcpNetworkClient(string hostName, int portNumber)
        {
            m_tcpClient = new TcpClient(hostName, portNumber);
        }

        public bool Connected
        {
            get
            {
                return m_tcpClient.Connected;
            }
        }

        public Stream GetStream()
        {
            return m_tcpClient.GetStream();
        }

        public void Dispose()
        {
            m_tcpClient.Close();
        }
    }
}