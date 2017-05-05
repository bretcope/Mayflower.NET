using System;

namespace Mayflower
{
    class Record
    {
        public int Id { get; set; }
        public string Filename { get; set; }
        public string Hash { get; set; }
        public DateTime ExecutionDate { get; set; }
        public double DurationMs { get; set; }

        internal static Record Create(Migration migration, TimeSpan duration)
        {
            var r = new Record();
            r.Filename = migration.Filename;
            r.Hash = migration.Hash;
            r.ExecutionDate = DateTime.UtcNow;
            r.DurationMs = duration.TotalMilliseconds;

            return r;
        }
    }
}