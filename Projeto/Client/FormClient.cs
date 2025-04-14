using EI.SI;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace Client
{
    public partial class FormClient : Form
    {
        private TcpClient client;
        private NetworkStream networkStream;
        private ProtocolSI protocolSI;
        private Thread receiveThread;

        public FormClient()
        {
            InitializeComponent();
            protocolSI = new ProtocolSI();
            labelEstado.Text = "Desligado";

            textBoxIP.Text = "127.0.0.1";
            textBoxPort.Text = "10000";


        }

        // Conectar ao servidor
        private void buttonConnect_Click(object sender, EventArgs e)
        {
            try
            {
                string ip = textBoxIP.Text;
                int port = int.Parse(textBoxPort.Text);
                client = new TcpClient();
                client.Connect(IPAddress.Parse(ip), port);

                networkStream = client.GetStream();
                labelEstado.Text = "Ligado";

                // Iniciar thread para escutar mensagens
                receiveThread = new Thread(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao conectar: " + ex.Message);
            }
        }

        // Enviar mensagem para o servidor
        private void buttonSend_Click(object sender, EventArgs e)
        {
            if (client == null || !client.Connected) return;

            string msg = textBoxMessage.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            textBoxMessage.Clear();

            byte[] packet = protocolSI.Make(ProtocolSICmdType.DATA, msg);
            networkStream.Write(packet, 0, packet.Length);

            AppendMessage("[EU] " + msg);
        }




        // Receber mensagens do servidor numa thread
        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    ProtocolSI tempProtocol = new ProtocolSI();
                    int bytesRead = networkStream.Read(tempProtocol.Buffer, 0, tempProtocol.Buffer.Length);

                    if (bytesRead == 0) break; // ligação perdida

                    switch (tempProtocol.GetCmdType())
                    {
                        case ProtocolSICmdType.DATA:
                            string msg = tempProtocol.GetStringFromData();
                            AppendMessage("[Servidor] " + msg);

                            // ACK
                            byte[] ack = tempProtocol.Make(ProtocolSICmdType.ACK);
                            networkStream.Write(ack, 0, ack.Length);
                            break;

                        case ProtocolSICmdType.EOT:
                            AppendMessage("[Servidor] Ligação encerrada.");
                            return;

                        default:
                            break;
                    }
                }
            }
            catch (IOException)
            {
                AppendMessage("[Erro] Ligação foi interrompida.");
            }
            catch (Exception ex)
            {
                AppendMessage("[Erro inesperado] " + ex.Message);
            }
        }






        // Atualizar a RichTextBox de forma segura
        private void AppendMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => richTextBoxChat.AppendText(message + Environment.NewLine)));
            }
            else
            {
                richTextBoxChat.AppendText(message + Environment.NewLine);
            }
        }

        // Encerrar cliente
        private void CloseClient()
        {
            try
            {
                if (client != null && client.Connected)
                {
                    byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
                    networkStream.Write(eot, 0, eot.Length);
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                }

                receiveThread?.Abort();
                networkStream?.Close();
                client?.Close();
            }
            catch { }
        }

        // Botão sair
        private void buttonQuit_Click(object sender, EventArgs e)
        {
            CloseClient();
            Application.Exit();
        }

        // Ao fechar o formulário
        private void FormClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseClient();
        }
    }
}
