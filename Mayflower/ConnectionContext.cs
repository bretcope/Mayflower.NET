using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using Dapper;

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
            return _connection.ExecuteScalar<int>(_sql.DoesMigrationsTableExist) == 1;
        }

        internal void CreateMigrationsTable()
        {
            _connection.Execute(_sql.CreateMigrationsTable);
        }

        internal AlreadyRan GetAlreadyRan()
        {
            var results = _connection.Query<MigrationRow>(_sql.GetAlreadyRan);
            return new AlreadyRan(results);
        }

        public int ExecuteCommand(string sql)
        {
            return _connection.Execute(sql, transaction: _transaction, commandTimeout: _timeout);
        }

        internal void InsertMigrationRecord(MigrationRow row)
        {
            _connection.Execute(_sql.InsertMigration, row, _transaction);
        }

        internal void UpdateMigrationRecordHash(MigrationRow row)
        {
            var affected = _connection.Execute(_sql.UpdateMigrationHash, row, _transaction);
            if (affected != 1)
                throw new Exception($"Failure updating the migration record. {affected} rows affected. Expected 1.");
        }

        internal void RenameMigration(Migration migration)
        {
            var affected = _connection.Execute(_sql.RenameMigration, migration, _transaction);
            if (affected != 1)
                throw new Exception($"Failure renaming the migration record. {affected} rows affected. Expected 1.");
        }
    }
}