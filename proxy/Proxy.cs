using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace proxy
{
    public class Proxy
    {
        public const int BUFFER = 1024;
        public static IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
        public static int Port = 8888;

        public static void Start()
        {
            TcpListener tcpListener = new TcpListener(ipAddress, Port);
            tcpListener.Start();
            
            while (true)
            {
                Socket client = tcpListener.AcceptSocket();
                Thread thread = new Thread(() => Listen(client));
                thread.Start();
            }
        }

        public static void Listen(Socket client)
        {
            NetworkStream clientStream = new NetworkStream(client);
            byte[] httpRequest = Receive(clientStream);
            Response(clientStream, httpRequest);
            client.Close();
        }

        public static byte[] Receive(NetworkStream netStream)
        {
            byte[] bufData = new byte[BUFFER];
            byte[] data = new byte[BUFFER];
            int receivedBytes, dataBytes = 0;
            do
            {
                receivedBytes = netStream.Read(bufData, 0, bufData.Length);
                Array.Copy(bufData, 0, data, dataBytes, receivedBytes);
                dataBytes += receivedBytes;
            } while (netStream.DataAvailable);

            return data;
        }

        public static void Response(NetworkStream clientStream, byte[] httpRequest)
        {
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                string request = Encoding.UTF8.GetString(httpRequest);
                string host;
                IPEndPoint ipEnd = GetEndPoint(request, out host);
                string relPath = GetRelativePath(request);

                if (Program.blackList != null && Array.IndexOf(Program.blackList, host.ToLower()) != -1)
                {
                    string error = $"HTTP/1.1 403 Forbidden\r\nContent-Type: text/html\r\nContent-Length: 13\r\n\r\n403 Forbidden";
                    byte[] errorPage = Encoding.UTF8.GetBytes(error);
                    clientStream.Write(errorPage, 0, errorPage.Length);
                    Console.WriteLine(DateTime.Now + ": " + host + " 403 Forbidden");
                    return;
                }

                server.Connect(ipEnd);
                NetworkStream serverStream = new NetworkStream(server);

                byte[] relPathBytes = Encoding.UTF8.GetBytes(relPath);
                serverStream.Write(relPathBytes, 0, relPathBytes.Length);

                byte[] httpResponse = Receive(serverStream);
                clientStream.Write(httpResponse, 0, httpResponse.Length);

                OutputResponse(httpResponse, host);
                serverStream.CopyTo(clientStream);
            }
            catch
            {
                return;
            }
            finally
            {
                server.Close();
            }
        }

        public static string GetRelativePath(string message)
        {
            MatchCollection matchCollection = (new Regex(@"http:\/\/[a-z0-9а-я\.\:]*")).Matches(message);
            string host = matchCollection[0].Value;
            message = message.Replace(host, "");
            return message;
        }

        public static IPEndPoint GetEndPoint(string request, out string host)
        {
            Regex regex = new Regex(@"Host: (((?<host>.+?):(?<port>\d+?))|(?<host>.+?))\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Match match = regex.Match(request);
            host = match.Groups["host"].Value;
            int port = 0;
            if (!int.TryParse(match.Groups["port"].Value, out port))
            {
                port = 80;
            }
            IPAddress ipHost = Dns.GetHostEntry(host).AddressList[0];
            IPEndPoint ipEnd = new IPEndPoint(ipHost, port);

            return ipEnd;
        }

        public static void OutputResponse(byte[] httpResponse, string host)
        {
            string response = Encoding.UTF8.GetString(httpResponse);
            string[] bufResponse = response.Split('\r', '\n');
            string code = bufResponse[0].Substring(bufResponse[0].IndexOf(" ") + 1);

            Console.WriteLine(DateTime.Now + " " + host + " " + code);
        }
    }
}
