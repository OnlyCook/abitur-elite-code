using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace AbiturEliteCode.cs;

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

                // pass the connection to converter
                string processedQuery = ConvertMysqlToSqlite(connection, userQuery);

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
                    // fix dml spoofing edge cases
                    string upperQuery = processedQuery.Trim().ToUpper();
                    string levelTitle = level.Title ?? "";
                    bool taskIsInsert = levelTitle.Contains("INSERT") || levelTitle.Contains("Einfügen");
                    bool taskIsUpdate = levelTitle.Contains("UPDATE") || levelTitle.Contains("Ändern");
                    bool taskIsDelete = levelTitle.Contains("DELETE") || levelTitle.Contains("Löschen") ||
                                        levelTitle.Contains("Stornierung");

                    if (taskIsUpdate && (upperQuery.Contains("DELETE") || upperQuery.Contains("INSERT")))
                        return new SqlTestResult
                        {
                            Success = false,
                            Feedback = "❌ Umgehung erkannt: Bitte nutze UPDATE, um die Daten zu ändern.",
                            ResultTable = null
                        };
                    if (taskIsInsert && (upperQuery.Contains("UPDATE") || upperQuery.Contains("DELETE")))
                        return new SqlTestResult
                        {
                            Success = false,
                            Feedback = "❌ Umgehung erkannt: Bitte nutze INSERT, um die Daten hinzuzufügen.",
                            ResultTable = null
                        };
                    if (taskIsDelete && (upperQuery.Contains("INSERT") || upperQuery.Contains("UPDATE")))
                        return new SqlTestResult
                        {
                            Success = false,
                            Feedback = "❌ Umgehung erkannt: Bitte nutze DELETE, um die Daten zu löschen.",
                            ResultTable = null
                        };

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
                    if (x is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
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

                    userResultTable = verifyDt;
                }

                bool correct = true;
                string errorFeedback = "❌ Das Ergebnis stimmt nicht mit der Erwartung überein.";

                // column name verification
                if (isSelect && userResultTable != null)
                {
                    var expectedSchema = level.ExpectedSchema;

                    // determine which source to use
                    int expectedCount = expectedSchema.Count;

                    if (expectedCount > 0)
                    {
                        if (userResultTable.Columns.Count != expectedCount)
                        {
                            correct = false;
                            errorFeedback =
                                $"❌ Falsche Spaltenanzahl. Erwartet: {expectedCount}, Erhalten: {userResultTable.Columns.Count}";
                        }
                        else
                        {
                            for (int i = 0; i < expectedCount; i++)
                            {
                                string userColName = userResultTable.Columns[i].ColumnName;

                                if (expectedSchema != null && expectedSchema.Count > 0)
                                {
                                    var expectedCol = expectedSchema[i];
                                    if (expectedCol.StrictName && !userColName.Equals(expectedCol.Name,
                                            StringComparison.OrdinalIgnoreCase))
                                    {
                                        correct = false;
                                        errorFeedback =
                                            $"❌ Falscher Spaltenname an Position {i + 1}. Erwartet: '{expectedCol.Name}', Erhalten: '{userColName}'";
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                if (correct)
                {
                    // basic dimensional check
                    if (actualRows.Count != level.ExpectedResult.Count)
                        correct = false;
                    else
                        // deep content check
                        for (int i = 0; i < actualRows.Count; i++)
                        {
                            if (actualRows[i].Length != level.ExpectedResult[i].Length)
                            {
                                correct = false;
                                break;
                            }

                            for (int j = 0; j < actualRows[i].Length; j++)
                            {
                                string expectedCell = level.ExpectedResult[i][j] ?? "";
                                if (expectedCell == "") expectedCell = "NULL";

                                if (actualRows[i][j] != expectedCell)
                                {
                                    correct = false;
                                    break;
                                }
                            }

                            if (!correct) break;
                        }
                }

                string msg = correct ? "✓ Richtig! Aufgabe gelöst." : errorFeedback;
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

    public static string ConvertMysqlToSqlite(SqliteConnection conn, string query)
    {
        string q = query;

        // find "SET @name = value;" or "SET @name := value;"
        var varMatches = Regex.Matches(q, @"SET\s+@(\w+)\s*(?::=|=)\s*([^;]+);", RegexOptions.IgnoreCase);
        foreach (Match m in varMatches)
        {
            string varName = m.Groups[1].Value;
            string varValue = m.Groups[2].Value.Trim();

            // evaluate subquery if present
            if (varValue.StartsWith("(") && varValue.EndsWith(")"))
            {
                string subQuery = varValue.Substring(1, varValue.Length - 2).Trim();
                if (subQuery.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = subQuery;
                        var result = cmd.ExecuteScalar();

                        if (result == null || result == DBNull.Value)
                            varValue = "NULL";
                        else if (result is string s)
                            varValue = $"'{s.Replace("'", "''")}'"; // escape string
                        else if (result is IFormattable formattable)
                            varValue = formattable.ToString(null, CultureInfo.InvariantCulture);
                        else
                            varValue = result.ToString();
                    }
            }

            // remove the 'SET' statement from the query
            q = q.Replace(m.Value, "");

            // replace all occurrences of @varName with the actual value
            q = Regex.Replace(q, "@" + varName + @"\b", varValue);
        }

        // comments
        q = Regex.Replace(q, @"(?<=^|\s)#", "--");

        // -- mysql emulation additions --

        // remove aliases from update
        var updateAliasMatch = Regex.Match(q, @"^\s*UPDATE\s+([a-zA-Z0-9_]+)\s+([a-zA-Z0-9_]+)\s+SET",
            RegexOptions.IgnoreCase);
        if (updateAliasMatch.Success)
        {
            string alias = updateAliasMatch.Groups[2].Value;
            if (!alias.Equals("SET", StringComparison.OrdinalIgnoreCase))
            {
                string tableName = updateAliasMatch.Groups[1].Value;
                q = Regex.Replace(q, $@"^\s*UPDATE\s+{tableName}\s+{alias}\s+SET", $"UPDATE {tableName} SET",
                    RegexOptions.IgnoreCase);
                q = Regex.Replace(q, $@"\b{alias}\.", "", RegexOptions.IgnoreCase);
            }
        }

        // remove aliases from delete
        var deleteAliasMatch = Regex.Match(q, @"^\s*DELETE\s+FROM\s+([a-zA-Z0-9_]+)\s+([a-zA-Z0-9_]+)\s+WHERE",
            RegexOptions.IgnoreCase);
        if (deleteAliasMatch.Success)
        {
            string alias = deleteAliasMatch.Groups[2].Value;
            if (!alias.Equals("WHERE", StringComparison.OrdinalIgnoreCase))
            {
                string tableName = deleteAliasMatch.Groups[1].Value;
                q = Regex.Replace(q, $@"^\s*DELETE\s+FROM\s+{tableName}\s+{alias}\s+WHERE",
                    $"DELETE FROM {tableName} WHERE", RegexOptions.IgnoreCase);
                q = Regex.Replace(q, $@"\b{alias}\.", "", RegexOptions.IgnoreCase);
            }
        }

        // transforms "INSERT INTO table SET col1=val1, col2=val2" -> "INSERT INTO table (col1, col2) VALUES (val1, val2)"
        // also supports optional aliases
        var insertSetMatch = Regex.Match(q, @"^\s*INSERT\s+INTO\s+(\w+)(?:\s+(\w+))?\s+SET\s+(.+)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (insertSetMatch.Success)
        {
            string tableName = insertSetMatch.Groups[1].Value;
            string alias = insertSetMatch.Groups[2].Success ? insertSetMatch.Groups[2].Value : null;
            string assignments = insertSetMatch.Groups[3].Value;

            if (!string.IsNullOrEmpty(alias) && !alias.Equals("SET", StringComparison.OrdinalIgnoreCase))
                assignments = Regex.Replace(assignments, $@"\b{alias}\.", "", RegexOptions.IgnoreCase);

            var columns = new List<string>();
            var values = new List<string>();

            // regex to capture "col = val" pairs
            var pairs = Regex.Matches(assignments, @"(\w+)\s*=\s*('[^']*'|[^,]+)");

            foreach (Match m in pairs)
            {
                columns.Add(m.Groups[1].Value);
                values.Add(m.Groups[2].Value.Trim());
            }

            if (columns.Count > 0)
                // rewrite the query structure entirely for sqlite
                q = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
        }

        // 1. CONCAT(a, b) -> a || b
        q = Regex.Replace(q, @"CONCAT\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "$1 || $2", RegexOptions.IgnoreCase);

        // 2. YEAR(date) -> strftime('%Y', date)
        q = Regex.Replace(q, @"\bYEAR\s*\(\s*([^)]+)\s*\)", "strftime('%Y', $1)", RegexOptions.IgnoreCase);

        // 3. MONTH(date) -> strftime('%m', date)
        q = Regex.Replace(q, @"\bMONTH\s*\(\s*([^)]+)\s*\)", "strftime('%m', $1)", RegexOptions.IgnoreCase);

        // 4. DAY(date) -> strftime('%d', date)
        q = Regex.Replace(q, @"\bDAY\s*\(\s*([^)]+)\s*\)", "strftime('%d', $1)", RegexOptions.IgnoreCase);

        // 5. DATEDIFF(end, start) -> CAST(julianday(end) - julianday(start) AS INTEGER)
        q = Regex.Replace(q, @"\bDATEDIFF\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)",
            "CAST((julianday($1) - julianday($2)) AS INTEGER)", RegexOptions.IgnoreCase);

        // 6. DATE_ADD(date, INTERVAL x DAY) -> date(date, 'x days')
        q = Regex.Replace(q, @"\bDATE_ADD\s*\(\s*([^,]+?)\s*,\s*INTERVAL\s+([+\-]?\d+)\s+DAY\s*\)",
            "date($1, '$2 days')", RegexOptions.IgnoreCase);

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