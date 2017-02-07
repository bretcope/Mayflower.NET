using System;
using System.Data.SqlClient;

namespace Mayflower
{
    /// <summary>
    /// Describes the information necessary for establishing a connection with a SQL Server database.
    /// </summary>
    public class ConnectionInfo
    {
        /// <summary>
        /// The connection string which can be used for establishing the connection.
        /// </summary>
        public string ConnectionString { get; }
        /// <summary>
        /// Name of the SQL Server database.
        /// </summary>
        public string DatabaseName { get; }
        /// <summary>
        /// Hostname (or IP address) of the SQL Server machine.
        /// </summary>
        public string ServerName { get; }

        ConnectionInfo(string connection, string db, string server)
        {
            ConnectionString = connection;
            DatabaseName = db;
            ServerName = server;
        }

        /// <summary>
        /// Creates an integrated auth connection string.
        /// </summary>
        public static ConnectionInfo BuildIntegratedAuth(string databaseName, string serverName = "localhost")
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new Exception("Database name cannot be null or empty.");

            if (string.IsNullOrWhiteSpace(serverName))
                throw new Exception("Server name cannot be null or empty.");

            var conn = $"Persist Security Info=False;Integrated Security=true;Initial Catalog={databaseName};server={serverName}";

            return new ConnectionInfo(conn, databaseName, serverName);
        }

        /// <summary>
        /// Creates a ConnectionInfo instance from a raw connection string.
        /// </summary>
        public static ConnectionInfo FromConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new Exception("Connection string cannot be null or empty.");

            var builder = new SqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
                throw new Exception("Connection string must provide an initial catalog (database).");

            if (string.IsNullOrWhiteSpace(builder.DataSource))
                throw new Exception("Connection string must provide a data source (server).");

            return new ConnectionInfo(connectionString, builder.InitialCatalog, builder.DataSource);
        }
    }
}