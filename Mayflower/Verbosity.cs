namespace Mayflower
{
    /// <summary>
    /// Enumeration of logging verbosity levels which control how detailed Mayflower's output is.
    /// </summary>
    public enum Verbosity
    {
        /// <summary>
        /// Only high-level actions are printed.
        /// </summary>
        Minimal = 5,
        /// <summary>
        /// Includes high-level actions as well as setup information and results from each SQL command.
        /// </summary>
        Normal = 10,
        /// <summary>
        /// Prints information about each action Mayflower takes, including reading migration files and skipped migrations.
        /// </summary>
        Detailed = 15,
        /// <summary>
        /// Highest level of detail. May include trace events and error stack traces.
        /// </summary>
        Debug = 20,
    }
}