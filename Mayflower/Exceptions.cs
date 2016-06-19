using System;

namespace Mayflower
{
    public class MigrationChangedException : Exception
    {
        internal MigrationChangedException(Migration migration) : base($"{migration.Filename} has been modified since it was run. Use --force to re-run it.")
        {
        }
    }
}