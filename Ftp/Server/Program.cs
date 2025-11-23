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
        public static List<User> Users = new List<User>();
        public static IPAddress IpAdress;
        public static int Port;

        public static bool Autorization(string login, string password)
        {
            User user = null;
            user = Users.Find(x => x.login == login && x.password == password);
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
                        if (DataCommand[0] == "connect")
                        {
                            string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                            if (Autorization(DataMessage[1], DataMessage[2]))
                            {
                                int IdUser = Users.FindIndex(x => x.login == DataMessage[1] && x.password == DataMessage[2]);
                                viewModelMessage = new ViewModelMessage("autorization", IdUser.ToString());
                            }
                            else
                            {
                                viewModelMessage = new ViewModelMessage("message", "Не правильный логин и пароль пользователя");
                            }

                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else if (DataCommand[0] == "cd")
                        {
                            if (ViewModelSend.Id != 1)
                            {
                                string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);
                                List<string> FoldersFiles = new List<string>();

                                if (DataMessage.Length == 1)
                                {
                                    Users[ViewModelSend.Id].temp_src = Users[ViewModelSend.Id].src;
                                    FoldersFiles = GetDirectory(Users[ViewModelSend.Id].src);
                                }
                                else
                                {
                                    string cdFolder = "";
                                    for (int i = 1; i < DataMessage.Length; i++)
                                        if (cdFolder == "")
                                            cdFolder += DataMessage[i];
                                        else
                                            cdFolder += " " + DataMessage[i];
                                    Users[ViewModelSend.Id].temp_src = Users[ViewModelSend.Id].src;
                                    FoldersFiles = GetDirectory(Users[ViewModelSend.Id].src);
                                }
                                if (FoldersFiles.Count == 0)
                                    viewModelMessage = new ViewModelMessage("message", "Директория пуста или не существует");
                                else
                                    viewModelMessage = new ViewModelMessage("cd", JsonConvert.SerializeObject(FoldersFiles));
                            }
                            else
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            Handler.Send(message);
                        }
                        else if (DataCommand[0] == "get")
                        {
                            if (ViewModelSend.Id != -1)
                            {
                                // Разбиваем оставшуюся часть ответа по пробелу, получая наименование файла
                                // которое хочет получить пользователь
                                string[] DataMessage = ViewModelSend.Message.Split(new string[1] { " " }, StringSplitOptions.None);

                                // Создаём переменную отвечающую за наименование файла
                                string getFile = "";

                                // Аналогично с директорией, добавляем пробелы если они есть
                                for (int i = 1; i < DataMessage.Length; i++)
                                    if (getFile == "")
                                        getFile += DataMessage[i];
                                    else
                                        getFile += " " + DataMessage[i];

                                // Преобразовываем файл в набор байт
                                byte[] byteFile = File.ReadAllBytes(Users[ViewModelSend.Id].temp_src + getFile);

                                // Формируем ответ клиенту
                                viewModelMessage = new ViewModelMessage("file", JsonConvert.SerializeObject(byteFile));
                            }
                            else
                                // В случае неудачи, формируем ответ
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");

                            // Конвертируем ответ в Json
                            Reply = JsonConvert.SerializeObject(viewModelMessage);

                            // Разбиваем сообщение от сервера на массив байт
                            byte[] message = Encoding.UTF8.GetBytes(Reply);

                            // Возвращаем пользователю
                            Handler.Send(message);
                        }
                        else
                        {
                            if (ViewModelSend.Id != -1)
                            {
                                // Преобразовываем набор байт пришедших от пользователя в объект
                                FileInfoFTP SendFileInfo = JsonConvert.DeserializeObject<FileInfoFTP>(ViewModelSend.Message);
                                // Сохраняем файл в директории
                                File.WriteAllBytes(Users[ViewModelSend.Id].temp_src + @"\" + SendFileInfo.Name, SendFileInfo.Data);
                                // Формируем ответ
                                viewModelMessage = new ViewModelMessage("message", "Файл загружен");
                            }
                            else
                                // В случае неудачи, формируем ответ
                                viewModelMessage = new ViewModelMessage("message", "Необходимо авторизоваться");

                            // Конвертируем ответ в Json
                            Reply = JsonConvert.SerializeObject(viewModelMessage);
                            // Разбиваем сообщение от сервера на массив байт
                            byte[] message = Encoding.UTF8.GetBytes(Reply);
                            // Возвращаем пользователю
                            Handler.Send(message);
                        }
                    }
                  

                }
                catch(Exception ex) { 
                Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Что то случилось: " + ex.Message);
                }

            }
        }
        static void Main(string[] args)
        {
        }
    }
}
