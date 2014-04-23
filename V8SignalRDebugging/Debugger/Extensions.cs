﻿namespace BaristaJS.AppEngine.Debugger
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    static class Extensions
    {
        /// <summary>
        /// Reads a string from the socket which is encoded as:
        ///     U, byte count, bytes 
        ///     A, byte count, ASCII
        ///     
        /// Which supports either UTF-8 or ASCII strings.
        /// </summary>
        internal static string ReadString(this Socket socket)
        {
            byte[] cmd_buffer = new byte[4];
            if (socket.Receive(cmd_buffer, 1, SocketFlags.None) == 1)
            {
                if (cmd_buffer[0] == 'N')
                {
                    // null string
                    return null;
                }
                bool isUnicode = cmd_buffer[0] == 'U';

                if (socket.Receive(cmd_buffer) == 4)
                {
                    int filenameLen = BitConverter.ToInt32(cmd_buffer, 0);
                    byte[] buffer = new byte[filenameLen];
                    int bytesRead = 0;
                    while (bytesRead != filenameLen)
                    {
                        bytesRead += socket.Receive(buffer, bytesRead, filenameLen - bytesRead, SocketFlags.None);
                    }

                    if (isUnicode)
                    {
                        return Encoding.UTF8.GetString(buffer);
                    }
                    else
                    {
                        char[] chars = new char[buffer.Length];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            chars[i] = (char)buffer[i];
                        }
                        return new string(chars);
                    }
                }
                else
                {
                    Debug.Assert(false, "Failed to read length");
                }
            }
            else
            {
                Debug.Assert(false, "Failed to read unicode/ascii byte");
            }
            return null;
        }

        internal static int ReadInt(this Socket socket)
        {
            byte[] cmd_buffer = new byte[4];
            if (socket.Receive(cmd_buffer) == 4)
            {
                return BitConverter.ToInt32(cmd_buffer, 0);
            }
            throw new SocketException();
        }

        /// <summary>
        /// Replaces \uxxxx with the actual unicode char for a prettier display in local variables.
        /// </summary>
        public static string FixupEscapedUnicodeChars(this string text)
        {
            StringBuilder buf = null;
            int i = 0;
            int l = text.Length;
            int val;
            while (i < l)
            {
                char ch = text[i++];
                if (ch == '\\')
                {
                    if (buf == null)
                    {
                        buf = new StringBuilder(text.Length);
                        buf.Append(text, 0, i - 1);
                    }

                    if (i >= l)
                    {
                        return text;
                    }
                    ch = text[i++];

                    if (ch == 'u' || ch == 'U')
                    {
                        int len = (ch == 'u') ? 4 : 8;
                        int max = 16;
                        if (TryParseInt(text, i, len, max, out val))
                        {
                            buf.Append((char)val);
                            i += len;
                        }
                        else
                        {
                            return text;
                        }
                    }
                    else
                    {
                        buf.Append("\\");
                        buf.Append(ch);
                    }
                }
                else if (buf != null)
                {
                    buf.Append(ch);
                }
            }

            if (buf != null)
            {
                return buf.ToString();
            }
            return text;
        }


        private static bool TryParseInt(string text, int start, int length, int b, out int value)
        {
            value = 0;
            if (start + length > text.Length)
            {
                return false;
            }
            for (int i = start, end = start + length; i < end; i++)
            {
                int onechar;
                if (HexValue(text[i], out onechar) && onechar < b)
                {
                    value = value * b + onechar;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private static int HexValue(char ch)
        {
            int value;
            if (!HexValue(ch, out value))
            {
                throw new ArgumentException("bad char for integer value: " + ch);
            }
            return value;
        }

        private static bool HexValue(char ch, out int value)
        {
            switch (ch)
            {
                case '0':
                case '\x660': value = 0; break;
                case '1':
                case '\x661': value = 1; break;
                case '2':
                case '\x662': value = 2; break;
                case '3':
                case '\x663': value = 3; break;
                case '4':
                case '\x664': value = 4; break;
                case '5':
                case '\x665': value = 5; break;
                case '6':
                case '\x666': value = 6; break;
                case '7':
                case '\x667': value = 7; break;
                case '8':
                case '\x668': value = 8; break;
                case '9':
                case '\x669': value = 9; break;
                default:
                    if (ch >= 'a' && ch <= 'z')
                    {
                        value = ch - 'a' + 10;
                    }
                    else if (ch >= 'A' && ch <= 'Z')
                    {
                        value = ch - 'A' + 10;
                    }
                    else
                    {
                        value = -1;
                        return false;
                    }
                    break;
            }
            return true;
        }

        internal static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout, CancellationToken token = default (CancellationToken))
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationTokenSource.Token);

            await Task.WhenAny(new[] {
                task,
                Task.Delay(timeout, linkedTokenSource.Token)
            }).ConfigureAwait(false);

            linkedTokenSource.Cancel();

            if (task.IsCompleted)
            {
                return task.Result;
            }

            throw new TimeoutException();
        }

        internal static async Task<string> ReadLineBlockAsync(this StreamReader streamReader, int length)
        {
            var buffer = new char[length];

            int count = await streamReader.ReadBlockAsync(buffer, 0, length).ConfigureAwait(false);
            if (count == 0)
            {
                var errorMessage = string.Format("Unable to read {0} bytes from stream.", length);
                throw new InvalidDataException(errorMessage);
            }

            // Get UTF-8 string
            byte[] bytes = streamReader.CurrentEncoding.GetBytes(buffer, 0, count);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
