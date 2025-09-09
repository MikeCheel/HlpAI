using System;
using System.IO;

namespace HlpAI.Utilities
{
    /// <summary>
    /// Provides centralized database path management for HlpAI application databases.
    /// All databases are stored in the user's home directory under .hlpai folder.
    /// </summary>
    public static class DatabasePathHelper
    {
        private static readonly string HlpAiDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            ".hlpai"
        );

        /// <summary>
        /// Gets the full path to the configuration database.
        /// </summary>
        public static string ConfigDatabasePath => Path.Combine(HlpAiDirectory, "config.db");

        /// <summary>
        /// Gets the full path to the vector database.
        /// </summary>
        public static string VectorDatabasePath => Path.Combine(HlpAiDirectory, "vectors.db");

        /// <summary>
        /// Gets the full path to the HlpAI application directory.
        /// </summary>
        public static string ApplicationDirectory => HlpAiDirectory;

        /// <summary>
        /// Gets the connection string for the vector database.
        /// </summary>
        public static string VectorDatabaseConnectionString => $"Data Source={VectorDatabasePath}";

        /// <summary>
        /// Ensures the HlpAI application directory exists.
        /// </summary>
        public static void EnsureApplicationDirectoryExists()
        {
            if (!Directory.Exists(HlpAiDirectory))
            {
                Directory.CreateDirectory(HlpAiDirectory);
            }
        }

        /// <summary>
        /// Gets a custom database path within the HlpAI directory.
        /// </summary>
        /// <param name="databaseName">The name of the database file (including extension)</param>
        /// <returns>Full path to the database file</returns>
        public static string GetDatabasePath(string databaseName)
        {
            return Path.Combine(HlpAiDirectory, databaseName);
        }
    }
}