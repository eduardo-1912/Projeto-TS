using EI.SI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class ClientHandler
    {
        // CLIENTE TCP E STREAM DE REDE
        private TcpClient client;
        private NetworkStream stream;

        // NOME DO CLIENTE DEFAULT
        private string clientName = "Anónimo";

        // PROTOCOLO DE COMUNICAÇÃO
        private ProtocolSI protocol;

        // CONSTRUTOR
        public ClientHandler(TcpClient client)
        {
            this.client = client;
            this.stream = client.GetStream();
            this.protocol = new ProtocolSI();
        }


        // HANDLE CLIENT
        public void HandleClient()
        {
            try
            {
                // RECEBER O NOME DO CLIENTE
                ProtocolSI localProtocol = new ProtocolSI();
                int bytesRead = stream.Read(localProtocol.Buffer, 0, localProtocol.Buffer.Length);

                if (localProtocol.GetCmdType() == ProtocolSICmdType.USER_OPTION_1)
                {
                    // LER O NOME
                    clientName = localProtocol.GetStringFromData();

                    // ENVIAR MENSAGEM AO SERVIDOR
                    Console.WriteLine("Novo cliente ligado [" + clientName + "]");

                    // ENVIAR MENSAGEM AOS CLIENTES
                    Program.BroadcastServerMessage(clientName + " entrou no chat.");

                }

                // CICLO DE ESCUTA DO CLIENTE ATÉ ELE TERMINAR
                while (true)
                {
                    localProtocol = new ProtocolSI();
                    bytesRead = stream.Read(localProtocol.Buffer, 0, localProtocol.Buffer.Length);
                    if (bytesRead == 0) break;

                    switch (localProtocol.GetCmdType())
                    {
                        // CLIENTE ENVIOU MENSAGEM
                        case ProtocolSICmdType.DATA:
                            // MENSAGEM DE TEXTO RECEBIDA
                            string msg = localProtocol.GetStringFromData();
                            Console.WriteLine("Mensagem recebida [" + clientName + "]: " + msg);

                            // MONTAR MENSAGEM COM O NOME DO CLIENTE
                            string fullMsg = "[" + clientName + "] " + msg;
                            Program.BroadcastMessage(fullMsg, this);

                            // ENVIAR ACK PARA O CLIENTE
                            byte[] ack = localProtocol.Make(ProtocolSICmdType.ACK);
                            stream.Write(ack, 0, ack.Length);
                            break;

                        // CLIENTE TERMINOU A LIGAÇÃO
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
                // FECHAR LIGAÇÃO E REMOVER CLIENTE
                stream?.Close();
                client?.Close();
                Program.BroadcastServerMessage(clientName + " saiu do chat.");
                Program.RemoveClient(this);
            }
        }


        // ENVIA UMA MENSAGEM PARA O CLIENTE
        public void SendMessage(byte[] data)
        {
            try
            {
                stream.Write(data, 0, data.Length);
            }
            catch
            {
                throw;
            }
        }
    }
}
