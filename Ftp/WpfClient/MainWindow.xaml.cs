using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Common;
using Microsoft.Win32;
using Newtonsoft.Json;
using Client;

namespace WpfClient
{
    public partial class MainWindow : Window
    {
        private int currentId = -1;
        private string currentPath = "";
        private Stack<string> pathHistory = new Stack<string>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void UpdateStatus(string message)
        {
            txtStatus.Text = message;
        }

        private void UpdatePathDisplay()
        {
            txtCurrentPath.Text = $"Текущий путь: {currentPath}";
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IPAddress.TryParse(txtIp.Text, out IPAddress ip) || !int.TryParse(txtPort.Text, out int port))
                {
                    MessageBox.Show("Неверный IP адрес или порт");
                    return;
                }

                if (string.IsNullOrEmpty(txtLogin.Text) || string.IsNullOrEmpty(txtPassword.Password))
                {
                    MessageBox.Show("Введите логин и пароль");
                    return;
                }

                string message = $"connect {txtLogin.Text} {txtPassword.Password}";
                string response = await SendCommandToServer(message);

                if (response.StartsWith("autorization:"))
                {
                    currentId = int.Parse(response.Split(':')[1]);
                    currentPath = "/";
                    pathHistory.Clear();
                    pathHistory.Push(currentPath);

                    btnConnect.IsEnabled = false;
                    btnDisconnect.IsEnabled = true;
                    txtIp.IsEnabled = false;
                    txtPort.IsEnabled = false;
                    txtLogin.IsEnabled = false;
                    txtPassword.IsEnabled = false;

                    UpdateStatus("Подключено успешно");
                    await RefreshFileList();
                }
                else
                {
                    MessageBox.Show("Ошибка авторизации: " + response);
                    UpdateStatus("Ошибка авторизации");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
                UpdateStatus("Ошибка подключения");
            }
        }

        private async void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            currentId = -1;
            currentPath = "";
            pathHistory.Clear();
            lstFiles.Items.Clear();

            btnConnect.IsEnabled = true;
            btnDisconnect.IsEnabled = false;
            txtIp.IsEnabled = true;
            txtPort.IsEnabled = true;
            txtLogin.IsEnabled = true;
            txtPassword.IsEnabled = true;

            UpdateStatus("Отключено");
        }

        private async void lstFiles_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedItem = lstFiles.SelectedItem as FileItem;
            if (selectedItem != null && selectedItem.IsDirectory)
            {
                await ChangeDirectory(selectedItem.Name);
            }
        }

        private async System.Threading.Tasks.Task ChangeDirectory(string folderName)
        {
            try
            {
                string command;
                if (folderName == "..")
                {
                    command = "cd ..";
                }
                else
                {
                    var selectedItem = lstFiles.SelectedItem as FileItem;
                    if (selectedItem == null) return;

                    command = $"cd {selectedItem.Name}";
                }

                string response = await SendCommandToServer(command);

                if (response.StartsWith("cd:"))
                {
                    await ParseAndDisplayFiles(response);
                    UpdateStatus("Директория изменена успешно");
                }
                else if (response.StartsWith("message:"))
                {
                    string errorMessage = response.Substring(8);
                    if (errorMessage.Contains("корневой директории") && folderName == "..")
                    {
                        await RefreshFileList();
                        UpdateStatus("Вы в корневой директории");
                    }
                    else
                    {
                        MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Неизвестный ответ от сервера: " + response, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async System.Threading.Tasks.Task UpdateCurrentPath()
        {
            try
            {
                string response = await SendCommandToServer("cd");
                if (response.StartsWith("cd:"))
                {
                    UpdateStatus("Директория изменена");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обновления пути: {ex.Message}");
            }
        }
        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = lstFiles.SelectedItem as FileItem;
            if (selectedItem == null || selectedItem.IsDirectory)
            {
                MessageBox.Show("Выберите файл для скачивания", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var saveDialog = new SaveFileDialog();
                saveDialog.FileName = selectedItem.Name;
                saveDialog.Filter = "Все файлы (*.*)|*.*";

                if (saveDialog.ShowDialog() == true)
                {
                    string command = $"get {selectedItem.Name}";
                    string response = await SendCommandToServer(command);

                    if (response.StartsWith("file:"))
                    {
                        try
                        {
                            string fileDataJson = response.Substring(5);
                            byte[] fileData = JsonConvert.DeserializeObject<byte[]>(fileDataJson);
                            File.WriteAllBytes(saveDialog.FileName, fileData);
                            MessageBox.Show($"Файл {selectedItem.Name} успешно скачан в {saveDialog.FileName}",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            UpdateStatus($"Файл {selectedItem.Name} скачан");
                        }
                        catch (Exception jsonEx)
                        {
                            MessageBox.Show($"Ошибка обработки файла: {jsonEx.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (response.StartsWith("message:"))
                    {
                        string errorMessage = response.Substring(8);
                        MessageBox.Show($"Ошибка скачивания: {errorMessage}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        MessageBox.Show($"Неизвестный ответ от сервера: {response}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка скачивания: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog();
                if (openDialog.ShowDialog() == true)
                {
                    string fileName = Path.GetFileName(openDialog.FileName);
                    byte[] fileData = File.ReadAllBytes(openDialog.FileName);

                    FileInfoFTP fileInfo = new FileInfoFTP(fileData, fileName);
                    string message = JsonConvert.SerializeObject(fileInfo);

                    string response = await SendCommandToServer(message);
                    MessageBox.Show(response);

                    if (response.Contains("успешно") || response.Contains("загружен"))
                    {
                        await RefreshFileList();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshFileList();
        }

        private async System.Threading.Tasks.Task RefreshFileList()
        {
            try
            {
                string response = await SendCommandToServer("cd");
                if (response.StartsWith("cd:"))
                {
                    await ParseAndDisplayFiles(response);
                }
                else
                {
                    MessageBox.Show("Ошибка обновления: " + response);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ParseAndDisplayFiles(string serverResponse)
        {
            try
            {
                string jsonData = serverResponse.Substring(3);
                List<string> files = JsonConvert.DeserializeObject<List<string>>(jsonData);

                lstFiles.Items.Clear();
                lstFiles.Items.Add(new FileItem("..", true) { FullName = ".." });

                foreach (string file in files)
                {
                    bool isDirectory = file.EndsWith("/");
                    string fullName = file;
                    if (string.IsNullOrEmpty(fullName) || fullName == "/") continue;
                    if (isDirectory && fullName.EndsWith("/"))
                        fullName = fullName.Substring(0, fullName.Length - 1);

                    lstFiles.Items.Add(new FileItem(fullName, isDirectory) { FullName = fullName });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка парсинга файлов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async System.Threading.Tasks.Task<string> SendCommandToServer(string message)
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(txtIp.Text), int.Parse(txtPort.Text));
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(endPoint);

                    if (socket.Connected)
                    {
                        ViewModelSend viewModelSend = new ViewModelSend(message, currentId);
                        string jsonData = JsonConvert.SerializeObject(viewModelSend);
                        byte[] data = Encoding.UTF8.GetBytes(jsonData);

                        socket.Send(data);

                        byte[] buffer = new byte[10485760];
                        int bytesReceived = socket.Receive(buffer);
                        string response = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

                        ViewModelMessage viewModelMessage = JsonConvert.DeserializeObject<ViewModelMessage>(response);

                        return $"{viewModelMessage.Command}:{viewModelMessage.Data}";
                    }
                    else
                    {
                        return "Ошибка подключения к серверу";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Ошибка: {ex.Message}";
            }
        }
    }
}