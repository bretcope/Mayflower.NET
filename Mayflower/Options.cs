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
        public DatabaseProvider Provider { get; set; } // there's no command line param for this yet because there's only one provider

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
            if (provider != DatabaseProvider.SqlServer)
                throw new Exception($"Unsupported DatabaseProvider " + provider);

            if (!string.IsNullOrEmpty(ConnectionString))
                return ConnectionString;

            var server = string.IsNullOrEmpty(Server) ? "localhost" : Server;
            return $"Persist Security Info=False;Integrated Security=true;Initial Catalog={Database};server={server}";
        }

        internal string GetMigrationsTable()
        {
            return string.IsNullOrEmpty(MigrationsTable) ? "Migrations" : MigrationsTable;
        }

        internal string GetFolder()
        {
            return string.IsNullOrEmpty(MigrationsFolder) ? Directory.GetCurrentDirectory() : MigrationsFolder;
        }
    }
}