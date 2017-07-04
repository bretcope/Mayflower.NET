using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Mayflower
{
    public class Options
    {
        public string ConnectionString { get; set; }
        public string Database { get; set; }
        public string Server { get; set; }
        public int CommandTimeout { get; set; } = 30;
        public string MigrationsFolder { get; set; }
        public bool IsPreview { get; set; }
        public bool UseGlobalTransaction { get; set; }
        public string MigrationsTable { get; set; }
        public TextWriter Output { get; set; }
        public bool Force { get; set; }
        public DatabaseProvider Provider { get; set; }

        public void AssertValid()
        {
            if (string.IsNullOrEmpty(ConnectionString) == string.IsNullOrEmpty(Database))
            {
                throw new Exception("Either a connection string or a database must be specified.");
            }

            if (!string.IsNullOrEmpty(MigrationsTable))
            {
                if (!Regex.IsMatch(MigrationsTable, "^[a-zA-Z]+$"))
                    throw new Exception("Migrations table name can only contain letters A-Z.");
            }
        }

        internal string GetConnectionString(DatabaseProvider provider)
        {
            if (!string.IsNullOrEmpty(ConnectionString))
                return ConnectionString;

            var server = string.IsNullOrEmpty(Server) ? "localhost" : Server;

            if (provider == DatabaseProvider.SqlServer)
                return $"Persist Security Info=False;Integrated Security=true;Initial Catalog={Database};server={server}";
            else if (provider == DatabaseProvider.MySql)
                return $"Database={Database};server={server}";
            else
                throw new Exception("Database provider is not supported.");
        }

        internal string GetMigrationsTable(DatabaseProvider provider)
        {
            if (provider == DatabaseProvider.SqlServer)
                return string.IsNullOrEmpty(MigrationsTable) ? "Migrations" : MigrationsTable;
            else if (provider == DatabaseProvider.MySql)
                return string.IsNullOrEmpty(MigrationsTable) ? "migrations" : MigrationsTable;
            else
                throw new Exception("Database provider is not supported.");
        }

        internal string GetFolder()
        {
            return string.IsNullOrEmpty(MigrationsFolder) ? Directory.GetCurrentDirectory() : MigrationsFolder;
        }
    }
}