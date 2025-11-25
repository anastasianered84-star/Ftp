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
                string[] dirs = Directory.GetDirectories(src);
                foreach (string dir in dirs)
                {
                    string NameDirectory = dir.Replace(src, "");
                    FoldersFiles.Add(NameDirectory + "/");
                }

                string[] files = Directory.GetFiles(src);
                foreach (string file in files)
                {
                    string NameFile = file.Replace(src, "");
                    FoldersFiles.Add(NameFile);
                }
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
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else if (command == "cd")
                        {
                            User user = DatabaseHelper.GetUserById(ViewModelSend.Id);
                            if (user != null)
                            {
                                string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                                List<string> FoldersFiles = new List<string>();
                                string cdFolder = "";

                                if (DataMessage.Length == 1)
                                {
                                    user.temp_src = user.src;
                                    FoldersFiles = GetDirectory(user.src);
                                }
                                else
                                {
                                    for (int i = 1; i < DataMessage.Length; i++)
                                        if (cdFolder == "")
                                            cdFolder += DataMessage[i];
                                        else
                                            cdFolder += " " + DataMessage[i];
                                    user.temp_src = user.src + cdFolder;
                                    FoldersFiles = GetDirectory(user.temp_src);
                                }

                                if (FoldersFiles.Count == 0)
                                {
                                    viewModelMessage = new ViewModelMessage("message", "Директория пуста или не существует");
                                    DatabaseHelper.LogCommand(user.Id, "cd", cdFolder, "failed");
                                }
                                else
                                {
                                    viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles));
                                    DatabaseHelper.LogCommand(user.Id, "cd", cdFolder, "success");
                                }
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else if (command == "get")
                        {
                            User user = DatabaseHelper.GetUserById(ViewModelSend.Id);
                            if (user != null)
                            {
                                string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                                string getFile = "";

                                for (int i = 1; i < DataMessage.Length; i++)
                                    if (getFile == "")
                                        getFile += DataMessage[i];
                                    else
                                        getFile += " " + DataMessage[i];

                                string filePath = user.temp_src + getFile;
                                if (File.Exists(filePath))
                                {
                                    byte[] byteFile = File.ReadAllBytes(filePath);
                                    viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile));
                                    DatabaseHelper.LogCommand(user.Id, "get", getFile, "success");
                                }
                                else
                                {
                                    viewModelMessage = new ViewModelMessage("message", "Файл не существует");
                                    DatabaseHelper.LogCommand(user.Id, "get", getFile, "failed");
                                }
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else
                        {
                            User user = DatabaseHelper.GetUserById(ViewModelSend.Id);
                            if (user != null)
                            {
                                FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(ViewModelSend.Message);
                                string filePath = Path.Combine(user.temp_src, SendFileInfo.Name);

                                File.WriteAllBytes(filePath, SendFileInfo.Data);
                                viewModelMessage = new ViewModelMessage("message", "Файл загружен");
                                DatabaseHelper.LogCommand(user.Id, "set", SendFileInfo.Name, "success");
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
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