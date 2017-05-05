using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mayflower
{
    /// <summary>
    /// The top-level class for Mayflower. It is instantiated from a directory of migration files using the static <see cref="Create"/> method. It can then be
    /// used to perform actions (such as migrate) on one or more databases.
    /// </summary>
    public class Migrator
    {
        readonly Logger _logger;
        
        /// <summary>
        /// An array of migration objects which each represent a migration file.
        /// </summary>
        public Migration[] Migrations { get; }

        Migrator(Migration[] migrations, Logger logger)
        {
            _logger = logger;
            Migrations = migrations;
        }

        /// <summary>
        /// Creates a new migrator instance which can then be used to migrate one or more databases.
        /// </summary>
        /// <param name="directory">The directory of the migration files (*.sql).</param>
        /// <param name="verbosity">How detailed the output of commands should be.</param>
        /// <param name="format">Set this if you are using a build system which has a special output format.</param>
        /// <param name="output">Where to send the output of commands. If null, output will be sent to Console.Out.</param>
        /// <param name="autoRunPrefixes">A comma-delimited </param>
        public static Migrator Create(
            string directory,
            Verbosity verbosity = Verbosity.Normal,
            OutputFormat format = OutputFormat.Plain,
            TextWriter output = null,
            string autoRunPrefixes = "SP,AUTORUN")
        {
            var logger = new Logger(output, verbosity, format);
            var migrations = GetMigrationsFromDirectory(directory, autoRunPrefixes, logger);

            return new Migrator(migrations, logger);
        }

        /// <summary>
        /// Runs all unrun migrations for each database database.
        /// </summary>
        /// <param name="databases">A list of databases to run the migrations on.</param>
        /// <param name="preview">
        /// If true, all migrations will be run in a global transaction and then rolled back. Any migration marked as "no transaction" will be skipped.
        /// </param>
        /// <param name="globalTransaction">
        /// If true, Mayflower will try to run all migrations in a global transaction. However, this may still be split up if any migrations are marked as "no
        /// transaction".
        /// </param>
        /// <param name="force">If true, any migration which has changed will be re-run.</param>
        /// <param name="parallel">If true, the migrations will be run in parallel on multiple databases.</param>
        /// <param name="stopOnFirstFailure">If true, Mayflower will not attempt to migrate any database after one has failed.</param>
        /// <returns>True if all migrations succeeded, otherwise false.</returns>
        public bool RunMigrations(
            IList<Database> databases,
            bool preview = false,
            bool globalTransaction = false,
            bool force = false,
            bool parallel = true,
            bool stopOnFirstFailure = true)
        {
            if (databases == null)
                throw new ArgumentNullException(nameof(databases));

            var count = databases.Count;
            _logger.Log(Verbosity.Normal, $"Migrating {count} database{(count == 1 ? "" : "s")}");

            if (preview)
                _logger.Log(Verbosity.Normal, "Preview mode (all transactions will be rolled back)");

            if (databases.Count == 0)
                return true;

            var aggregateResult = true;

            if (parallel && databases.Count > 1)
            {
                var parallelConns = databases.AsParallel().WithDegreeOfParallelism(Environment.ProcessorCount).WithExecutionMode(ParallelExecutionMode.ForceParallelism);

                parallelConns.ForAll(
                    conn =>
                    {
                        if (!aggregateResult && stopOnFirstFailure)
                            return;

                        var result = RunMigrationsImpl(conn, preview, globalTransaction, force, true);

                        if (!result)
                            aggregateResult = false;
                    });
            }
            else
            {
                foreach (var conn in databases)
                {
                    var result = RunMigrationsImpl(conn, preview, globalTransaction, force, false);

                    if (!result)
                    {
                        aggregateResult = false;
                        if (stopOnFirstFailure)
                            break;
                    }
                }
            }

            return aggregateResult;
        }

        /// <summary>
        /// Runs all unrun migrations for each database.
        /// </summary>
        /// <param name="database">The database to run the migrations on.</param>
        /// <param name="preview">
        /// If true, all migrations will be run in a global transaction and then rolled back. Any migration marked as "no transaction" will be skipped.
        /// </param>
        /// <param name="globalTransaction">
        /// If true, Mayflower will try to run all migrations in a global transaction. However, this may still be split up if any migrations are marked as "no
        /// transaction".
        /// </param>
        /// <param name="force">If true, any migration which has changed will be re-run.</param>
        /// <returns>True if all migrations succeeded, otherwise false.</returns>
        public bool RunMigrations(Database database, bool preview = false, bool globalTransaction = false, bool force = false)
        {
            if (preview)
                _logger.Log(Verbosity.Normal, "Preview mode (all transactions will be rolled back)");

            return RunMigrationsImpl(database, preview, globalTransaction, force, false);
        }

        static Migration[] GetMigrationsFromDirectory(string directory, string autoRunPrefixes, ILogger logger)
        {
            using (var dirLogger = logger.CreateNestedLogger("Reading Migration Files", false, Verbosity.Detailed))
            {
                string[] prefixes;
                if (autoRunPrefixes != null)
                {
                    dirLogger.Log(Verbosity.Detailed, $"Autorun prefixes: {autoRunPrefixes}");
                    prefixes = autoRunPrefixes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    dirLogger.Log(Verbosity.Detailed, "Autorun prefixes disabled");
                    prefixes = Array.Empty<string>();
                }

                dirLogger.Log(Verbosity.Debug, $"Reading from directory {directory}");
                var filePaths = Directory.GetFiles(directory, "*.sql");
                var migrations = new Migration[filePaths.Length];

                dirLogger.Log(Verbosity.Normal, $"Found {filePaths.Length} migration files in {directory}");

                for (var i = 0; i < filePaths.Length; i++)
                {
                    var filePath = filePaths[i];
                    migrations[i] = Migration.CreateFromFile(filePath, prefixes, dirLogger);
                }

                Array.Sort(migrations, MigrationSorter);

                return migrations;
            }
        }

        static int MigrationSorter(Migration a, Migration b)
        {
            return string.Compare(a.Filename, b.Filename, StringComparison.CurrentCultureIgnoreCase);
        }

        bool RunMigrationsImpl(Database db, bool preview, bool globalTransaction, bool force, bool bufferOutput)
        {
            using (var logger = _logger.CreateNestedLogger($"Migrating Database {db.DatabaseName}", bufferOutput, Verbosity.Normal))
            {
                try
                {
                    using (var context = ConnectionContext.TryOpen(db, logger))
                    {
                        if (context == null)
                            return false;

                        if (preview)
                        {
                            logger.Log(Verbosity.Debug, "Creating global transaction for preview mode");
                            context.BeginTransaction(logger);
                        }

                        context.EnsureMigrationsTableExists(logger);
                        context.LoadMigrationRecords(logger);

                        //

                        // loop through Migrations and execute as necessary

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }

        void ExecuteMigration(Migration migration, bool global)
        {
            //
        }
    }
}