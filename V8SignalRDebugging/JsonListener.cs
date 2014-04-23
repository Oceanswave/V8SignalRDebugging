/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

namespace Microsoft.NodejsTools
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
            int pos = 0;
            byte[] text = new byte[0];

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

                        Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                                    string headerNameStr = Encoding.UTF8.GetString(headerName).Trim();

                                    var headerValue = text.Substring(nameEnd + 1, newPos - nameEnd - 1);
                                    string headerValueStr = Encoding.UTF8.GetString(headerValue).Trim();
                                    headers[headerNameStr] = headerValueStr;
                                }
                                pos = newPos + 2;
                            }
                        }

                        string body = "";
                        string contentLen;
                        if (headers.TryGetValue("Content-Length", out contentLen))
                        {
                            int lengthRemaining = Int32.Parse(contentLen);
                            if (lengthRemaining != 0)
                            {
                                StringBuilder bodyBuilder = new StringBuilder();

                                while (m_socket != null && socket.Connected)
                                {
                                    int len = Math.Min(text.Length - pos, lengthRemaining);
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
            byte[] combinedText = new byte[bytesRead + text.Length - pos];
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
            for (int i = start; i < start + count && i < bytes.Length; i++)
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
            byte[] res = new byte[length];
            for (int i = 0; i < length; i++)
            {
                res[i] = bytes[i + start];
            }
            return res;
        }

        public static int FirstNewLine(this byte[] bytes, int start)
        {
            for (int i = start; i < bytes.Length - 1; i++)
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
