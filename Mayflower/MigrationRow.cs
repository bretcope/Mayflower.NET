using System;

namespace Mayflower
{
    public class MigrationRow
    {
        public int Id { get; set; }
        public string Filename { get; set; }
        public string Hash { get; set; }
        public DateTime ExecutionDate { get; set; }
        public int Duration { get; set; }
    }
}