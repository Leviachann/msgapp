using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;

namespace ServerApp
{
    public partial class MainWindow : Window
    {
        private TcpListener tcpListener;
        private Thread listenThread;
        private ConcurrentDictionary<string, TcpClient> clients = new ConcurrentDictionary<string, TcpClient>();

        public MainWindow()
        {
            InitializeComponent();
            StartListening();
        }

        private void StartListening()
        {
            tcpListener = new TcpListener(IPAddress.Any, 12345);
            tcpListener.Start();

            listenThread = new Thread(() =>
            {
                while (true)
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.Start();
                }
            });

            listenThread.Start();
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead;

                // Read the user name
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (!string.IsNullOrEmpty(data))
                {
                    dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(data);
                    string userName = json.Name;
                    clients.TryAdd(userName, client);

                    Dispatcher.Invoke(() =>
                    {
                        userList.Items.Add(userName);
                    });

                    // Start a separate thread to monitor client status
                    Thread statusThread = new Thread(() => MonitorClientStatus(client, userName));
                    statusThread.Start();

                    while (true)
                    {
                        try
                        {
                            bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead == 0)
                            {
                                // Client disconnected
                                clients.TryRemove(userName, out _);
                                Dispatcher.Invoke(() =>
                                {
                                    UpdateUserStatus(userName, false); // Mark user as offline
                                });
                                break;
                            }

                            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            Dispatcher.Invoke(() =>
                            {
                                receivedMessages.AppendText($"{userName}: {message}\n");
                            });
                        }
                        catch (InvalidOperationException)
                        {
                            // Handle non-connected socket exception
                            clients.TryRemove(userName, out _);
                            Dispatcher.Invoke(() =>
                            {
                                UpdateUserStatus(userName, false); // Mark user as offline
                            });
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle other exceptions
            }
        }

        private void MonitorClientStatus(TcpClient client, string userName)
        {
            while (true)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] heartbeat = Encoding.UTF8.GetBytes("heartbeat");
                    stream.Write(heartbeat, 0, heartbeat.Length);

                    // Wait for a short interval before sending the next heartbeat
                    Thread.Sleep(5000); // Adjust the interval as needed
                }
                catch (Exception)
                {
                    // Client is disconnected, handle accordingly
                    clients.TryRemove(userName, out _);
                    Dispatcher.Invoke(() =>
                    {
                        UpdateUserStatus(userName, false); // Mark user as offline
                    });
                    break;
                }
            }
        }

        private void UpdateUserStatus(string userName, bool isOnline)
        {
            foreach (var item in userList.Items)
            {
                if (item.ToString() == userName)
                {
                    int index = userList.Items.IndexOf(item);
                    if (isOnline)
                    {
                        userList.Items[index] = userName;
                    }
                    else
                    {
                        userList.Items[index] = $"[Offline] {userName}";
                    }
                    // Change color of user name (you can use a DataTemplate for more styling)
                    userList.ItemContainerGenerator.ContainerFromIndex(index);
                    break;
                }
            }
        }

        private bool IsUserOnline(string userName)
        {
            return clients.ContainsKey(userName);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedUser = userList.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedUser) && clients.TryGetValue(selectedUser, out TcpClient client))
            {
                bool isUserOnline = IsUserOnline(selectedUser);
                if (isUserOnline)
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        string message = messageBox.Text;
                        byte[] buffer = Encoding.UTF8.GetBytes(message);
                        stream.Write(buffer, 0, buffer.Length);

                        Dispatcher.Invoke(() =>
                        {
                            receivedMessages.AppendText($"You: {message}\n");
                            UpdateUserStatus(selectedUser, true);
                        });
                    }
                    catch (Exception)
                    {
                        // Client is disconnected, handle accordingly
                        clients.TryRemove(selectedUser, out _);
                        Dispatcher.Invoke(() =>
                        {
                            UpdateUserStatus(selectedUser, false); // Mark user as offline
                        });
                    }
                }
                else
                {
                    // User is offline, handle accordingly
                    MessageBox.Show($"{selectedUser} is currently offline. Your message could not be sent.");
                }
            }
        }
    }
}
