using System;
using System.IO;

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
            return string.Compare(a.FileName, b.FileName, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}