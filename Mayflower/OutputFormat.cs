namespace Mayflower
{
    /// <summary>
    /// Enumeration of different console output formats.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// No special formatting.
        /// </summary>
        Plain = 0,
        /// <summary>
        /// Includes TeamCity-specific formatting for nested output blocks. For more details, see:
        /// https://confluence.jetbrains.com/display/TCD10/Build+Script+Interaction+with+TeamCity
        /// </summary>
        TeamCity = 1,
    }
}