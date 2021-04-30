using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace proxy
{
    public class Proxy
    {
        public static int port = 8888;
        public static IPAddress ipAddress = IPAddress.Parse("192.168.100.10");

        public static void Listen()
        {
            TcpListener tcpListener = new TcpListener(ipAddress, port);
            tcpListener.Start();

            while (true)
            {
                if (tcpListener.Pending())
                {
                    Socket socket = tcpListener.AcceptSocket();
                    if (socket.Connected)
                    {
                        byte[] httpRequest = Receive(socket);
                        Send(httpRequest, socket);
                    }
                }
            }
        }

        public static byte[] Receive(Socket socket)
        {
            byte[] bufData = new byte[socket.ReceiveBufferSize];
            byte[] data = new byte[socket.ReceiveBufferSize];
            int receivedBytes = 0, index = 0;
            NetworkStream netStream = new NetworkStream(socket);
            do
            {
                receivedBytes += netStream.Read(bufData, 0, bufData.Length);
                Array.Copy(bufData, 0, data, receivedBytes, index);
                index += receivedBytes;
            } while (netStream.DataAvailable && (index < socket.ReceiveBufferSize));

            return data;
        }

        public static void Send(byte[] httpRequest, Socket client)
        {
            Regex regex = new Regex(@"Host: (((?<host>.+?):(?<port>\d+?))|(?<host>.+?))\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Match match = regex.Match(Encoding.UTF8.GetString(httpRequest));
            string host = match.Groups["host"].Value;
            //int port = 80;
            /*if (!int.TryParse(match.Groups["port"].Value, out port))
            {
                port = 80;
            }*/

            IPHostEntry ipHost = Dns.GetHostByName(host);
            IPEndPoint ipEnd = new IPEndPoint(ipHost.AddressList[0], 80);

            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Connect(ipEnd);

            if (server.Send(httpRequest, httpRequest.Length, SocketFlags.None) != httpRequest.Length)
            {
                Console.WriteLine("Error sending data to server");
            }
            else
            {
                byte[] httpResponse = Receive(server);
                client.Send(httpResponse, httpResponse.Length, SocketFlags.None);
            }
        }
    }
}
