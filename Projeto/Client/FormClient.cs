using EI.SI;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Client
{
    public partial class FormClient : Form
    {
        private string username = "Anónimo";
        private TcpClient client;
        private NetworkStream networkStream;
        private ProtocolSI protocolSI;
        private Thread receiveThread;
        private bool isRunning = false;


        public FormClient()
        {
            InitializeComponent();

            ToggleConnectionUI(false);

            protocolSI = new ProtocolSI();
            AtualizarEstado("Desconectado", Color.Red);
            textBoxIP.Text = "127.0.0.1";
            textBoxPort.Text = "10000";


        }

        private void ToggleConnectionUI(bool isConnected)
        {
            textBoxIP.Enabled = !isConnected;
            textBoxPort.Enabled = !isConnected;
            textBoxNome.Enabled = !isConnected;
            buttonConnect.Enabled = !isConnected;

            buttonSend.Enabled = isConnected;
            textBoxMessage.Enabled = isConnected;

        }

        private void AtualizarEstado(string estado, Color cor)
        {
            labelEstado.Text = estado;
            labelEstado.ForeColor = cor;
        }


        // Conectar ao servidor
        private void buttonConnect_Click(object sender, EventArgs e)
        {
            try
            {
                string ip = textBoxIP.Text;
                int port = int.Parse(textBoxPort.Text);
                username = string.IsNullOrWhiteSpace(textBoxNome.Text) ? "Anónimo" : textBoxNome.Text.Trim();
                client = new TcpClient();
                client.Connect(IPAddress.Parse(ip), port);

                networkStream = client.GetStream();

                isRunning = true;


                // Enviar nome ao servidor
                byte[] namePacket = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, username);
                networkStream.Write(namePacket, 0, namePacket.Length);

                AtualizarEstado("Conectado", Color.Green);

                // Iniciar thread para escutar mensagens
                receiveThread = new Thread(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                ToggleConnectionUI(true);

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

            AppendMessage("[" + username + "] " + msg);

        }




        // Receber mensagens do servidor numa thread
        private void ReceiveMessages()
        {
            try
            {
                while (isRunning)
                {
                    ProtocolSI tempProtocol = new ProtocolSI();
                    int bytesRead = networkStream.Read(tempProtocol.Buffer, 0, tempProtocol.Buffer.Length);

                    if (bytesRead == 0) break; // ligação perdida

                    switch (tempProtocol.GetCmdType())
                    {
                        case ProtocolSICmdType.DATA:
                            string msg = tempProtocol.GetStringFromData();
                            AppendMessage(msg);

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
                if (isRunning) // Evita mostrar erro se a thread estiver a terminar naturalmente
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
                isRunning = false;

                if (client != null && client.Connected)
                {
                    byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
                    networkStream.Write(eot, 0, eot.Length);
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                }

                receiveThread?.Join(); // Espera a thread terminar em vez de abortar
                networkStream?.Close();
                client?.Close();
            }
            catch { }

            ToggleConnectionUI(false);
            AtualizarEstado("Desconectado", Color.Red);



        }

        // Botão sair
        private void buttonQuit_Click(object sender, EventArgs e)
        {
            if (client != null && client.Connected)
            {
                CloseClient(); // Apenas desconecta
            }
            else
            {
                Application.Exit(); // Fecha o programa completamente
            }
        }


        // Ao fechar o formulário
        private void FormClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseClient();
        }
    }
}
