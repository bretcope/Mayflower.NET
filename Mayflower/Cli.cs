using System;
using System.Collections.Generic;
using System.IO;

namespace Mayflower
{
    public static class Cli
    {
        const string CONN = "--conn";
        const string DB = "--db";
        const string SERVER = "--server";
        const string DIR = "--dir";
        const string TABLE = "--table";
        const string TIMEOUT = "--timeout";
        const string PREVIEW = "--preview";
        const string GLOBAL = "--global";
        const string FORCE = "--force";
        const string COUNT = "--count";
        const string VERSION = "--version";
        const string HELP = "--help";

        public static string ExeName { get; set; } = "mayflower";

        public static bool Execute(string[] args, TextWriter output)
        {
            try
            {
                var argsDictionary = GetArgumentsDictionary(args);

                if (argsDictionary.Count == 0 || argsDictionary.ContainsKey(HELP))
                {
                    WriteHelp(output);
                    return true;
                }

                if (argsDictionary.ContainsKey(VERSION))
                {
                    output.WriteLine("2.0.0-beta"); // todo: print real version
                    return true;
                }

                var options = CreateOptions(argsDictionary);

                if (argsDictionary.ContainsKey(COUNT))
                {
                    var count = Migrator.GetOutstandingMigrationsCount(options);
                    output.WriteLine(count + " outstanding migrations");
                    output.WriteLine();
                    return true;
                }

                var result = Migrator.RunOutstandingMigrations(options);
                return result.Success;
            }
            catch (Exception ex)
            {
                output.WriteLine(ex);
                output.WriteLine();
                WriteHelp(output);

                return false;
            }
        }

        static Options CreateOptions(Dictionary<string, string> args)
        {
            var options = new Options();

            if (args.TryGetValue(CONN, out var conn))
                options.ConnectionString = conn;

            if (args.TryGetValue(DB, out var db))
                options.Database = db;

            if (args.TryGetValue(SERVER, out var server))
                options.Server = server;

            if (args.TryGetValue(DIR, out var dir))
                options.Directory = dir;

            if (args.TryGetValue(TABLE, out var table))
                options.MigrationsTable = table;

            if (args.TryGetValue(TIMEOUT, out var timeoutStr))
            {
                if (int.TryParse(timeoutStr, out var timeout))
                    options.CommandTimeout = timeout;
                else 
                    throw new Exception("--timeout argument value must be an integer.");
            }

            if (args.ContainsKey(PREVIEW))
                options.IsPreview = true;

            if (args.ContainsKey(GLOBAL))
                options.UseGlobalTransaction = true;

            if (args.ContainsKey(FORCE))
                options.Force = true;

            return options;
        }

        static void WriteHelp(TextWriter output)
        {
            output.Write($@"Usage: {ExeName} [OPTIONS]+
  Runs all *.sql files in the directory {DIR} <directory>.
  The databse connection can be specified using a full connection string with
  {CONN}, or Mayflower can generate an integrated auth connection string using
  the {DB} and optional {SERVER} arguments.

OPTIONS:

  {CONN} <value>        A SQL Server connection string. For integrated auth, you
                          can use --database and --server instead.
  {DB} <value>          Generates an integrated auth connection string for the
                          specified database.
  {SERVER} <value>      Generates an integrated auth connection string with the
                          specified server (default: localhost).
  {DIR} <value>         The directory containing your .sql migration files
                          (defaults to current working directory).
  {TABLE} <value>       Name of the table used to track migrations (default:
                          Migrations).
  {TIMEOUT} <value>     Command timeout duration in seconds (default: 30).
  {PREVIEW}             Run outstanding migrations, but roll them back.
  {GLOBAL}              Run all outstanding migrations in a single transaction,
                          if possible.
  {FORCE}               Will rerun modified migrations.
  {COUNT}               Print the number of outstanding migrations.
  {VERSION}             Print the Mayflower version number.
  {HELP}                Shows this help message.
");
        }

        static Dictionary<string, string> GetArgumentsDictionary(string[] args)
        {
            var argsDictionary = new Dictionary<string, string>();

            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];

                if (argsDictionary.ContainsKey(a))
                    throw new Exception($"Duplicate argument \"{a}\"");

                switch (a)
                {
                    case CONN:
                    case DB:
                    case SERVER:
                    case DIR:
                    case TABLE:
                    case TIMEOUT:
                        var value = i + 1 < args.Length ? args[i + 1] : null;
                        if (value == null || value.StartsWith("--"))
                            throw new Exception($"Argument \"{a}\" is missing a value.");

                        argsDictionary[a] = value;
                        break;
                    case PREVIEW:
                    case GLOBAL:
                    case FORCE:
                    case COUNT:
                    case VERSION:
                    case HELP:
                        argsDictionary[a] = null;
                        break;
                    default:
                        throw new Exception($"Unknown argument \"{a}\"");
                }
            }

            return argsDictionary;
        }
    }
}