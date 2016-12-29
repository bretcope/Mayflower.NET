using System.Data;

namespace Mayflower
{
    static class SqlExtensions
    {
        internal static IDbCommand NewCommand(this IDbConnection conn, string sql, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = transaction;

            if (commandTimeout.HasValue)
                cmd.CommandTimeout = commandTimeout.Value;

            return cmd;
        }

        internal static void AddParameter(this IDbCommand cmd, string name, object value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            cmd.Parameters.Add(param);
        }
    }
}