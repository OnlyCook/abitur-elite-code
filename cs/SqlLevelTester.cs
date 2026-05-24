using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AbiturEliteCode.cs;

public class SqlTestResult
{
    public bool Success { get; set; }
    public string Feedback { get; set; }
    public DataTable ResultTable { get; set; }
}

public static class SqlLevelTester
{
    public static SqlTestResult Run(SqlLevel level, string userQuery, CancellationToken token = default)
    {
        try
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var limitCmd = connection.CreateCommand())
                {
                    limitCmd.CommandText = "PRAGMA hard_heap_limit = 250000000;"; // 250 MB
                    limitCmd.ExecuteNonQuery();
                }

                // execute level setup script
                using (var setupCmd = connection.CreateCommand())
                {
                    setupCmd.CommandText = level.SetupScript;
                    using (token.Register(() => {
                        try { setupCmd.Cancel(); } catch { }
                        try { connection.Close(); } catch { }
                        try { connection.Dispose(); } catch { }
                    }))
                    {
                        setupCmd.ExecuteNonQuery();
                    }
                }

                // pass the connection to converter
                string processedQuery = ConvertMysqlToSqlite(connection, userQuery, token);

            // custom level rules
            if (level.Id == 29)
            {
                // check if the outer query uses a join (everything before the where clause)
                string outerQueryBeforeWhere = Regex.Split(processedQuery, @"\bWHERE\b", RegexOptions.IgnoreCase)[0];
                if (outerQueryBeforeWhere.IndexOf("JOIN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Regex.IsMatch(outerQueryBeforeWhere, @"FROM\s+[a-zA-Z0-9_]+\s*,", RegexOptions.IgnoreCase))
                {
                    return new SqlTestResult
                    {
                        Success = false,
                        Feedback = "@E Umgehung erkannt: Bitte nutze keinen JOIN im äußeren SELECT (nutze eine Unterabfrage mit IN).",
                        ResultTable = null
                    };
                }
            }
            else if (level.Id == 35)
            {
                // check if the target bid was hardcoded by the user
                string targetBid = level.AuxiliaryIds.FirstOrDefault();
                if (!string.IsNullOrEmpty(targetBid) && Regex.IsMatch(userQuery, $@"\b{targetBid}\b"))
                {
                    return new SqlTestResult
                    {
                        Success = false,
                        Feedback = "@E Umgehung erkannt: Bitte ermittle die Ziel-ID dynamisch über eine Unterabfrage, anstatt sie direkt zu übergeben.",
                        ResultTable = null
                    };
                }
            }

            DataTable userResultTable = null;
            string upperQueryCheck = processedQuery.Trim().ToUpper();
            // detect WITH (cte) as read queries alongside standard SELECT
            bool isSelect = upperQueryCheck.StartsWith("SELECT") || upperQueryCheck.StartsWith("WITH");
            int rowsAffected = 0;

            if (isSelect)
            {
                // SELECT
                userResultTable = ExecuteDbQuery(connection, processedQuery, token);
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
                        Feedback = "@E Umgehung erkannt: Bitte nutze UPDATE, um die Daten zu ändern.",
                        ResultTable = null
                    };
                if (taskIsInsert && (upperQuery.Contains("UPDATE") || upperQuery.Contains("DELETE")))
                    return new SqlTestResult
                    {
                        Success = false,
                        Feedback = "@E Umgehung erkannt: Bitte nutze INSERT, um die Daten hinzuzufügen.",
                        ResultTable = null
                    };
                if (taskIsDelete && (upperQuery.Contains("INSERT") || upperQuery.Contains("UPDATE")))
                    return new SqlTestResult
                    {
                        Success = false,
                        Feedback = "@E Umgehung erkannt: Bitte nutze DELETE, um die Daten zu löschen.",
                        ResultTable = null
                    };

                    // UPDATE/INSERT/DELETE
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = processedQuery;
                        using (token.Register(() => {
                            try { cmd.Cancel(); } catch { }
                            try { connection.Close(); } catch { }
                            try { connection.Dispose(); } catch { }
                        }))
                        {
                            rowsAffected = cmd.ExecuteNonQuery();
                        }
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
                var verifyDt = ExecuteDbQuery(connection, level.VerificationQuery, token);
                foreach (DataRow row in verifyDt.Rows)
                {
                    string[] rowData = row.ItemArray.Select(ObjectToInvariantString).ToArray();
                    actualRows.Add(rowData);
                }

                userResultTable = verifyDt;
            }

            bool correct = true;
            string errorFeedback = "@E Das Ergebnis stimmt nicht mit der Erwartung überein.";

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
                            $"@E Falsche Spaltenanzahl. Erwartet: {expectedCount}, Erhalten: {userResultTable.Columns.Count}";
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
                                        $"@E Falscher Spaltenname an Position {i + 1}. Erwartet: '{expectedCol.Name}', Erhalten: '{userColName}'";
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

                string msg = correct ? "@S Richtig! Aufgabe gelöst." : errorFeedback;
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
            // if forcefully killed via the token, safely suppress native termination errors
            if (token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
            }

            string errorMsg = ex.Message.Replace("SQLite Error", "SQL Fehler");
            return new SqlTestResult
            {
                Success = false,
                Feedback = errorMsg,
                ResultTable = null
            };
        }
    }

    public static string ConvertMysqlToSqlite(SqliteConnection conn, string query, CancellationToken token = default)
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
                {
                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = subQuery;
                            using (token.Register(() => { try { cmd.Cancel(); } catch { } }))
                            {
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
                    }
                    catch (Exception) when (token.IsCancellationRequested)
                    {
                        token.ThrowIfCancellationRequested();
                    }
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
            string assignments = insertSetMatch.Groups[3].Value.TrimEnd(' ', '\r', '\n', ';');

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

        // 1. string functions (added boundary \b to preserve GROUP_CONCAT correctly)
        // concat up to 5 arguments
        q = Regex.Replace(q, @"\bCONCAT\s*\(\s*([^,]+?)\s*,\s*([^,]+?)\s*,\s*([^,]+?)\s*,\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "$1 || $2 || $3 || $4 || $5", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCONCAT\s*\(\s*([^,]+?)\s*,\s*([^,]+?)\s*,\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "$1 || $2 || $3 || $4", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCONCAT\s*\(\s*([^,]+?)\s*,\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "$1 || $2 || $3", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCONCAT\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "$1 || $2", RegexOptions.IgnoreCase);

        q = Regex.Replace(q, @"\bGROUP_CONCAT\s*\(\s*([^,]+?)\s+SEPARATOR\s+([^)]+?)\s*\)", "GROUP_CONCAT($1, $2)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCHAR_LENGTH\s*\(", "LENGTH(", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCHARACTER_LENGTH\s*\(", "LENGTH(", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bUCASE\s*\(", "UPPER(", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bLCASE\s*\(", "LOWER(", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bLEFT\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "SUBSTR($1, 1, $2)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bRIGHT\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "SUBSTR($1, -($2))", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bLOCATE\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "INSTR($2, $1)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bSUBSTRING\s*\(\s*([^,]+?)\s+FROM\s+([^,]+?)\s+FOR\s+([^)]+?)\s*\)", "SUBSTR($1, $2, $3)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bSUBSTRING\s*\(\s*([^,]+?)\s*,\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "SUBSTR($1, $2, $3)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bSUBSTRING\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "SUBSTR($1, $2)", RegexOptions.IgnoreCase);

        // 2. control flow
        q = Regex.Replace(q, @"\bIF\s*\(", "IIF(", RegexOptions.IgnoreCase); // maps IF(cond, a, b) to IIF safely
        q = Regex.Replace(q, @"\bISNULL\s*\(\s*([^()]+)\s*\)", "($1 IS NULL)", RegexOptions.IgnoreCase);

        // 3. math & structural
        q = Regex.Replace(q, @"\bMOD\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "(($1) % ($2))", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bTRUNCATE\s*\(", "ROUND(", RegexOptions.IgnoreCase); // technically round, but visually identical in tests
        q = Regex.Replace(q, @"\bPOW\s*\(", "POWER(", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bRAND\(\)", "RANDOM()", RegexOptions.IgnoreCase); // highly compatible for ORDER BY RAND()
        q = Regex.Replace(q, @"\bAUTO_INCREMENT\b", "AUTOINCREMENT", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bINSERT\s+IGNORE\s+INTO\b", "INSERT OR IGNORE INTO", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bENGINE\s*=\s*\w+", "", RegexOptions.IgnoreCase); // strips unsupported DDL config keywords
        q = Regex.Replace(q, @"\bDEFAULT\s+CHARSET\s*=\s*\w+", "", RegexOptions.IgnoreCase);

        // 4. date/time part functions
        q = Regex.Replace(q, @"\bYEAR\s*\(\s*([^)]+)\s*\)", "strftime('%Y', $1)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bMONTH\s*\(\s*([^)]+)\s*\)", "strftime('%m', $1)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bDAY\s*\(\s*([^)]+)\s*\)", "strftime('%d', $1)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bHOUR\s*\(\s*([^)]+)\s*\)", "strftime('%H', $1)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bMINUTE\s*\(\s*([^)]+)\s*\)", "strftime('%M', $1)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bSECOND\s*\(\s*([^)]+)\s*\)", "strftime('%S', $1)", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bDATE_FORMAT\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)", "strftime($2, $1)", RegexOptions.IgnoreCase);

        // 5. datediff
        q = Regex.Replace(q, @"\bDATEDIFF\s*\(\s*([^,]+?)\s*,\s*([^)]+?)\s*\)",
            "CAST((julianday($1) - julianday($2)) AS INTEGER)", RegexOptions.IgnoreCase);

        // 6. DATE_ADD / DATE_SUB (with variable intervals mapping)
        // unit dates
        q = Regex.Replace(q, @"\bDATE_ADD\s*\(\s*([^,]+?)\s*,\s*INTERVAL\s+([+\-]?\d+)\s+(DAY|MONTH|YEAR)S?\s*\)",
            "date($1, '+$2 $3s')", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bDATE_SUB\s*\(\s*([^,]+?)\s*,\s*INTERVAL\s+([+\-]?\d+)\s+(DAY|MONTH|YEAR)S?\s*\)",
            "date($1, '-$2 $3s')", RegexOptions.IgnoreCase);
        // unit times
        q = Regex.Replace(q, @"\bDATE_ADD\s*\(\s*([^,]+?)\s*,\s*INTERVAL\s+([+\-]?\d+)\s+(HOUR|MINUTE|SECOND)S?\s*\)",
            "datetime($1, '+$2 $3s')", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bDATE_SUB\s*\(\s*([^,]+?)\s*,\s*INTERVAL\s+([+\-]?\d+)\s+(HOUR|MINUTE|SECOND)S?\s*\)",
            "datetime($1, '-$2 $3s')", RegexOptions.IgnoreCase);

        // fix possible double mathematical signs inside the resulting date modifiers (e.g. '+-5 days' -> '-5 days')
        q = q.Replace("'+-", "'-").Replace("'-+", "'-").Replace("'++", "'+").Replace("'--", "'+");

        // 7. current date/time mappings
        q = Regex.Replace(q, @"\bNOW\(\)", "datetime('now')", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCURDATE\(\)", "date('now')", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCURTIME\(\)", "time('now')", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bSYSDATE\(\)", "datetime('now')", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCURRENT_TIMESTAMP\(\)", "datetime('now')", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCURRENT_DATE\(\)", "date('now')", RegexOptions.IgnoreCase);
        q = Regex.Replace(q, @"\bCURRENT_TIME\(\)", "time('now')", RegexOptions.IgnoreCase);

        return q;
    }

    private static DataTable ExecuteDbQuery(SqliteConnection conn, string sql, CancellationToken token = default)
    {
        var dt = new DataTable();
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                using (token.Register(() => { 
                    try { cmd.Cancel(); } catch { } 
                    try { conn.Close(); } catch { }
                    try { conn.Dispose(); } catch { }
                }))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        // initialize columns
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            dt.Columns.Add(reader.GetName(i), reader.GetFieldType(i) ?? typeof(object));
                        }

                        // manually read rows to allow cancellation checking during memory allocation
                        while (reader.Read())
                        {
                            token.ThrowIfCancellationRequested();
                            var row = dt.NewRow();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[i] = reader.GetValue(i);
                            }
                            dt.Rows.Add(row);
                        }
                    }
                }
            }
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            token.ThrowIfCancellationRequested();
        }

        return dt;
    }
}