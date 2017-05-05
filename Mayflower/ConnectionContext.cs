using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace Mayflower
{
    class ConnectionContext : IDisposable
    {
        readonly SqlConnection _conn;
        SqlTransaction _tran;

        public Database Database { get; }
        //

        ConnectionContext(SqlConnection conn, Database db)
        {
            _conn = conn;
            Database = db;
        }

        internal static ConnectionContext TryOpen(Database db, ILogger logger)
        {
            logger.Log(Verbosity.Debug, $"Opening connection to {db.DatabaseName} on {db.ServerName}");
            try
            {
                var conn = new SqlConnection(db.ConnectionString);
                conn.Open();

                return new ConnectionContext(conn, db);
            }
            catch (Exception ex)
            {
                logger.Log(Verbosity.Error, $"Unable to open connection to {db.DatabaseName} on {db.ServerName}");
                logger.Log(Verbosity.Debug, "");
                logger.Log(Verbosity.Debug, ex.Message);
                logger.Log(Verbosity.Debug, ex.StackTrace);
                logger.Log(Verbosity.Debug, "");

                return null;
            }
        }

        internal void BeginTransaction(ILogger logger)
        {
            logger.Log(Verbosity.Debug, "Beginning transaction");
            _tran = _conn.BeginTransaction();
        }

        internal void CommitTransaction(ILogger logger)
        {
            logger.Log(Verbosity.Debug, "Committing transaction");
            _tran.Commit();
            _tran = null;
        }

        internal void RollbackTransaction(ILogger logger)
        {
            logger.Log(Verbosity.Debug, "Rolling back transaction");
            _tran.Commit();
            _tran = null;
        }

        internal void EnsureMigrationsTableExists(ILogger logger)
        {
            throw new NotImplementedException();
        }

        internal void LoadMigrationRecords(ILogger logger)
        {
            throw new NotImplementedException();
        }

        internal void ExecuteMigrationCommands(List<string> commands, ILogger logger)
        {
            throw new NotImplementedException();
        }

        void WriteRecord(Migration migration, TimeSpan duration, ILogger logger)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _conn.Dispose();
        }
    }
}