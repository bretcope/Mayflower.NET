using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Configuration;

namespace Mayflower
{
    public class Options
    {
        public string ConnectionString { get; set; }
        public string ConnectionStringName { get; set; }
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
            if (string.IsNullOrWhiteSpace(ConnectionString) && string.IsNullOrWhiteSpace(Database) && string.IsNullOrWhiteSpace(ConnectionStringName))
            {
                var count = ConfigurationManager.ConnectionStrings.Count;
                if (count == 0 ||
                    (count > 0 && string.IsNullOrWhiteSpace(ConfigurationManager.ConnectionStrings[0].ConnectionString)))
                {
                    throw new Exception(
                        "There is no connection string in the config file. Either a connection string or a database must be specified.");
                }
            }

            if (!string.IsNullOrWhiteSpace(ConnectionStringName) && string.IsNullOrWhiteSpace(ConfigurationManager.ConnectionStrings[ConnectionStringName]?.ConnectionString))
            {
                throw new Exception("When a connection string name is specified, it must exist in the .config file.");
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

            if (!string.IsNullOrEmpty(ConnectionStringName))
                return ConfigurationManager.ConnectionStrings[ConnectionStringName]?.ConnectionString;

            if (!string.IsNullOrEmpty(Database))
            {
                var server = string.IsNullOrEmpty(Server) ? "localhost" : Server;
                return $"Persist Security Info=False;Integrated Security=true;Initial Catalog={Database};server={server}";
            }

            if (ConfigurationManager.ConnectionStrings.Count > 0 && !string.IsNullOrWhiteSpace(ConfigurationManager.ConnectionStrings[0].ConnectionString))
            {
                return ConfigurationManager.ConnectionStrings[0].ConnectionString;
            }

            throw new Exception("There is no connection string in the config file. Either a connection string or a database must be specified.");
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