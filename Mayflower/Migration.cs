using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mayflower
{
    /// <summary>
    /// Represents a migration file.
    /// </summary>
    public class Migration
    {
        static readonly SHA256 s_sha256 = SHA256.Create();
        static readonly Regex s_lineEndings = new Regex("\r\n|\n|\r", RegexOptions.Compiled);
        static readonly Regex s_commandSplitter = new Regex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// The name of the migration file.
        /// </summary>
        public string FileName { get; }
        /// <summary>
        /// The full name of the migration file, which may include its path.
        /// </summary>
        public string FullFileName { get; }
        /// <summary>
        /// A hex representation of the file's SHA256 Hash.
        /// </summary>
        public string Hash { get; }
        /// <summary>
        /// An array of the SQL commands which were present in the file.
        /// </summary>
        public List<string> SqlCommands { get; }
        /// <summary>
        /// If true, the migration's commands will be run inside a migration.
        /// </summary>
        public bool UseTransaction { get; }
        /// <summary>
        /// If true, the migration will be re-run anytime the file is changed.
        /// </summary>
        public bool AutoRunIfChanged { get; }

        Migration(string fileName, string fillFileName, string hash, List<string> commands, bool useTransaction, bool autoRunIfChanged)
        {
            FileName = fileName;
            FullFileName = FullFileName;
            Hash = hash;
            SqlCommands = commands;
            UseTransaction = useTransaction;
            AutoRunIfChanged = autoRunIfChanged;
        }

        internal static Migration CreateFromFile(string filePath, string[] autoRunPrefixes, ILogger logger)
        {
            var fileName = Path.GetFileName(filePath);

            using (var fileLogger = logger.CreateNestedLogger(fileName, false, Verbosity.Debug))
            {
                fileLogger.Log(Verbosity.Debug, $"Reading file {filePath}");
                var fileBody = File.ReadAllText(filePath, Encoding.UTF8);

                var hash = CalculateHash(fileBody);
                fileLogger.Log(Verbosity.Detailed, $"Found migration {fileName} ({hash})");

                var commands = new List<string>();
                foreach (var cmd in s_commandSplitter.Split(fileBody))
                {
                    var trimmed = cmd.Trim();
                    if (trimmed.Length > 0)
                        commands.Add(trimmed);
                }

                fileLogger.Log(Verbosity.Debug, $"{commands.Count} commands found");

                var useTransaction = !fileBody.StartsWith("-- no transaction --");
                if (!useTransaction)
                    fileLogger.Log(Verbosity.Debug, "Transaction disabled");

                var autoRun = false;
                foreach (var prefix in autoRunPrefixes)
                {
                    if (fileName.StartsWith(prefix))
                    {
                        autoRun = true;
                        break;
                    }
                }

                if (autoRun)
                    fileLogger.Log(Verbosity.Debug, "Autorun if changed enabled");

                return new Migration(fileName, filePath, hash, commands, useTransaction, autoRun);
            }
        }

        static string CalculateHash(string body)
        {
            // normalize line endings
            var normalized = s_lineEndings.Replace(body, "\n");
            var inputBytes = Encoding.Unicode.GetBytes(normalized);

            byte[] hashBytes;
            lock (s_sha256)
            {
                hashBytes = s_sha256.ComputeHash(inputBytes);
            }

            return ToHex(hashBytes);
        }

        static unsafe string ToHex(byte[] bytes)
        {
            var len = bytes.Length * 2;
            var cStr = stackalloc char[len];

            var ptr = cStr;
            foreach (var b in bytes)
            {
                int nibble;

                nibble = b >> 4;
                *ptr = (char)((nibble < 0xa ? '0' : ('a' - 10)) + nibble);
                ptr++;

                nibble = b & 0xf;
                *ptr = (char)((nibble < 0xa ? '0' : ('a' - 10)) + nibble);
                ptr++;
            }

            return new string(cStr, 0, len);
        }
    }
}