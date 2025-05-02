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
        // VARIÁVEIS
        private string username = "Anónimo"; // NOME DO UTILIZADOR DEFAULT (SE FICAR VAZIO)
        private TcpClient client;
        private NetworkStream networkStream;
        private ProtocolSI protocolSI;
        private Thread receiveThread;
        private bool isRunning = false; // BOOLEAN PARA SABER SE O CLIENTE ESTÁ A FUNCIONAR

        // CONSTRUTOR
        public FormClient()
        {
            InitializeComponent();

            // DESATIVAR BOTÕES E TEXTBOXES QUANDO NÃO ESTÁ CONECTADO
            ToggleConnectionUI(false);

            protocolSI = new ProtocolSI();

            
            // VALORES DEFAULT
            textBoxIP.Text = "127.0.0.1";
            textBoxPort.Text = "10000";

            AtualizarEstado("Desconectado", Color.Red);

        }


        // ATIVAR/DESATIVAR USER-INTERFACE COM BASE NA CONEXÃO
        private void ToggleConnectionUI(bool isConnected)
        {
            // QUANDO O CLIENTE NÃO ESTÁ CONECTADO --> ATIVAR TEXTBOXES DE NOME, IP E PORT E BOTÃO DE CONECTAR
            textBoxNome.Enabled = !isConnected;
            textBoxIP.Enabled = !isConnected;
            buttonConnect.Enabled = !isConnected;

            // QUANDO O CLIENTE ESTÁ CONECTADO --> ATIVAR TEXTBOX DE ENVIAR MENSAGEM E BOTÃO DE ENVIAR
            buttonSend.Enabled = isConnected;
            textBoxMessage.Enabled = isConnected;

        }


        // ATUALIZAR O ESTADO DA CONEXÃO
        private void AtualizarEstado(string estado, Color cor)
        {
            labelEstado.Text = estado;
            labelEstado.ForeColor = cor;
        }


        // CONECTAR AO SERVIDOR
        private void buttonConnect_Click(object sender, EventArgs e)
        {
            try
            {
                // OBTER O IP, PORT E USERNAME
                string ip = textBoxIP.Text;
                int port = int.Parse(textBoxPort.Text);
                username = string.IsNullOrWhiteSpace(textBoxNome.Text) ? "Anónimo" : textBoxNome.Text.Trim();

                // CRIAR A LIGAÇÃO
                client = new TcpClient();
                client.Connect(IPAddress.Parse(ip), port);
                networkStream = client.GetStream();

                // CLIENTE A FUNCIONAR
                isRunning = true;

                // ENBIAR O USERNAME AO SERVIDOR
                byte[] namePacket = protocolSI.Make(ProtocolSICmdType.USER_OPTION_1, username);
                networkStream.Write(namePacket, 0, namePacket.Length);

                // ATUALIZAR ESTADO DA CONEXÃO
                AtualizarEstado("Conectado", Color.Green);

                // INICIAR THREAD PARA RECEBER MENSAGENS
                receiveThread = new Thread(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                // ATUALIZAR A USER-INTERFACE
                ToggleConnectionUI(true);

            }
            catch (Exception ex)
            {
                // SE DEU ERRO --> MOSTRAR MENSAGEM COM O ERRO
                MessageBox.Show("Erro ao conectar: " + ex.Message);
            }
        }


        // ENVIAR MENSAGEN AO SERVIDOR
        private void buttonSend_Click(object sender, EventArgs e)
        {
            // VERIFICAR SE O CLIENTE JÁ ESTÁ CONECTADO
            if (client == null || !client.Connected) return;

            // OBTER A MENSAGEM DA TEXTBOX MENSAGEM
            string msg = textBoxMessage.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            // LIMPAR A TEXTBOX MENSAGEM
            textBoxMessage.Clear();

            // ENVIAR A MENSAGEM AO SERVIDOR
            byte[] packet = protocolSI.Make(ProtocolSICmdType.DATA, msg);
            networkStream.Write(packet, 0, packet.Length);

            // MOSTRAR A MENSAGEM NO FORM CLIENTE
            AppendMessage("[" + username + "] " + msg);

        }


        // RECEBER MENSAGENS DO SERVIDOR
        private void ReceiveMessages()
        {
            try
            {
                // LER MENSAGENS DO SERVIDOR ENQUANTO O CLIENT ESTIVER A FUNCIONAR
                while (isRunning)
                {
                    // CRIAR UM NOVO PROTOCOLO TEMPORÁRIO PARA LER A MENSAGEM
                    ProtocolSI tempProtocol = new ProtocolSI();

                    // LER A MENSAGEM DO SERVIDOR
                    int bytesRead = networkStream.Read(tempProtocol.Buffer, 0, tempProtocol.Buffer.Length);

                    // SAIR SE A LIGAÇÃO FOR PERDIDA
                    if (bytesRead == 0) break;

                    switch (tempProtocol.GetCmdType())
                    {
                        // CASO SEJA MENSAGEM DE TEXTO
                        case ProtocolSICmdType.DATA:

                            // OBTER O TEXTO DA MENSAGEM
                            string msg = tempProtocol.GetStringFromData();

                            // MOSTRAR A MENSAGEM NO CHAT
                            AppendMessage(msg);

                            // ENVIAR ACK AO SERVIDOR
                            byte[] ack = tempProtocol.Make(ProtocolSICmdType.ACK);
                            networkStream.Write(ack, 0, ack.Length);
                            break;

                        // TERMINAR A LIGAÇÃO
                        case ProtocolSICmdType.EOT:
                            // MENSAGEM DE LIGAÇÃO ENCERRADA
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
                if (isRunning) // EVITA MOSTRAR ERRO SE A THREAD ESTIVER A TERMINAR NORMALMENTE
                    AppendMessage("[Erro inesperado] " + ex.Message);
            }
        }


        // ATUALIZAR A TEXTBOX DE MENSAGENS RECEBIDAS
        private void AppendMessage(string message)
        {
            // VERIFICA SE A THREAD ATUAL NÃO É A THREAD DA USER-INTERFACE
            if (InvokeRequired)
            {
                // SE NÃO FOR, USA INVOKE PARA CHAMAR ESTE MÉTODO NA THREAD CORRETA
                Invoke(new MethodInvoker(() => AppendMessage(message)));
                return;
            }

            // COR DAS MENSAGENS
            if (message.StartsWith("[" + username + "]"))
            {
                // MENSAGENS DO UTILIZADOR --> AZUL
                richTextBoxChat.SelectionColor = Color.Blue;
            }
            else if (message.StartsWith("[Servidor]"))
            {
                // MENSAGENS DO SERVIDOR --> CINZENTO
                richTextBoxChat.SelectionColor = Color.Gray;
            }
            else
            {
                // MENSAGENS DE OUTRO UTILIZADOR --> VERMELHO
                richTextBoxChat.SelectionColor = Color.Red;
            }

            // ADICIONAR A MENSAGEM
            richTextBoxChat.AppendText(message + Environment.NewLine);

            // FAZ RESET DA COR PARA DEFAULT
            richTextBoxChat.SelectionColor = richTextBoxChat.ForeColor; 
        }


        // FECHAR O CLIENTE
        private void CloseClient()
        {
            try
            {
                // DAR A LIGAÇÃO COMO TERMINADA
                isRunning = false;

                // VERIFICAR SE O CLIENTE ESTÁ CONECTADO
                if (client != null && client.Connected)
                {
                    // ENCERRAMENTO DA LIGAÇÃO
                    byte[] eot = protocolSI.Make(ProtocolSICmdType.EOT);
                    networkStream.Write(eot, 0, eot.Length);
                    networkStream.Read(protocolSI.Buffer, 0, protocolSI.Buffer.Length);
                }

                // ESPERA A THREAD TERMINAR EM VEZ DE ABORTAR (EVITAR MENSAGEM ERRO INESPERADO NO CLIENT)
                receiveThread?.Join();

                // FECHAR A LIGAÇÃO
                networkStream?.Close();
                client?.Close();
            }
            catch { }

            // ATUALIZAR A USER-INTERFACE
            ToggleConnectionUI(false);
            AtualizarEstado("Desconectado", Color.Red);
        }


        // BOTÃO SAIR
        private void buttonQuit_Click(object sender, EventArgs e)
        {
            // CASO O CLIENTE ESTEJA CONECTADO
            if (client != null && client.Connected)
            {
                // APENAS DESCONECTA
                CloseClient();
            }
            else
            {
                // FECHA O CLIENTE
                Application.Exit();
            }
        }


        // FORM CLOSING
        private void FormClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            // FECHAR O CLIENTE
            CloseClient();
        }
    }
}
