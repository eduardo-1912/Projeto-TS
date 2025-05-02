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
        // PORT DE ESCUTA DO SERVIDOR
        private const int PORT = 10000;

        // RESPONSÁVEL POR ACEITAR CONEXÕES TCP
        private static TcpListener listener;

        // LISTA DE CLIENTES CONECTADOS
        private static List<ClientHandler> clients = new List<ClientHandler>();

        // OBJETO DE BLOQUEIO PARA ACESSO À LISTA DE CLIENTES
        private static object lockObject = new object();

        // MÉTODO MAIN
        static void Main(string[] args)
        {
            // DEFINIÇÃO DO ENDPOINT (IP + PORT)
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, PORT);

            // CRIAÇÃO DO LISTENER TCP
            listener = new TcpListener(endpoint);

            // INICIAR A ESCUTA DE CONEXÕES
            listener.Start();
            Console.WriteLine("Servidor pronto. À escuta na porta " + PORT);

            // CICLO INFINITO PARA ACEITAR CLIENTES
            while (true)
            {
                // ACEITAR NOVO CLIENTE
                TcpClient client = listener.AcceptTcpClient();

                // CRIAR HANDLER PARA O CLIENTE
                ClientHandler handler = new ClientHandler(client);

                // ADICIONAR O CLIENTE À LISTA COM LOCK (SEGURANÇA DE THREADS)
                lock (lockObject)
                {
                    clients.Add(handler);
                }

                // CRIAR THREAD PARA GERIR A COMUNICAÇÃO DO CLIENTE
                Thread clientThread = new Thread(handler.HandleClient);
                clientThread.Start();
            }
        }


        // ENVIA MENSAGEM PARA TODOS OS CLIENTES
        public static void BroadcastMessage(string message, ClientHandler sender)
        {
            // CRIAÇÃO DO PACOTE DE DADOS
            byte[] data;
            ProtocolSI protocol = new ProtocolSI();
            data = protocol.Make(ProtocolSICmdType.DATA, message);

            // LISTA DE CLIENTES A REMOVER
            List<ClientHandler> disconnectedClients = new List<ClientHandler>();

            // ACESSO SEGURO À LISTA DE CLIENTES
            lock (lockObject)
            {
                // PERCORRER TODOS OS CLIENTES CONECTADOS
                foreach (var client in clients.ToList())
                {
                    // NÃO ENVIAR DE VOLTA AO CLIENTE QUE ENVIOU
                    if (client != sender)
                    {
                        try
                        {
                            // ENVIAR A MENSAGEM
                            client.SendMessage(data);
                        }
                        catch
                        {
                            // EM CASO DE ERRO, MARCAR PARA REMOVER
                            disconnectedClients.Add(client);
                        }
                    }
                }

                // REMOVER OS CLIENTES DESCONECTADOS
                foreach (var client in disconnectedClients)
                {
                    clients.Remove(client);
                }
            }
        }


        // ENVIAR MENSAGEM DO SERVIDOR PARA TODOS OS CLIENTES
        public static void BroadcastServerMessage(string message)
        {
            // CRIAÇÃO DO PACOTE DE DADOS
            byte[] data;
            ProtocolSI protocol = new ProtocolSI();
            data = protocol.Make(ProtocolSICmdType.DATA, "[Servidor] " + message);

            // LISTA DE CLIENTES A REMOVER
            List<ClientHandler> disconnectedClients = new List<ClientHandler>();

            // ACESSO SEGURO À LISTA DE CLIENTES
            lock (lockObject)
            {
                // PERCORRER TODOS OS CLIENTES CONECTADOS
                foreach (var client in clients.ToList())
                {
                    try
                    {
                        // ENVIAR A MENSAGEM
                        client.SendMessage(data);
                    }
                    catch
                    {
                        // EM CASO DE ERRO, MARCAR PARA REMOVER
                        disconnectedClients.Add(client); // Marca para remoção, sem dar erro na consola
                    }
                }

                // REMOVER CLIENTES DESCONECTADOS
                foreach (var client in disconnectedClients)
                {
                    clients.Remove(client);
                }
            }
        }


        // REMOVER CLIENTE DA LISTA
        public static void RemoveClient(ClientHandler client)
        {
            // ACESSO SEGURO À LISTA DE CLIENTES
            lock (lockObject)
            {
                clients.Remove(client);
            }
        }
    }
}