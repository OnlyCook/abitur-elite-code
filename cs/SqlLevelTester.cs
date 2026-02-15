using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AbiturEliteCode.cs
{
    public class SqlTestResult
    {
        public bool Success { get; set; }
        public string Feedback { get; set; }
        public DataTable ResultTable { get; set; }
    }

    public static class SqlLevelTester
    {
        public static SqlTestResult Run(SqlLevel level, string userQuery)
        {
            try
            {
                using (var connection = new SqliteConnection("Data Source=:memory:"))
                {
                    connection.Open();

                    // execute level setup script
                    using (var setupCmd = connection.CreateCommand())
                    {
                        setupCmd.CommandText = level.SetupScript;
                        setupCmd.ExecuteNonQuery();
                    }

                    string processedQuery = ConvertMysqlToSqlite(userQuery);

                    DataTable userResultTable = null;
                    bool isSelect = processedQuery.Trim().ToUpper().StartsWith("SELECT");
                    int rowsAffected = 0;

                    if (isSelect)
                    {
                        // SELECT
                        userResultTable = ExecuteDbQuery(connection, processedQuery);
                    }
                    else
                    {
                        // UPDATE/INSERT/DELETE
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = processedQuery;
                            rowsAffected = cmd.ExecuteNonQuery();
                        }
                    }

                    // validation logic
                    List<string[]> actualRows = new List<string[]>();

                    string ObjectToInvariantString(object x) // localization fix (de: ',' | us: '.')
                    {
                        if (x == null || x == DBNull.Value) return "NULL";
                        if (x is IFormattable formattable)
                        {
                            return formattable.ToString(null, CultureInfo.InvariantCulture);
                        }
                        return x.ToString();
                    }

                    // 1: user did a select -> validate the result directly
                    if (userResultTable != null)
                    {
                        foreach (DataRow row in userResultTable.Rows)
                        {
                            string[] rowData = row.ItemArray
                                .Select(ObjectToInvariantString)
                                .ToArray();
                            actualRows.Add(rowData);
                        }
                    }
                    // 2: user did update/insert -> run verification query
                    else if (!string.IsNullOrEmpty(level.VerificationQuery))
                    {
                        var verifyDt = ExecuteDbQuery(connection, level.VerificationQuery);
                        foreach (DataRow row in verifyDt.Rows)
                        {
                            string[] rowData = row.ItemArray.Select(ObjectToInvariantString).ToArray();
                            actualRows.Add(rowData);
                        }
                    }

                    bool correct = true;
                    // basic dimensional check
                    if (actualRows.Count != level.ExpectedResult.Count)
                    {
                        correct = false;
                    }
                    else
                    {
                        // deep content check
                        for (int i = 0; i < actualRows.Count; i++)
                        {
                            // row length check
                            if (actualRows[i].Length != level.ExpectedResult[i].Length)
                            {
                                correct = false; break;
                            }

                            for (int j = 0; j < actualRows[i].Length; j++)
                            {
                                if (actualRows[i][j] != level.ExpectedResult[i][j])
                                {
                                    correct = false; break;
                                }
                            }
                            if (!correct) break;
                        }
                    }

                    string msg = correct ? "✓ Richtig! Aufgabe gelöst." : "❌ Das Ergebnis stimmt nicht mit der Erwartung überein.";
                    if (!isSelect && correct) msg += $"\n({rowsAffected} Zeilen betroffen)";

                    return new SqlTestResult
                    {
                        Success = correct,
                        Feedback = msg,
                        ResultTable = userResultTable
                    };
                }
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message.Replace("SQLite Error", "SQL Fehler");
                return new SqlTestResult
                {
                    Success = false,
                    Feedback = errorMsg,
                    ResultTable = null
                };
            }
        }

        private static string ConvertMysqlToSqlite(string query)
        {
            string q = query;

            // finds "SET @name = value;" or "SET @name := value;"
            var varMatches = Regex.Matches(q, @"SET\s+@(\w+)\s*(?::=|=)\s*([^;]+);", RegexOptions.IgnoreCase);
            foreach (Match m in varMatches)
            {
                string varName = m.Groups[1].Value;
                string varValue = m.Groups[2].Value.Trim();

                // remove the SET statement from the query
                q = q.Replace(m.Value, "");

                // replace all occurrences of @varName with the actual value
                // \b ensures we don't replace @year in @years
                q = Regex.Replace(q, "@" + varName + @"\b", varValue);
            }

            // comments
            q = Regex.Replace(q, @"(?<=^|\s)#", "--");

            // -- mysql emulation additions --

            // 1. CONCAT(a, b) -> a || b
            q = Regex.Replace(q, @"CONCAT\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "$1 || $2", RegexOptions.IgnoreCase);

            // 2. YEAR(date) -> strftime('%Y', date)
            q = Regex.Replace(q, @"\bYEAR\s*\(\s*([^)]+)\s*\)", "strftime('%Y', $1)", RegexOptions.IgnoreCase);

            // 3. MONTH(date) -> strftime('%m', date)
            q = Regex.Replace(q, @"\bMONTH\s*\(\s*([^)]+)\s*\)", "strftime('%m', $1)", RegexOptions.IgnoreCase);

            // 4. DAY(date) -> strftime('%d', date)
            q = Regex.Replace(q, @"\bDAY\s*\(\s*([^)]+)\s*\)", "strftime('%d', $1)", RegexOptions.IgnoreCase);

            // 5. DATEDIFF(end, start) -> CAST(julianday(end) - julianday(start) AS INTEGER)
            q = Regex.Replace(q, @"\bDATEDIFF\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "CAST((julianday($1) - julianday($2)) AS INTEGER)", RegexOptions.IgnoreCase);

            // 6. DATE_ADD(date, INTERVAL x DAY) -> date(date, 'x days')
            q = Regex.Replace(q, @"\bDATE_ADD\s*\(\s*([^,]+?)\s*,\s*INTERVAL\s+([+\-]?\d+)\s+DAY\s*\)", "date($1, '$2 days')", RegexOptions.IgnoreCase);

            // date/time (existing)
            q = Regex.Replace(q, @"\bNOW\(\)", "datetime('now')", RegexOptions.IgnoreCase);
            q = Regex.Replace(q, @"\bCURDATE\(\)", "date('now')", RegexOptions.IgnoreCase);

            return q;
        }

        private static DataTable ExecuteDbQuery(SqliteConnection conn, string sql)
        {
            var dt = new DataTable();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                using (var reader = cmd.ExecuteReader())
                {
                    dt.Load(reader);
                }
            }
            return dt;
        }
    }
}