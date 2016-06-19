using System.Collections.Generic;

namespace Mayflower
{
    class AlreadyRan
    {
        public int Count => ByFilename.Count;
        public Dictionary<string, MigrationRow> ByFilename { get; } = new Dictionary<string, MigrationRow>();
        public Dictionary<string, MigrationRow> ByHash { get; } = new Dictionary<string, MigrationRow>();
        public MigrationRow Last { get; }

        internal AlreadyRan(IEnumerable<MigrationRow> rows)
        {
            MigrationRow last = null;
            foreach (var row in rows)
            {
                ByFilename[row.Filename] = row;
                ByHash[row.Hash] = row;
                last = row;
            }

            Last = last;
        }
    }
}