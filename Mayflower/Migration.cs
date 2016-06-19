using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mayflower
{
    enum MigrateMode
    {
        Skip,
        Run,
        Rename,
        HashMismatch,
    }

    public class Migration
    {
        static readonly MD5CryptoServiceProvider s_md5Provider = new MD5CryptoServiceProvider();
        static readonly Regex s_lineEndings = new Regex("\r\n|\n\r|\n|\r", RegexOptions.Compiled);

        public List<string> SqlCommands { get; }
        public string Hash { get; }
        public string Filename { get; }
        public bool UseTransaction { get; }

        internal Migration(string filePath, Regex commandSplitter)
        {
            var sql = File.ReadAllText(filePath, Encoding.GetEncoding("iso-8859-1"));
            SqlCommands = commandSplitter.Split(sql).Where(s => s.Trim().Length > 0).ToList();
            Hash = GetHash(sql);
            Filename = Path.GetFileName(filePath);

            UseTransaction = !sql.StartsWith("-- no transaction --");
        }

        internal MigrateMode GetMigrateMode(AlreadyRan alreadyRan)
        {
            MigrationRow row;
            if (alreadyRan.ByFilename.TryGetValue(Filename, out row))
            {
                return row.Hash == Hash ? MigrateMode.Skip : MigrateMode.HashMismatch;
            }

            if (alreadyRan.ByHash.TryGetValue(Hash, out row))
            {
                return MigrateMode.Rename;
            }

            return MigrateMode.Run;
        }

        static string GetHash(string str)
        {
            var normalized = NormalizeLineEndings(str);
            var inputBytes = Encoding.Unicode.GetBytes(normalized);

            byte[] hashBytes;
            lock (s_md5Provider)
            {
                hashBytes = s_md5Provider.ComputeHash(inputBytes);
            }

            return new Guid(hashBytes).ToString();
        }

        static string NormalizeLineEndings(string str)
        {
            return s_lineEndings.Replace(str, "\n");
        }
    }
}