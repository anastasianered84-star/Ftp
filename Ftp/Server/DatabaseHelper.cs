using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using Common;
using System.IO;

namespace Server
{
    public static class DatabaseHelper
    {
        private static string connectionString = "Server=localhost;Database=ftp;Uid=root;Pwd=;";

        // Добавляем кэш для хранения текущих путей пользователей
        private static Dictionary<int, string> userCurrentPaths = new Dictionary<int, string>();

        public static User GetUser(string login, string password)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT id, login, password, src FROM users WHERE login = @login AND password = @password";

                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@login", login);
                    command.Parameters.AddWithValue("@password", password);

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int userId = reader.GetInt32("id");
                            string userSrc = reader.GetString("src");

                            // Нормализуем путь пользователя
                            userSrc = Path.GetFullPath(userSrc);

                            // Сохраняем начальный путь пользователя
                            if (!userCurrentPaths.ContainsKey(userId))
                            {
                                userCurrentPaths[userId] = userSrc;
                            }

                            return new User(
                                userId,
                                reader.GetString("login"),
                                reader.GetString("password"),
                                userSrc
                            );
                        }
                    }
                }
            }
            return null;
        }

        public static User GetUserById(int id)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT id, login, password, src FROM users WHERE id = @id";

                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@id", id);

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string userSrc = Path.GetFullPath(reader.GetString("src"));
                            return new User(
                                reader.GetInt32("id"),
                                reader.GetString("login"),
                                reader.GetString("password"),
                                userSrc
                            );
                        }
                    }
                }
            }
            return null;
        }

        public static string GetUserCurrentPath(int userId)
        {
            if (userCurrentPaths.ContainsKey(userId))
            {
                return userCurrentPaths[userId];
            }
            else
            {
                var user = GetUserById(userId);
                if (user != null)
                {
                    userCurrentPaths[userId] = user.src;
                    return user.src;
                }
            }
            return null;
        }

        public static void SetUserCurrentPath(int userId, string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                userCurrentPaths[userId] = Path.GetFullPath(path);
            }
        }

        // Метод для проверки безопасности пути
        public static bool IsPathAllowed(int userId, string path)
        {
            try
            {
                var user = GetUserById(userId);
                if (user == null) return false;

                string userRoot = user.src;
                string fullPath = Path.GetFullPath(path);

                // Проверяем, что путь начинается с корневой директории пользователя
                return fullPath.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void LogCommand(int userId, string command, string parameters, string status)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
                    INSERT INTO user_commands (user_id, command, parameters, status, executed_at) 
                    VALUES (@userId, @command, @parameters, @status, NOW())";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@command", command);
                    cmd.Parameters.AddWithValue("@parameters", parameters);
                    cmd.Parameters.AddWithValue("@status", status);

                    cmd.ExecuteNonQuery();
                }
            }
        }
        public static string GetRelativePath(int userId)
        {
            try
            {
                string currentPath = GetUserCurrentPath(userId);
                string rootPath = GetUserById(userId).src;

                if (currentPath.StartsWith(rootPath))
                {
                    string relativePath = currentPath.Substring(rootPath.Length);
                    if (string.IsNullOrEmpty(relativePath))
                        return "/";
                    else
                        return relativePath.Replace("\\", "/");
                }

                return "/";
            }
            catch
            {
                return "/";
            }
        }

    }
}