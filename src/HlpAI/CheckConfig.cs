using System;
using Microsoft.Data.Sqlite;
using System.IO;

namespace HlpAI
{
    public static class CheckConfig
    {
        public static void DisplayConfiguration()
        {
        string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hlpai", "config.db");
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine("Config.db not found at: " + dbPath);
            return;
        }
        
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        
        Console.WriteLine("=== CONFIGURATION TABLE ===");
        using (var cmd = new SqliteCommand("SELECT * FROM configuration", connection))
        {
            using var reader = cmd.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    Console.WriteLine($"ID: {reader["id"]}");
                    Console.WriteLine($"AI Provider: {reader["ai_provider"]}");
                    Console.WriteLine($"AI Model: {reader["ai_model"]}");
                    Console.WriteLine($"Operation Mode: {reader["operation_mode"]}");
                    Console.WriteLine($"Created At: {reader["created_at"]}");
                    Console.WriteLine($"Updated At: {reader["updated_at"]}");
                    Console.WriteLine("---");
                }
            }
            else
            {
                Console.WriteLine("No configurations found in configuration table.");
            }
        }
        
        Console.WriteLine("\n=== DIRECTORY CONFIGURATIONS TABLE ===");
        using (var cmd = new SqliteCommand("SELECT * FROM directory_configurations", connection))
        {
            using var reader = cmd.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    Console.WriteLine($"ID: {reader["id"]}");
                    Console.WriteLine($"Directory Path: {reader["directory_path"]}");
                    Console.WriteLine($"AI Provider: {reader["ai_provider"]}");
                    Console.WriteLine($"AI Model: {reader["ai_model"]}");
                    Console.WriteLine($"Operation Mode: {reader["operation_mode"]}");
                    Console.WriteLine($"Created At: {reader["created_at"]}");
                    Console.WriteLine($"Updated At: {reader["updated_at"]}");
                    Console.WriteLine("---");
                }
            }
            else
            {
                Console.WriteLine("No directory configurations found.");
            }
        }
        }
    }
}