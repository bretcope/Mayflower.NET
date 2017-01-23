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
        AutoRun,
    }

    public class Migration
    {
        static readonly MD5 s_md5 = MD5.Create();
        static readonly Regex s_lineEndings = new Regex("\r\n|\n|\r", RegexOptions.Compiled);

        public List<string> SqlCommands { get; }
        public string Hash { get; }
        public string Filename { get; }
        public bool UseTransaction { get; }
        public bool AutoRunIfChanged { get; }

        internal Migration(string filePath, string[] autoRunPrefixes, Regex commandSplitter)
        {
            var sql = File.ReadAllText(filePath, Encoding.GetEncoding("iso-8859-1"));
            SqlCommands = commandSplitter.Split(sql).Where(s => s.Trim().Length > 0).ToList();
            Hash = GetHash(sql);
            Filename = Path.GetFileName(filePath);

            UseTransaction = !sql.StartsWith("-- no transaction --");

            var autoRun = false;
            foreach (var prefix in autoRunPrefixes)
            {
                if (Filename.StartsWith(prefix))
                {
                    autoRun = true;
                    break;
                }
            }

            AutoRunIfChanged = autoRun;
        }

        internal MigrateMode GetMigrateMode(AlreadyRan alreadyRan)
        {
            MigrationRow row;
            if (alreadyRan.ByFilename.TryGetValue(Filename, out row))
            {
                if (row.Hash == Hash)
                    return MigrateMode.Skip;

                if (AutoRunIfChanged)
                    return MigrateMode.AutoRun;

                return MigrateMode.HashMismatch;
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
            lock (s_md5)
            {
                hashBytes = s_md5.ComputeHash(inputBytes);
            }

            return new Guid(hashBytes).ToString();
        }

        static string NormalizeLineEndings(string str)
        {
            return s_lineEndings.Replace(str, "\n");
        }
    }
}