using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using Common;
using System.Data.SqlClient;

namespace Server
{
    public static class DatabaseHelper
    {
        private static string connectionString = "Server=localhost;Database=ftp;Uid=root;Pwd=;";

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
                            return new User(
                                reader.GetInt32("id"),
                                reader.GetString("login"),
                                reader.GetString("password"),
                                reader.GetString("src")
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
                            return new User(
                                reader.GetInt32("id"),
                                reader.GetString("login"),
                                reader.GetString("password"),
                                reader.GetString("src")
                            );
                        }
                    }
                }
            }
            return null;
        }

        public static void LogCommand(int userId, string command, string parameters, string status)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
                    INSERT INTO user_commands (user_id, command, parameters, status, executed_at) 
                    VALUES (@userId, @command, @parameters, @status, NOW())"
                ;

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
    }
}