using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AbiturEliteCode.cs
{
    internal static class SqlPrerequisiteSystem
    {
        public class SqlLessonData
        {
            public string Title { get; set; }
            public string YoutubeUrl { get; set; }
            public string DocsUrl { get; set; }
        }

        private static Dictionary<string, SqlLessonData> _database = new();

        // PREREQUISITES SECTIONS (Rows in AllTopics):
        // > Grundlagen
        // > Datentypen & Werte
        // > Filtern & Sortieren
        // > Relationen & Schlüssel
        // > Joins
        // > Aggregation
        // > Berechnungen im SELECT
        // > Datumsfunktionen
        // > Unterabfragen (Subqueries)
        // > Daten manipulieren (DML)
        // > Struktur definieren (DDL)
        // > Erweiterte Konzepte
        // > Datenbankdesign

        public static readonly List<string> AllTopics = new List<string>
        {
            "Datenbanken", "Tabellen", "SELECT", "FROM", "WHERE", "Vergleichsoperatoren", "Logische Operatoren (AND, OR, NOT)", "Aliase (AS)", "Comments",
            "INT", "VARCHAR", "DATE / DATETIME", "FLOAT / DECIMAL", "NULL-Werte", "Literale (Strings, Zahlen, Datumswerte)",
            "BETWEEN", "IN / NOT IN", "LIKE (Wildcards % und _)", "IS NULL / IS NOT NULL", "ORDER BY (ASC / DESC)", "LIMIT",
            "Primärschlüssel (PRIMARY KEY)", "Fremdschlüssel (FOREIGN KEY)", "ER-Diagramm lesen (Chen-Notation)", "Relationales Schema aus ER-Diagramm ableiten", "1:1 Beziehungen", "1:n Beziehungen", "n:m Beziehungen", "Referentielle Integrität",
            "Implicit Join", "INNER JOIN ... ON", "LEFT JOIN", "JOINs verstehen",
            "COUNT()", "SUM()", "AVG()", "MIN() / MAX()", "GROUP BY", "HAVING",
            "Arithmetische Ausdrücke", "ROUND()", "POWER()",
            "YEAR()", "MONTH()", "DAY()", "NOW()", "TIMEDIFF()", "DATEDIFF()", "DATE_ADD()", "Datumsvergleiche",
            "Subquery im WHERE mit IN", "Subquery im WHERE mit NOT IN", "Korrelierte Unterabfragen", "Subquery im FROM (abgeleitete Tabelle)", "Scalar Subquery (einzelner Wert)",
            "INSERT INTO ... VALUES", "UPDATE ... SET ... WHERE", "DELETE ... WHERE",
            "CREATE TABLE", "Constraints (UNIQUE, NOT NULL, PRIMARY KEY, FOREIGN KEY)", "DROP TABLE",
            "DISTINCT", "Variablen (SET @variable)", "Transaktionen (BEGIN, COMMIT, ROLLBACK)", "Ausführungsreihenfolge von SQL-Klauseln",
            "Normalisierung (3NF)", "Vererbung"
        };

        public static void Initialize()
        {
            try
            {
                var uri = new Uri("avares://AbiturEliteCode/assets/sql-prerequisites.txt");
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    using var reader = new StreamReader(stream);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(">")) continue;

                        var parts = line.Split('|');
                        if (parts.Length >= 3)
                        {
                            string title = parts[0].Trim();
                            string ytRaw = parts[1].Replace("youtube:", "").Trim();
                            string docRaw = parts[2].Replace("docs:", "").Trim();

                            _database[title] = new SqlLessonData
                            {
                                Title = title,
                                YoutubeUrl = ytRaw,
                                DocsUrl = docRaw
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load sql prerequisites: {ex.Message}");
            }
        }

        public static SqlLessonData GetLesson(string title)
        {
            return _database.TryGetValue(title, out var data) ? data : null;
        }

        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // basic validation to only allow expected external links
            if (!url.StartsWith("https://youtube.com/") &&
                !url.StartsWith("https://youtu.be/") &&
                !url.StartsWith("https://www.youtube.com/") &&
                !url.StartsWith("https://dev.mysql.com/"))
            {
                return;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
            }
            catch { }
        }
    }
}