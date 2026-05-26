// ────────────────────────────────────────────────────────────
//  Project     : aa Mcp Server
//  Author      : NK
//  Date        : 26-05-2026
// ────────────────────────────────────────────────────────────
// Copyright 2026 The aaMcpServer Authors
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Data;
using System.Data.SqlClient;

namespace aaMcpServer.Historian
{
    /// <summary>
    /// Thin data-access layer over the AVEVA Historian Runtime database. Opens a
    /// short-lived SQL Server connection per query (SQL Server auth) and returns a
    /// <see cref="QueryResult"/>. Query construction lives in the individual tools;
    /// this class just executes SQL safely and caps the row count.
    /// </summary>
    public sealed class HistorianClient
    {
        private readonly ServiceConfig _cfg;
        private readonly string _connectionString;

        public HistorianClient(ServiceConfig cfg)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _connectionString = cfg.BuildConnectionString();
        }

        /// <summary>
        /// Runs a SELECT and returns the rows, stopping once <paramref name="maxRows"/>
        /// have been read (the result is then flagged as truncated).
        /// </summary>
        public QueryResult Run(string sql, int maxRows, SqlParameter[] parameters = null)
        {
            var result = new QueryResult();

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = _cfg.CommandTimeoutSeconds;
                if (parameters != null) cmd.Parameters.AddRange(parameters);

                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                        result.Columns.Add(reader.GetName(i));

                    while (reader.Read())
                    {
                        if (result.Rows.Count >= maxRows)
                        {
                            result.Truncated = true;
                            break;
                        }

                        var row = new object[reader.FieldCount];
                        reader.GetValues(row);
                        result.Rows.Add(row);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Verifies that the configured credentials can open the Runtime database.
        /// Used at start-up to fail fast with a clear message. Returns null on
        /// success or the error message on failure.
        /// </summary>
        public string TestConnection()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT 1", conn))
                        cmd.ExecuteScalar();
                }
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
