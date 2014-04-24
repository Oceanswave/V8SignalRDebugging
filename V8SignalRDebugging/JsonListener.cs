namespace V8SignalRDebugging
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Base class for listening to a socket where we're communicating by sending JSON over
    /// the wire.  Usage is to subclass, set the socket, and then override ProcessPacket.
    /// </summary>
    public abstract class JsonListener
    {
        private readonly byte[] m_socketBuffer = new byte[4096];
        private Socket m_socket;

        protected void StartListenerThread()
        {
            var debuggerThread = new Thread(ListenerThread)
            {
                Name = GetType().Name + " Thread"
            };
            debuggerThread.Start();
        }

        private void ListenerThread()
        {
            var pos = 0;
            var text = new byte[0];

            // Use a local for Socket to keep nulling of _socket field (on non listener thread)
            // from causing spurious null dereferences
            var socket = m_socket;

            try
            {
                if (socket != null && socket.Connected)
                {
                    // _socket == null || !_socket.Connected effectively stops listening and associated packet processing
                    while (m_socket != null && socket.Connected)
                    {
                        if (pos >= text.Length)
                        {
                            ReadMoreData(socket.Receive(m_socketBuffer), ref text, ref pos);
                        }

                        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        while (m_socket != null && socket.Connected)
                        {
                            int newPos = text.FirstNewLine(pos);
                            if (newPos == pos)
                            {
                                // double \r\n, we're done with headers.
                                pos += 2;
                                break;
                            }

                            if (newPos == -1)
                            {
                                // we need to get more data...
                                ReadMoreData(socket.Receive(m_socketBuffer), ref text, ref pos);
                            }
                            else
                            {
                                // continue onto next header
                                // save header, continue to the next one.
                                int nameEnd = text.IndexOf((byte)':', pos, newPos - pos);
                                if (nameEnd != -1)
                                {
                                    var headerName = text.Substring(pos, nameEnd - pos);
                                    var headerNameStr = Encoding.UTF8.GetString(headerName).Trim();

                                    var headerValue = text.Substring(nameEnd + 1, newPos - nameEnd - 1);
                                    var headerValueStr = Encoding.UTF8.GetString(headerValue).Trim();
                                    headers[headerNameStr] = headerValueStr;
                                }
                                pos = newPos + 2;
                            }
                        }

                        var body = "";
                        string contentLen;
                        if (headers.TryGetValue("Content-Length", out contentLen))
                        {
                            var lengthRemaining = Int32.Parse(contentLen);
                            if (lengthRemaining != 0)
                            {
                                var bodyBuilder = new StringBuilder();

                                while (m_socket != null && socket.Connected)
                                {
                                    var len = Math.Min(text.Length - pos, lengthRemaining);
                                    bodyBuilder.Append(Encoding.UTF8.GetString(text.Substring(pos, len)));
                                    pos += len;

                                    lengthRemaining -= len;

                                    if (lengthRemaining == 0)
                                    {
                                        break;
                                    }

                                    ReadMoreData(socket.Receive(m_socketBuffer), ref text, ref pos);
                                }
                                body = bodyBuilder.ToString();
                            }
                        }

                        if (m_socket != null && socket.Connected)
                        {
                            try
                            {
                                ProcessPacket(new JsonResponse(headers, body));
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Error: {0}", e);
                            }
                        }
                    }

                }
            }
            catch (SocketException)
            {
            }
            finally
            {
                Debug.Assert(m_socket == null || !m_socket.Connected);
                if (socket != null && socket.Connected)
                {
                    socket.Disconnect(false);
                }
                OnSocketDisconnected();
            }
        }

        protected abstract void OnSocketDisconnected();
        protected abstract void ProcessPacket(JsonResponse response);

        private void ReadMoreData(int bytesRead, ref byte[] text, ref int pos)
        {
            var combinedText = new byte[bytesRead + text.Length - pos];
            Buffer.BlockCopy(text, pos, combinedText, 0, text.Length - pos);
            Buffer.BlockCopy(m_socketBuffer, 0, combinedText, text.Length - pos, bytesRead);
            text = combinedText;
            pos = 0;
        }

        protected Socket Socket
        {
            get
            {
                return m_socket;
            }
            set
            {
                m_socket = value;
            }
        }
    }

    public static class ByteExtensions
    {
        public static int IndexOf(this byte[] bytes, byte ch, int start, int count)
        {
            for (var i = start; i < start + count && i < bytes.Length; i++)
            {
                if (bytes[i] == ch)
                {
                    return i;
                }
            }
            return -1;
        }

        public static byte[] Substring(this byte[] bytes, int start, int length)
        {
            var res = new byte[length];
            for (var i = 0; i < length; i++)
            {
                res[i] = bytes[i + start];
            }
            return res;
        }

        public static int FirstNewLine(this byte[] bytes, int start)
        {
            for (var i = start; i < bytes.Length - 1; i++)
            {
                if (bytes[i] == '\r' && bytes[i + 1] == '\n')
                {
                    return i;
                }
            }
            return -1;
        }
    }

    public class JsonResponse
    {
        public readonly Dictionary<string, string> Headers;
        public readonly string Body;

        public JsonResponse(Dictionary<string, string> headers, string body)
        {
            Headers = headers;
            Body = body;
        }
    }

}
