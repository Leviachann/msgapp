using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace ClientApp
{
    public partial class MainWindow : Window
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private string userName;

        public MainWindow()
        {
            InitializeComponent();
            InitializeClient();
        }

        private void InitializeClient()
        {
            tcpClient = new TcpClient();
            tcpClient.Connect("192.168.0.108", 12345);
            stream = tcpClient.GetStream();

            // Get user's name
            userName = PromptForName();
            if (!string.IsNullOrEmpty(userName))
            {
                dynamic userJson = new { Name = userName };
                string userJsonString = Newtonsoft.Json.JsonConvert.SerializeObject(userJson);
                byte[] data = Encoding.UTF8.GetBytes(userJsonString);
                stream.Write(data, 0, data.Length);

                messageBox.IsEnabled = true;
                sendButton.IsEnabled = true;
            }
        }

        private string PromptForName()
        {
            return Microsoft.VisualBasic.Interaction.InputBox("Enter your name:", "User Name", "");
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = messageBox.Text;
            byte[] data = Encoding.UTF8.GetBytes(message);
            stream.Write(data, 0, data.Length);
        }
    }
}
