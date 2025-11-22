using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    internal class Program
    {
        private IPAddress ipAddress;
        private int port;
        private int userId = -1;
        static void Main(string[] args)
        {

        }
        public static bool CheckCommand(string message)
        {
            bool BCommand = false;
            string[] DataMessage = message.Split(new string[1](" "), StringSplitOptions.None);
            if (DataMessage.Length > 0)
            {
                string Command = DataMessage[0];
                if (Command == "connect")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("");
                    BCommand = false;
                }
            }
            else if (Command == "cd")
                BCommand = true;
            else if (Command == "get")
            {
                if (DataMessage.Length == 1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("");
                    BCommand = false;
                }
                else
                    BCommand = true;
            }
            else if (Command == "set")
            {
                if (DataMessage.Length == 1)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("");
                    BCommand = false;
                }
                else
                    BCommand = true;
            }
        }

    }
}
