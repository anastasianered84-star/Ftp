using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Common;
using Newtonsoft.Json;

namespace Server
{
    public class Program
    {
        public static IPAddress IpAdress;
        public static int Port;

        public static bool Autorization(string login, string password, out User user)
        {
            user = DatabaseHelper.GetUser(login, password);
            return user != null;
        }

        public static List<string> GetDirectory(string src)
        {
            List<string> FoldersFiles = new List<string>();

            if (Directory.Exists(src))
            {
                try
                {
                    string[] dirs = Directory.GetDirectories(src);
                    foreach (string dir in dirs)
                    {
                        string dirName = Path.GetFileName(dir);
                        if (!string.IsNullOrEmpty(dirName))
                        {
                            FoldersFiles.Add(dirName + "/");
                        }
                    }

                    string[] files = Directory.GetFiles(src);
                    foreach (string file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            FoldersFiles.Add(fileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка чтения директории {src}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Директория не существует: {src}");
            }
            return FoldersFiles;
        }

        public static void StartServer()
        {
            IPEndPoint endPoint = new IPEndPoint(IpAdress, Port);
            Socket sListener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            sListener.Bind(endPoint);
            sListener.Listen(10);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Сервер запущен.");

            while (true)
            {
                try
                {
                    Socket Handler = sListener.Accept();
                    string Data = null;
                    byte[] Bytes = new byte[10485760];

                    int BytesRec = Handler.Receive(Bytes);
                    Data += Encoding.UTF8.GetString(Bytes, 0, BytesRec);
                    Console.Write("Сообщение от пользователя: " + Data + "\n");

                    string Reply = "";
                    ViewModelSend ViewModelSend = JsonConvert.DeserializeObject<ViewModelSend>(Data);

                    if (ViewModelSend != null)
                    {
                        ViewModelMessage viewModelMessage;
                        string[] DataCommand = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                        string command = DataCommand[0];
                        byte[] responseBytes;

                        if (command == "connect")
                        {
                            string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            User user;
                            if (Autorization(DataMessage[1], DataMessage[2], out user))
                            {
                                viewModelMessage = new ViewModelMessage("autorization", user.Id.ToString());
                                DatabaseHelper.LogCommand(user.Id, "connect", $"{DataMessage[1]}", "success");
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Не правильный логин и пароль пользователя");
                                DatabaseHelper.LogCommand(-1, "connect", $"{DataMessage[1]}", "failed");
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            responseBytes = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(responseBytes);
                        }
                        else if (command == "cd")
                        {
                            if (ViewModelSend.Id == -1)
                            {
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                                Reply = JsonConvert.SerializeObject(viewModelMessage);
                                responseBytes = Encoding.UTF8.GetBytes(Reply);
                                Handler.Send(responseBytes);
                                continue;
                            }

                            string currentPath = DatabaseHelper.GetUserCurrentPath(ViewModelSend.Id);
                            string userRoot = DatabaseHelper.GetUserById(ViewModelSend.Id).src;
                            string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            List<string> FoldersFiles = new List<string>();
                            string cdFolder = "";

                            if (DataMessage.Length == 1)
                            {
                                currentPath = userRoot;
                                DatabaseHelper.SetUserCurrentPath(ViewModelSend.Id, currentPath);
                                FoldersFiles = GetDirectory(currentPath);
                            }
                            else
                            {
                                for (int i = 1; i < DataMessage.Length; i++)
                                    if (cdFolder == "")
                                        cdFolder += DataMessage[i];
                                    else
                                        cdFolder += " " + DataMessage[i];

                                string newPath;
                                if (cdFolder == "..")
                                {
                                    if (string.Equals(currentPath, userRoot, StringComparison.OrdinalIgnoreCase))
                                    {
                                        viewModelMessage = new ViewModelMessage("message", "Вы уже в корневой директории");
                                        Reply = JsonConvert.SerializeObject(viewModelMessage);
                                        responseBytes = Encoding.UTF8.GetBytes(Reply);
                                        Handler.Send(responseBytes);
                                        continue;
                                    }

                                    DirectoryInfo parentDir = Directory.GetParent(currentPath);
                                    if (parentDir != null)
                                    {
                                        newPath = parentDir.FullName;
                                        if (!newPath.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase))
                                        {
                                            newPath = userRoot; 
                                        }
                                    }
                                    else
                                    {
                                        newPath = userRoot; 
                                    }

                                    Console.WriteLine($"Переход на уровень выше: {currentPath} -> {newPath}");
                                }
                                else
                                {
                                    newPath = Path.Combine(currentPath, cdFolder);
                                    Console.WriteLine($"Переход в папку: {currentPath} + {cdFolder} = {newPath}");
                                }

                                newPath = Path.GetFullPath(newPath);
                                if (!DatabaseHelper.IsPathAllowed(ViewModelSend.Id, newPath))
                                {
                                    Console.WriteLine($"Доступ запрещен: {newPath} не находится в {userRoot}");
                                    viewModelMessage = new ViewModelMessage("message", "Доступ запрещен: выход за пределы корневой директории");
                                    Reply = JsonConvert.SerializeObject(viewModelMessage);
                                    responseBytes = Encoding.UTF8.GetBytes(Reply);
                                    Handler.Send(responseBytes);
                                    continue;
                                }
                                if (!Directory.Exists(newPath))
                                {
                                    viewModelMessage = new ViewModelMessage("message", "Директория не существует");
                                    Reply = JsonConvert.SerializeObject(viewModelMessage);
                                    responseBytes = Encoding.UTF8.GetBytes(Reply);
                                    Handler.Send(responseBytes);
                                    continue;
                                }

                                currentPath = newPath;
                                DatabaseHelper.SetUserCurrentPath(ViewModelSend.Id, currentPath);
                                FoldersFiles = GetDirectory(currentPath);
                            }

                            if (FoldersFiles.Count == 0)
                            {
                                viewModelMessage = new ViewModelMessage("message", "Директория пуста");
                                DatabaseHelper.LogCommand(ViewModelSend.Id, "cd", cdFolder, "success");
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles));
                                DatabaseHelper.LogCommand(ViewModelSend.Id, "cd", cdFolder, "success");
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            responseBytes = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(responseBytes);
                        }
                        else if (command == "pwd") 
                        {
                            if (ViewModelSend.Id == -1)
                            {
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            }
                            else
                            {
                                string relativePath = DatabaseHelper.GetRelativePath(ViewModelSend.Id);
                                viewModelMessage = new ViewModelMessage("path", relativePath);
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            responseBytes = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(responseBytes);
                        }
                        else if (command == "get")
                        {
                            if (ViewModelSend.Id == -1)
                            {
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            }
                            else
                            {
                                string currentPath = DatabaseHelper.GetUserCurrentPath(ViewModelSend.Id);
                                string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                                string getFile = "";

                                for (int i = 1; i < DataMessage.Length; i++)
                                    if (getFile == "")
                                        getFile += DataMessage[i];
                                    else
                                        getFile += " " + DataMessage[i];

                                // Убираем начальный слеш если есть
                                if (getFile.StartsWith("/") || getFile.StartsWith("\\"))
                                    getFile = getFile.Substring(1);

                                string filePath = Path.Combine(currentPath, getFile);
                                filePath = Path.GetFullPath(filePath);

                                Console.WriteLine($"Попытка скачать файл: {filePath}");
                                Console.WriteLine($"Текущий путь: {currentPath}");
                                Console.WriteLine($"Запрошенный файл: {getFile}");

                                if (!DatabaseHelper.IsPathAllowed(ViewModelSend.Id, filePath))
                                {
                                    Console.WriteLine($"Доступ запрещен: {filePath} не находится в {DatabaseHelper.GetUserById(ViewModelSend.Id).src}");
                                    viewModelMessage = new ViewModelMessage("message", "Доступ запрещен: файл вне корневой директории");
                                }
                                else if (File.Exists(filePath))
                                {
                                    try
                                    {
                                        byte[] byteFile = File.ReadAllBytes(filePath);
                                        viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile));
                                        DatabaseHelper.LogCommand(ViewModelSend.Id, "get", getFile, "success");
                                        Console.WriteLine($"Файл {filePath} успешно прочитан, размер: {byteFile.Length} байт");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Ошибка чтения файла: {ex.Message}");
                                        viewModelMessage = new ViewModelMessage("message", $"Ошибка чтения файла: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Файл не существует: {filePath}");
                                    viewModelMessage = new ViewModelMessage("message", "Файл не существует");
                                    DatabaseHelper.LogCommand(ViewModelSend.Id, "get", getFile, "failed");
                                }
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            responseBytes = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(responseBytes);
                        }
                        else 
                        {
                            if (ViewModelSend.Id == -1)
                            {
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            }
                            else
                            {
                                string currentPath = DatabaseHelper.GetUserCurrentPath(ViewModelSend.Id);
                                FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(ViewModelSend.Message);
                                string filePath = Path.Combine(currentPath, SendFileInfo.Name);
                                if (!DatabaseHelper.IsPathAllowed(ViewModelSend.Id, filePath))
                                {
                                    viewModelMessage = new ViewModelMessage("message", "Доступ запрещен: выход за пределы корневой директории");
                                }
                                else
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                                    File.WriteAllBytes(filePath, SendFileInfo.Data);
                                    viewModelMessage = new ViewModelMessage("message", "Файл загружен");
                                    DatabaseHelper.LogCommand(ViewModelSend.Id, "set", SendFileInfo.Name, "success");
                                }
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            responseBytes = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(responseBytes);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Ошибка: " + ex.Message);
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Введите IP адрес сервера: ");
            string sIpAdress = Console.ReadLine();

            Console.Write("Введите порт: ");
            string sPort = Console.ReadLine();

            if (int.TryParse(sPort, out Port) && IPAddress.TryParse(sIpAdress, out IpAdress))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Данные успешно введены. Запускаю сервер.");
                StartServer();
            }

            Console.Read();
        }
    }
}