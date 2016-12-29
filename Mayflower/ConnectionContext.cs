using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace Mayflower
{
    public enum DatabaseProvider
    {
        SqlServer = 0,
    }

    class ConnectionContext : IDisposable
    {
        readonly IDbConnection _connection;
        IDbTransaction _transaction;
        readonly ISqlStatements _sql;
        readonly int _timeout;

        internal bool IsPreview { get; }
        internal DatabaseProvider Provider { get; }
        internal string Database { get; }
        internal List<string> FilesInCurrentTransaction { get; } = new List<string>();
        
        internal Regex CommandSplitter => _sql.CommandSplitter;
        internal bool HasPendingTransaction => _transaction != null;

        internal ConnectionContext(Options options)
        {
            _timeout = options.CommandTimeout;
            IsPreview = options.IsPreview;
            Provider = options.Provider;

            var connStr = options.GetConnectionString(Provider);

            switch (Provider)
            {
                case DatabaseProvider.SqlServer:
                    _sql = new SqlServerStatements(options.GetMigrationsTable());
                    _connection = new SqlConnection(connStr);
                    Database = new SqlConnectionStringBuilder(connStr).InitialCatalog;
                    break;
                default:
                    throw new Exception("Unsupported DatabaseProvider " + options.Provider);
            }

            if (string.IsNullOrEmpty(Database))
                throw new Exception("No database was set in the connection string.");
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        internal void Open()
        {
            _connection.Open();
        }

        internal void BeginTransaction()
        {
            _transaction = _connection.BeginTransaction();
        }

        internal void Commit()
        {
            if (IsPreview)
                _transaction.Rollback();
            else
                _transaction.Commit();

            _transaction = null;
            FilesInCurrentTransaction.Clear();
        }

        internal void Rollback()
        {
            _transaction.Rollback();
            _transaction = null;
            FilesInCurrentTransaction.Clear();
        }

        internal bool MigrationTableExists()
        {
            var cmd = _connection.NewCommand(_sql.DoesMigrationsTableExist);
            return (int)cmd.ExecuteScalar() == 1;
        }

        internal void CreateMigrationsTable()
        {
            var cmd = _connection.NewCommand(_sql.CreateMigrationsTable);
            cmd.ExecuteNonQuery();
        }

        internal AlreadyRan GetAlreadyRan()
        {
            var results = new List<MigrationRow>();
            var cmd = _connection.NewCommand(_sql.GetAlreadyRan);

            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    var row = new MigrationRow();

                    row.Id = rdr.GetInt32(0);
                    row.Filename = rdr.GetString(1);
                    row.Hash = rdr.GetString(2);
                    row.ExecutionDate = rdr.GetDateTime(3);
                    row.Duration = rdr.GetInt32(4);

                    results.Add(row);
                }
            }

            return new AlreadyRan(results);
        }

        public int ExecuteCommand(string sql)
        {
            var cmd = _connection.NewCommand(sql, _transaction, _timeout);
            return cmd.ExecuteNonQuery();

        }

        internal void InsertMigrationRecord(MigrationRow row)
        {
            var cmd = _connection.NewCommand(_sql.InsertMigration, _transaction);

            cmd.AddParameter("Filename", row.Filename);
            cmd.AddParameter("Hash", row.Hash);
            cmd.AddParameter("ExecutionDate", row.ExecutionDate);
            cmd.AddParameter("Duration", row.Duration);

            cmd.ExecuteNonQuery();
        }

        internal void UpdateMigrationRecordHash(MigrationRow row)
        {
            var cmd = _connection.NewCommand(_sql.UpdateMigrationHash, _transaction);

            cmd.AddParameter("Hash", row.Hash);
            cmd.AddParameter("ExecutionDate", row.ExecutionDate);
            cmd.AddParameter("Duration", row.Duration);
            cmd.AddParameter("Filename", row.Filename);

            var affected = cmd.ExecuteNonQuery();
            if (affected != 1)
                throw new Exception($"Failure updating the migration record. {affected} rows affected. Expected 1.");
        }

        internal void RenameMigration(Migration migration)
        {
            var cmd = _connection.NewCommand(_sql.RenameMigration, _transaction);

            cmd.AddParameter("Filename", migration.Filename);
            cmd.AddParameter("Hash", migration.Hash);

            var affected = cmd.ExecuteNonQuery();
            if (affected != 1)
                throw new Exception($"Failure renaming the migration record. {affected} rows affected. Expected 1.");
        }
    }
}