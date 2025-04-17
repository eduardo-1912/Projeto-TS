 using EI.SI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    internal class Program
    {
        private string clientName = "Desconhecido";

        private const int PORT = 10000;
        private static TcpListener listener;
        private static List<ClientHandler> clients = new List<ClientHandler>();
        private static object lockObject = new object();

        static void Main(string[] args)
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, PORT);
            listener = new TcpListener(endpoint);

            listener.Start();
            Console.WriteLine("Servidor pronto. À escuta na porta " + PORT);

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                ClientHandler handler = new ClientHandler(client);

                lock (lockObject)
                {
                    clients.Add(handler);
                }

                Thread clientThread = new Thread(handler.HandleClient);
                clientThread.Start();
            }
        }

        // Enviar mensagem para todos os clientes, exceto quem enviou
        public static void BroadcastMessage(string message, ClientHandler sender)
        {
            byte[] data;
            ProtocolSI protocol = new ProtocolSI();
            data = protocol.Make(ProtocolSICmdType.DATA, message);

            List<ClientHandler> disconnectedClients = new List<ClientHandler>();

            lock (lockObject)
            {
                foreach (var client in clients.ToList())
                {
                    if (client != sender)
                    {
                        try
                        {
                            client.SendMessage(data);
                        }
                        catch
                        {
                            disconnectedClients.Add(client);
                        }
                    }
                }

                foreach (var client in disconnectedClients)
                {
                    clients.Remove(client);
                }
            }
        }




        public static void BroadcastServerMessage(string message)
        {
            byte[] data;
            ProtocolSI protocol = new ProtocolSI();
            data = protocol.Make(ProtocolSICmdType.DATA, "[Servidor] " + message);

            List<ClientHandler> disconnectedClients = new List<ClientHandler>();

            lock (lockObject)
            {
                foreach (var client in clients.ToList())
                {
                    try
                    {
                        client.SendMessage(data);
                    }
                    catch
                    {
                        disconnectedClients.Add(client); // Marca para remoção, sem dar erro na consola
                    }
                }

                // Remover todos os clientes falhados
                foreach (var client in disconnectedClients)
                {
                    clients.Remove(client);
                }
            }
        }





        public static void RemoveClient(ClientHandler client)
        {
            lock (lockObject)
            {
                clients.Remove(client);
            }
        }
    }

    class ClientHandler
    {
        private TcpClient client;
        private NetworkStream stream;

        private string clientName = "Anónimo";

        private ProtocolSI protocol;

        public ClientHandler(TcpClient client)
        {
            this.client = client;
            this.stream = client.GetStream();
            this.protocol = new ProtocolSI();
        }

        public void HandleClient()
        {
            try
            {
                // Primeiro pacote deve ser o nome
                ProtocolSI localProtocol = new ProtocolSI();
                int bytesRead = stream.Read(localProtocol.Buffer, 0, localProtocol.Buffer.Length);
                if (localProtocol.GetCmdType() == ProtocolSICmdType.USER_OPTION_1)
                {
                    clientName = localProtocol.GetStringFromData();
                    Console.WriteLine("Novo cliente ligado [" + clientName + "]");
                    Program.BroadcastServerMessage(clientName + " entrou no chat.");

                }

                while (true)
                {
                    localProtocol = new ProtocolSI();
                    bytesRead = stream.Read(localProtocol.Buffer, 0, localProtocol.Buffer.Length);
                    if (bytesRead == 0) break;

                    switch (localProtocol.GetCmdType())
                    {
                        case ProtocolSICmdType.DATA:
                            string msg = localProtocol.GetStringFromData();
                            Console.WriteLine("Mensagem recebida [" + clientName + "] : " + msg);

                            // Montar mensagem com nome
                            string fullMsg = "[" + clientName + "] " + msg;
                            Program.BroadcastMessage(fullMsg, this);

                            byte[] ack = localProtocol.Make(ProtocolSICmdType.ACK);
                            stream.Write(ack, 0, ack.Length);
                            break;

                        case ProtocolSICmdType.EOT:
                            Console.WriteLine(clientName + " terminou ligação.");
                            byte[] ackEot = localProtocol.Make(ProtocolSICmdType.ACK);
                            stream.Write(ackEot, 0, ackEot.Length);
                            return;
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro com cliente: " + ex.Message);
            }
            finally
            {
                stream?.Close();
                client?.Close();
                Program.BroadcastServerMessage(clientName + " saiu do chat.");
                Program.RemoveClient(this);
            }
        }


        public void SendMessage(byte[] data)
        {
            try
            {
                stream.Write(data, 0, data.Length);
            }
            catch
            {
                throw; // Lança para que seja tratado fora, na Broadcast
            }
        }





    }
}