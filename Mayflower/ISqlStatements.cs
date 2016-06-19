using System.Text.RegularExpressions;

namespace Mayflower
{
    interface ISqlStatements
    {
        /// <summary>
        /// The regular expression used to break up multiple commands within a single migration.
        /// </summary>
        Regex CommandSplitter { get; }
        /// <summary>
        /// Should return a single integer. 1 indicating the table exists. 0 indicating it does not.
        /// </summary>
        string DoesMigrationsTableExist { get; }
        /// <summary>
        /// Creates the migrations table. Does not take any parameters.
        /// </summary>
        string CreateMigrationsTable { get; }
        /// <summary>
        /// Changes the Filename of a migration. Requires @Filename and @Hash parameters.
        /// </summary>
        string RenameMigration { get; }
        /// <summary>
        /// Updates the hash of a migration by Filename. Requires @Filename, @Hash, @ExecutionDate, and @Duration parameters.
        /// </summary>
        string UpdateMigrationHash { get; }
        /// <summary>
        /// Inserts a new migration record. Requires @Filename, @Hash, @ExecutionDate, and @Duration parameters.
        /// </summary>
        string InsertMigration { get; }
        /// <summary>
        /// Selects all previously run migrations, ordered by ExecutedDate. Does not take any parameters.
        /// </summary>
        string GetAlreadyRan { get; }
    }

    class SqlServerStatements : ISqlStatements
    {
        public Regex CommandSplitter { get; } = new Regex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        public string DoesMigrationsTableExist { get; }
        public string CreateMigrationsTable { get; }
        public string RenameMigration { get; }
        public string UpdateMigrationHash { get; }
        public string InsertMigration { get; }
        public string GetAlreadyRan { get; }
        
        internal SqlServerStatements(string migrationsTableName)
        {
            DoesMigrationsTableExist = $"SELECT count(*) FROM sys.tables WHERE name = '{migrationsTableName}';";

            CreateMigrationsTable = $@"
CREATE TABLE [{migrationsTableName}]
(
    Id int not null Identity(1,1),
    Filename nvarchar(260) not null,
    Hash varchar(40) not null,
    ExecutionDate datetime not null,
    Duration int not null,

    constraint PK_{migrationsTableName} primary key clustered (Id),
    constraint UX_{migrationsTableName}_Filename unique (Filename),
    constraint UX_{migrationsTableName}_Hash unique (Hash)
);
";
            RenameMigration = $"update [{migrationsTableName}] set Filename = @Filename where Hash = @Hash;";
            UpdateMigrationHash = $"update [{migrationsTableName}] set Hash = @Hash, ExecutionDate = @ExecutionDate, Duration = @Duration where Filename = @Filename;";
            InsertMigration = $"insert [{migrationsTableName}] (Filename, Hash, ExecutionDate, Duration) values (@Filename, @Hash, @ExecutionDate, @Duration);";
            GetAlreadyRan = $"select * from [{migrationsTableName}] order by ExecutionDate, Id;";
        }
    }
}