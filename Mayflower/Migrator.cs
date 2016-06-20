using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Mayflower
{
    public class Migrator : IDisposable
    {
        public class MigrationResult
        {
            public bool Success { get; internal set; }
            public int Attempted { get; internal set; }
            public int Ran { get; internal set; }
            public int Skipped { get; internal set; }
            public int Renamed { get; internal set; }
            public int Forced { get; internal set; }
            public Exception Exception { get; internal set; }
        }
        readonly ConnectionContext _db;
        bool _tableExists;
        readonly bool _isPreview;
        readonly bool _useGlobalTransaction;
        readonly TextWriter _output;
        readonly bool _force;

        readonly AlreadyRan _alreadyRan;

        public List<Migration> Migrations { get; }

        Migrator(Options options)
        {
            _output = options.Output;

            options.AssertValid();
            
            _isPreview = options.IsPreview;
            _useGlobalTransaction = options.UseGlobalTransaction || _isPreview; // always run preview in a global transaction so previous migrations are seen
            _force = options.Force;

            var dir = options.GetDirectory();

            Log("Mayflower.NET Migrator");
            Log("    Directory:        " + dir);
            Log("    Provider:         " + options.Provider);

            _db = new ConnectionContext(options);

            Migrations = GetAllMigrations(dir, _db.CommandSplitter).ToList();

            Log("    Database:         " + _db.Database);
            _db.Open();
            
            Log("    Transaction Mode: " + (_useGlobalTransaction ? "Global" : "Individual"));
            
            EnsureMigrationsTableExists();

            _alreadyRan = _tableExists ? _db.GetAlreadyRan() : new AlreadyRan(Enumerable.Empty<MigrationRow>());
            Log("    Prior Migrations: " + _alreadyRan.Count);
            if (_alreadyRan.Count > 0)
            {
                var last = _alreadyRan.Last;
                Log($"    Last Migration:   \"{last.Filename}\" on {last.ExecutionDate:u}");
            }

            Log();

            if (_isPreview && !options.UseGlobalTransaction)
            {
                Log("Using global transaction mode because of preview mode");
                Log();
            }
        }

        public void Dispose()
        {
            _db?.Dispose();
        }

        public static string GetVersion()
        {
            var attr = typeof(Migrator).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attr.InformationalVersion;
        }

        public static int GetOutstandingMigrationsCount(Options options)
        {
            using (var migrator = Create(options))
            {
                var count = 0;
                foreach (var m in migrator.Migrations)
                {
                    if (m.GetMigrateMode(migrator._alreadyRan) != MigrateMode.Skip)
                        count++;
                }

                return count;
            }
        }

        public static MigrationResult RunOutstandingMigrations(Options options)
        {
            using (var migrator = Create(options))
            {
                return migrator.RunOutstandingMigrations();
            }
        }

        // this only exists because you don't expect a constructor to perform I/O, whereas calling Create() implies there might be some work being performed
        static Migrator Create(Options options)
        {
            return new Migrator(options);
        }

        static IEnumerable<Migration> GetAllMigrations(string directory, Regex commandSplitter)
        {
            return Directory.GetFiles(directory, "*.sql").OrderBy(f => f).Select(f => new Migration(f, commandSplitter));
        }

        MigrationResult RunOutstandingMigrations()
        {
            Log("Running migrations" + (_isPreview ? " (preview mode)" : ""));
            Log();

            var result = new MigrationResult();

            Migration current = null;
            try
            {
                foreach (var m in Migrations)
                {
                    current = m;
                    result.Attempted++;
                    var mode = Migrate(m);

                    switch (mode)
                    {
                        case MigrateMode.Skip:
                            result.Skipped++;
                            break;
                        case MigrateMode.Run:
                            result.Ran++;
                            break;
                        case MigrateMode.HashMismatch:
                            result.Forced++;
                            break;
                        case MigrateMode.Rename:
                            result.Renamed++;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (_db.HasPendingTransaction)
                    _db.Commit();

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Exception = ex;

                MigrationFailed(current, ex);
            }

            Log($"Attempted {result.Attempted} migrations.");
            if (result.Ran > 0)
                Log("  Ran:     " + result.Ran);
            if (result.Forced > 0)
                Log("  Forced:  " + result.Forced);
            if (result.Skipped > 0)
                Log("  Skipped: " + result.Skipped);
            if (result.Renamed > 0)
                Log("  Renamed: " + result.Renamed);

            Log();
            Log((result.Success ? "SUCCESS" : "FAIL") + (_isPreview ? " (preview mode)" : ""));

            return result;
        }

        MigrateMode Migrate(Migration migration)
        {
            var mode = migration.GetMigrateMode(_alreadyRan);

            if (mode == MigrateMode.Skip)
                return mode;

            if (mode == MigrateMode.Rename)
            {
                RenameMigration(migration);
                return mode;
            }
            
            if (mode == MigrateMode.HashMismatch && !_force)
            {
                throw new MigrationChangedException(migration);
            }
            
            if (!migration.UseTransaction && _isPreview)
            {
                Log($"  Skipping \"{migration.Filename}\". It cannot be run in preview mode because the no-transaction header is set.");
                Log();
                return MigrateMode.Skip;
            }

            RunMigrationCommands(migration, mode);
            return mode;
        }

        void RenameMigration(Migration migration)
        {
            var existing = _alreadyRan.ByHash[migration.Hash];
            Log($"  Filename has changed (\"{existing.Filename}\" in the database, \"{migration.Filename}\" in file system) - updating.");
            Log();

            BeginMigration(useTransaction: true);
            _db.RenameMigration(migration);
            EndMigration(migration);
        }

        void RunMigrationCommands(Migration migration, MigrateMode mode)
        {
            BeginMigration(migration.UseTransaction);

            if (mode == MigrateMode.Run)
                Log("  Running \"" + migration.Filename + "\"" + (migration.UseTransaction ? "" : " (NO TRANSACTION)"));
            else if (mode == MigrateMode.HashMismatch)
                Log($"  {migration.Filename} has been modified since it was run. It is being run again because --force was used.");
            else
                throw new Exception("Mayflower bug: RunMigrationCommands called with mode: " + mode);
            
            var sw = new Stopwatch();
            sw.Start();
            foreach (var cmd in migration.SqlCommands)
            {
                var result = _db.ExecuteCommand(cmd);
                Log("    Result: " + (result == -1 ? "No Rows Affected" : result + " rows"));
            }
            sw.Stop();

            if (!_isPreview || _tableExists)
            {
                var recordRow = new MigrationRow()
                {
                    Filename = migration.Filename,
                    Hash = migration.Hash,
                    ExecutionDate = DateTime.UtcNow,
                    Duration = (int)sw.ElapsedMilliseconds,
                };

                if (mode == MigrateMode.Run)
                    _db.InsertMigrationRecord(recordRow);
                else
                    _db.UpdateMigrationRecordHash(recordRow);
            }

            Log();
            EndMigration(migration);
        }

        void BeginMigration(bool useTransaction)
        {
            if (useTransaction)
            {
                if (!_db.HasPendingTransaction)
                {
                    _db.BeginTransaction();
                }
            }
            else
            {
                if (_db.HasPendingTransaction)
                {
                    Log("  Breaking up Global Transaction");

                    if (_db.FilesInCurrentTransaction.Count > 0)
                        Log("    Committing all migrations which have run up to this point...");
                }
            }
        }

        void EndMigration(Migration migration)
        {
            if (_db.HasPendingTransaction)
            {
                if (_useGlobalTransaction)
                    _db.FilesInCurrentTransaction.Add(migration.Filename);
                else 
                    _db.Commit();
            }
        }

        void MigrationFailed(Migration migration, Exception ex)
        {
            Log();
            if (migration == null)
            {
                Log("ERROR:");
            }
            else
            {
                if (!_db.HasPendingTransaction && !migration.UseTransaction)
                    Log("FAILED WITHOUT A TRANSACTION: " + migration.Filename);
                else
                    Log("FAILED: " + migration.Filename);
            }

            Log(ex.Message);
            Log();

            if (_db.HasPendingTransaction)
            {
                if (_db.FilesInCurrentTransaction.Count > 0)
                {
                    Log(" Rolling back prior migrations:");
                    foreach (var f in _db.FilesInCurrentTransaction)
                    {
                        Log("    " + f);
                    }
                }

                _db.Rollback();
            }
        }

        void Log(string str = "")
        {
            _output?.WriteLine(str);
        }

        void EnsureMigrationsTableExists()
        {
            if (_db.MigrationTableExists())
            {
                _tableExists = true;
                return;
            }

            if (_isPreview)
            {
                // don't want to create the table if this is just a preview
                _tableExists = false;
                return;
            }

            _db.CreateMigrationsTable();
            _tableExists = true;
        }
    }
}