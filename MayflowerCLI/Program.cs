using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mayflower;
using NDesk.Options;

namespace MayflowerCLI
{
    class Program
    {
        enum Command
        {
            None,
            RunMigrations,
            GetCount,
        }

        static void Main(string[] args)
        {
            SetupAssemblyResolving();
            Run(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Run(string[] args)
        {
            Options options;
            var cmd = TryParseArgs(args, out options);

            switch (cmd)
            {
                case Command.RunMigrations:
                    var result = Migrator.RunOutstandingMigrations(options);
                    if (!result.Success)
                        Environment.Exit(1);
                    break;
                case Command.GetCount:
                    var count = Migrator.GetOutstandingMigrationsCount(options);
                    Console.WriteLine(count + " outstanding migrations");
                    Console.WriteLine();
                    break;
            }

            Environment.Exit(0);
        }

        static Command TryParseArgs(string[] args, out Options options)
        {
            var showHelp = false;
            var showVersion = false;
            var getCount = false;
            var optionsTmp = options = new Options();

            var optionSet = new OptionSet()
            {
                { "h|help", "Shows this help message.", v => showHelp= v != null },
                {"c|connection=", "A SQL Server connection string. For integrated auth, you can use --database and --server instead.", v => optionsTmp.ConnectionString = v },
                {"d|database=", "Generates an integrated auth connection string for the specified database.", v => optionsTmp.Database = v },
                {"s|server=", "Generates an integrated auth connection string with the specified server (default: localhost).", v=> optionsTmp.Server = v },
                {"f|folder=", "The folder containing your .sql migration files (defaults to current working directory).", v => optionsTmp.MigrationsFolder = v },
                {"timeout=", "Command timeout duration in seconds (default: 30)", v => optionsTmp.CommandTimeout = int.Parse(v) },
                {"preview", "Run outstanding migrations, but roll them back.", v => optionsTmp.IsPreview = v != null },
                {"global", "Run all outstanding migrations in a single transaction, if possible.", v => optionsTmp.UseGlobalTransaction = v != null },
                {"table=", "Name of the table used to track migrations (default: Migrations)", v => optionsTmp.MigrationsTable = v },
                {"force", "Will rerun modified migrations.", v => optionsTmp.Force = v != null },
                {"version", "Print version number.", v => showVersion = v != null },
                { "count", "Print the number of outstanding migrations.", v => getCount = v != null },
            };

            try
            {
                optionSet.Parse(args);
                options.Output = Console.Out;

                if (!showHelp && !showVersion)
                    optionsTmp.AssertValid();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                showHelp = true;
            }

            if (showVersion)
            {
                Console.WriteLine("Mayflower.NET - Version " + Migrator.GetVersion());
                Console.WriteLine();
                return Command.None;
            }

            if (showHelp)
            {
                ShowHelpMessage(optionSet);
                return Command.None;
            }

            return getCount ? Command.GetCount : Command.RunMigrations;
        }

        static void ShowHelpMessage(OptionSet optionSet)
        {
            Console.WriteLine("Usage: mayflower [OPTIONS]+");
            Console.WriteLine("  Runs all *.sql files in the directory --dir=<directory>.");
            Console.WriteLine("  The databse connection can be specified using a full connection string with --connection,");
            Console.WriteLine("  or Mayflower can generate an integrated auth connection string using the --database and");
            Console.WriteLine("  optional --server arguments.");
            Console.WriteLine();
            optionSet.WriteOptionDescriptions(Console.Out);
        }

        static void SetupAssemblyResolving()
        {
            // Load dependant assemblies from embedded resources so that we don't have to distribute them separate from the exe.
            // https://blogs.msdn.microsoft.com/microsoft_press/2010/02/03/jeffrey-richter-excerpt-2-from-clr-via-c-third-edition/
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var resourceName = "MayflowerCLI." + new AssemblyName(args.Name).Name + ".dll";

                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    var assemblyData = new byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };
        }
    }
}
