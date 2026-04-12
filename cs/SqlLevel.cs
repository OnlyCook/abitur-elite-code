using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AbiturEliteCode.cs;
// SQL LEVEL CRATION GUIDE
// the app uses SQLite but we are trying our best to emulate MySQL syntax and behavior as that is what we are supposed to learn and is expected from us in the abitur

// regarding "PlantUMLSources" we are using plantuml and only enitity-relation-diagrams (Chen's notation)

// the entity-relation-diagrams should match the ones in the abitur exams, here is how they should be structured:
// multiplicities are written in the min-max-notation like this for each side: [min,max] (for example: ET1 -(0,n)- REL -(0,m)- ET2; not actually valid just for visualization)
// primary keys are underlined (<<key>> in the plantuml diagram, relational model: primary key RColumn has 'IsPk = true')
// entities (tables) are written in PascalCase (for example: "Supermarkt"); attributes (column names) are written in camelCase (for example: "anzahlGetränke"); ids have their "id" in lowercase and without underscores (for example: "kid" or "klasseid")
// foreign keys are hashtags '#' in the relational model and '_FK' in the actual database as well as in the diagrams
// up to level 17 we include the foreign keys in the ER-diagrams, but in the later levels we do not (the user must know on their own what has what key)
// multiplicities in relations are flipped in the abitur exams, thats why we arent using the variant that is commonly used for the chens notation, but the flipped variant (instead of: "A -(1,1)-b-(0,n)- C" we do: "A -(0,n)-b-(1,1)- C")
// starting from level 23 the user must normalize the ER-diagram themselves; also starting from level 23 the task should give less hints and stop highlighting important aspects, thus making the language used for the task more similar to the abitur exams

public class RTable
{
    public string Name { get; set; } = "";
    public List<RColumn> Columns { get; set; } = new();
}

public class RColumn
{
    public string Name { get; set; } = "id";
    public bool IsPk { get; set; }
    public bool IsFk { get; set; }
}

public class SqlExpectedColumn
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool StrictName { get; set; }
}

public class SqlLevel
{
    public int Id { get; set; }
    public string Section { get; set; }
    public string SkipCode { get; set; }
    public string NextLevelCode { get; set; }
    public string Title { get; set; }
    public string Difficulty { get; set; } = ""; // "Einfach", "Mittel", "Schwer", "Abitur"
    public List<string> DiagramTags { get; set; } = new(); // "ER"
    public string Description { get; set; }
    public string SetupScript { get; set; }
    public string VerificationQuery { get; set; }
    public List<SqlExpectedColumn> ExpectedSchema { get; set; } = new();
    public List<string[]> ExpectedResult { get; set; }
    public string MaterialDocs { get; set; }

    public bool IsRelationalModelReadOnly { get; set; }
    public List<RTable> InitialRelationalModel { get; set; } = new();

    public List<string> DiagramPaths { get; set; } = new(); // max of 3
    public List<string> PlantUMLSources { get; set; } = new(); // max of 3
    public List<string> AuxiliaryIds { get; set; } = new();

    public List<string> Prerequisites { get; set; } = new();
    public List<string> OptionalPrerequisites { get; set; } = new();

    public bool IsRelationalModelSectionShared { get; set; }

    public string GetDisplayTitle(bool antiSpoilerEnabled)
    {
        if (!antiSpoilerEnabled) return Title;
        if (!string.IsNullOrEmpty(Section) && Section.StartsWith("Sektion 7")) return Title; // ignore section 7
        return Regex.Replace(Title ?? "", @"\s*\(.*?\)\s*", "").Trim(); // remove braces including its contents
    }
}

public static class SqlLevelCodes
{
    public static string[] CodesList =
    {
        "SEL", "WHE", "ORD", "GRP", "INS", "UPD", "DEL", "EXM",
        "JON", "IMP", "JOI", "JO3", "JOX",
        "LEF", "NUL", "DIS", "PRB",
        "MAT", "SUM", "TOP", "HAV", "SAL",
        "DAT", "BTW", "NOW", "DIF", "ADD", "HOT",
        "SIN", "SNO", "SIS", "ABO",
        "LAD", "COS", "CAN",
        ""
    };
}

public static class SqlSharedDiagrams
{
    // placeholder
}

public static class SqlAuxiliaryImplementations
{
    public static string GetCode(string auxId)
    {
        return "";
        // placeholder
    }
}

public static class SqlCurriculum
{
    public static List<SqlLevel> GetLevels()
    {
        // randomize target bid for level 35 (prevent hardcoded exploiting)
        int bBase = new Random().Next(10000, 89000);
        int b1 = bBase + 1;
        int b2 = bBase + 2;
        int b3 = bBase + 3;
        int b4 = bBase + 4;
        int bTarget = bBase + 5; // goal
        int b6 = bBase + 6;

        return new List<SqlLevel>
        {
            // --- SECTION 1 ---
            new()
            {
                Id = 1,
                Section = "Sektion 1: SQL Grundlagen",
                SkipCode = SqlLevelCodes.CodesList[0],
                NextLevelCode = SqlLevelCodes.CodesList[1],
                Title = "Projektion (SELECT)",
                Description = "In der Datenbank der Schulbibliothek existiert eine Tabelle [Buch].\n\n" +
                              "Aufgabe:\n" +
                              "Wählen Sie nur den [titel] und den [preis] aller Bücher aus.",
                SetupScript =
                    "CREATE TABLE Buch (id INTEGER PRIMARY KEY, titel TEXT, Autor TEXT, preis REAL, isbn TEXT);" +
                    "INSERT INTO Buch (titel, autor, preis, isbn) VALUES ('Faust', 'Goethe', 9.99, '978-3');" +
                    "INSERT INTO Buch (titel, autor, preis, isbn) VALUES ('Die Verwandlung', 'Kafka', 5.50, '978-4');" +
                    "INSERT INTO Buch (titel, autor, preis, isbn) VALUES ('Der Prozess', 'Kafka', 8.90, '978-5');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "titel", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "preis", Type = "DOUBLE", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Faust", "9.99" },
                    new[] { "Die Verwandlung", "5.5" },
                    new[] { "Der Prozess", "8.9" }
                },
                MaterialDocs = "start-hint: Projektion\n" +
                               "Mit [SELECT spalte1, spalte2 FROM Tabelle] wählen Sie spezifische Spalten aus.\n" +
                               "Möchten Sie alle Spalten, nutzen Sie das Wildcard-Symbol [*] (Sternchen).\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Buch",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "titel" }, new() { Name = "autor" },
                            new() { Name = "preis" }, new() { Name = "isbn" }
                        }
                    }
                },
                DiagramPaths = new List<string>(),
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "Datenbanken", "Tabellen", "SELECT", "FROM" },
                OptionalPrerequisites = new List<string> { "INT", "VARCHAR", "FLOAT / DECIMAL" }
            },
            new()
            {
                Id = 2,
                Section = "Sektion 1: SQL Grundlagen",
                SkipCode = SqlLevelCodes.CodesList[1],
                NextLevelCode = SqlLevelCodes.CodesList[2],
                Title = "Selektion (WHERE)",
                Description = "Die Bibliotheksleitung sucht nach günstigen Büchern für den Ausverkauf.\n\n" +
                              "Aufgabe:\n" +
                              "Ermitteln Sie alle Spalten ([*]) aller Bücher der Tabelle [Buch], deren Preis **kleiner als 9.00** Euro ist.",
                SetupScript =
                    "CREATE TABLE Buch (id INTEGER PRIMARY KEY, titel TEXT, autor TEXT, Preis REAL, isbn TEXT);" +
                    "INSERT INTO Buch (titel, autor, preis, isbn) VALUES ('Faust', 'Goethe', 9.99, '978-3');" +
                    "INSERT INTO Buch (titel, autor, preis, isbn) VALUES ('Die Verwandlung', 'Kafka', 5.50, '978-4');" +
                    "INSERT INTO Buch (titel, autor, preis, isbn) VALUES ('Der Prozess', 'Kafka', 8.90, '978-5');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "id", Type = "INT", StrictName = false },
                    new() { Name = "titel", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "autor", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "preis", Type = "DOUBLE", StrictName = false },
                    new() { Name = "isbn", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "2", "Die Verwandlung", "Kafka", "5.5", "978-4" },
                    new[] { "3", "Der Prozess", "Kafka", "8.9", "978-5" }
                },
                MaterialDocs = "start-hint: Filterbedingung\n" +
                               "Nutzen Sie die [WHERE]-Klausel.\n" +
                               "Operatoren:\n" +
                               "- Kleiner: [<]\n" +
                               "- Größer: [>]\n" +
                               "- Gleich: [=]\n" +
                               "- Ungleich: [!=] oder [<>]\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Buch",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "titel" }, new() { Name = "autor" },
                            new() { Name = "preis" }, new() { Name = "isbn" }
                        }
                    }
                },
                DiagramPaths = new List<string>(),
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "WHERE", "Vergleichsoperatoren" },
                OptionalPrerequisites = new List<string> { "Literale (Strings, Zahlen, Datumswerte)" }
            },
            new()
            {
                Id = 3,
                Section = "Sektion 1: SQL Grundlagen",
                SkipCode = SqlLevelCodes.CodesList[2],
                NextLevelCode = SqlLevelCodes.CodesList[3],
                Title = "Sortierung (ORDER BY)",
                Description = "Wir suchen alle Bücher eines bestimmten Autors, sortiert nach dem Titel.\n\n" +
                              "Aufgabe:\n" +
                              "Wählen Sie [titel] und [erscheinungsjahr] aller Bücher von 'Kafka' aus der Tabelle [Buch].\n" +
                              "Sortieren Sie das Ergebnis **aufsteigend** nach dem Titel.",
                SetupScript =
                    "CREATE TABLE Buch (id INTEGER PRIMARY KEY, titel TEXT, autor TEXT, erscheinungsjahr INTEGER);" +
                    "INSERT INTO Buch (titel, autor, erscheinungsjahr) VALUES ('Die Verwandlung', 'Kafka', 1915);" +
                    "INSERT INTO Buch (titel, autor, erscheinungsjahr) VALUES ('Faust', 'Goethe', 1808);" +
                    "INSERT INTO Buch (titel, autor, erscheinungsjahr) VALUES ('Das Schloss', 'Kafka', 1926);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "titel", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "erscheinungsjahr", Type = "INT", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Das Schloss", "1926" },
                    new[] { "Die Verwandlung", "1915" }
                },
                MaterialDocs = "start-hint: Sortierung\n" +
                               "Syntax: [ORDER BY spalte ASC|DESC]\n" +
                               "- [ASC]: Aufsteigend (Standard, A-Z, 0-9)\n" +
                               "- [DESC]: Absteigend (Z-A, 9-0)\n" +
                               ":end-hint\n" +
                               "start-tipp: Strings\n" +
                               "Textwerte müssen in einfachen Anführungszeichen stehen: ['Kafka'].\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Buch",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "titel" }, new() { Name = "autor" },
                            new() { Name = "erscheinungsjahr" }
                        }
                    }
                },
                DiagramPaths = new List<string>(),
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "ORDER BY (ASC / DESC)" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 4,
                Section = "Sektion 1: SQL Grundlagen",
                SkipCode = SqlLevelCodes.CodesList[3],
                NextLevelCode = SqlLevelCodes.CodesList[4],
                Title = "Gruppierung (GROUP BY)",
                Description =
                    "Für eine statistische Auswertung sollen Bücher nach ihrem Genre zusammengefasst werden.\n\n" +
                    "Aufgabe:\n" +
                    "Ermitteln Sie für jedes [genre] den **Durchschnittspreis** als 'Durchschnitt'.\n" +
                    "Das Ergebnis soll das Genre und den berechneten Durchschnittspreis enthalten.",
                SetupScript =
                    "CREATE TABLE Buch (id INTEGER PRIMARY KEY, titel TEXT, autor TEXT, genre TEXT, preis REAL);" +
                    "INSERT INTO Buch (titel, autor, genre, preis) VALUES ('Faust', 'Goethe', 'Drama', 10.0);" +
                    "INSERT INTO Buch (titel, autor, genre, preis) VALUES ('Iphigenie', 'Goethe', 'Drama', 12.0);" +
                    "INSERT INTO Buch (titel, autor, genre, preis) VALUES ('Es', 'King', 'Horror', 15.0);" +
                    "INSERT INTO Buch (titel, autor, genre, preis) VALUES ('Shining', 'King', 'Horror', 13.0);" +
                    "INSERT INTO Buch (titel, autor, genre, preis) VALUES ('Der Marsianer', 'Weir', 'SciFi', 20.0);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "genre", Type = "VARCHAR(255)", StrictName = true },
                    new() { Name = "durchschnitt", Type = "DOUBLE", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Drama", "11" }, // (10+12)/2 = 11
                    new[] { "Horror", "14" }, // (15+13)/2 = 14
                    new[] { "SciFi", "20" }
                },
                MaterialDocs = "start-hint: GROUP BY Syntax\n" +
                               "Wenn Sie Aggregatfunktionen wie [AVG()], [COUNT()] oder [SUM()] nutzen und gleichzeitig eine normale Spalte ausgeben wollen, müssen Sie nach dieser Spalte gruppieren:\n" +
                               "{|SELECT spalte1, AVG(spalte2)\nFROM Tabelle\nGROUP BY spalte1;|}\n" +
                               ":end-hint\n" +
                               "start-hint: Spaltenbenennung\n" +
                               "Um Spalten einen eigenen Namen zu geben, nutzen sie das [AS]-Schlüsselwort:\n" +
                               "{|SELECT AVG(spalte) AS EigenerName\nFROM Tabelle;|}" +
                               "Dies wird häufig zusammen mit Aggregatfunktionen genutzt.\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Buch",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "titel" }, new() { Name = "autor" },
                            new() { Name = "genre" }, new() { Name = "preis" }
                        }
                    }
                },
                DiagramPaths = new List<string>(),
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "GROUP BY", "AVG()", "Aliase (AS)" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 5,
                Section = "Sektion 1: SQL Grundlagen",
                SkipCode = SqlLevelCodes.CodesList[4],
                NextLevelCode = SqlLevelCodes.CodesList[5],
                Title = "Daten Einfügen (INSERT)",
                Description = "Ein neuer Schüler hat sich angemeldet.\n\n" +
                              "Aufgabe:\n" +
                              "Fügen Sie den Schüler 'Leon' mit der ID 10 in die Klasse 12 ein.",
                SetupScript = "CREATE TABLE Schueler (id INTEGER PRIMARY KEY, name TEXT, klasse INTEGER);" +
                              "INSERT INTO Schueler VALUES (1, 'Max', 11);",
                VerificationQuery = "SELECT * FROM Schueler WHERE id = 10",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "id", Type = "INT", StrictName = false },
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "klasse", Type = "INT", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "10", "Leon", "12" }
                },
                MaterialDocs = "start-hint: INSERT Syntax\n" +
                               "Variante 1 (Alle Spalten):\n" +
                               "{|INSERT INTO Tabelle VALUES (wert1, wert2, ...);|}\n" +
                               "Variante 2 (Spezifische Spalten):\n" +
                               "{|INSERT INTO Tabelle (spalteA, spalteB) VALUES (wertA, wertB);|}\n" +
                               "Variante 3 (MySQL SET-Syntax):\n" +
                               "{|INSERT INTO Tabelle SET spalteA = wertA, spalteB = wertB;|}\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Schueler",
                        Columns = new List<RColumn>
                            { new() { Name = "id", IsPk = true }, new() { Name = "name" }, new() { Name = "klasse" } }
                    }
                },
                DiagramPaths = new List<string>(),
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "INSERT INTO ... VALUES" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 6,
                Section = "Sektion 1: SQL Grundlagen",
                SkipCode = SqlLevelCodes.CodesList[5],
                NextLevelCode = SqlLevelCodes.CodesList[6],
                Title = "Daten Ändern (UPDATE)",
                Description = "Der Schüler 'Max' mit der ID 1 ist in die Klasse 13 versetzt worden.\n\n" +
                              "Aufgabe:\n" +
                              "Aktualisieren Sie den Eintrag von Max in der Tabelle [Schueler], sodass seine Klasse nun 13 ist.\n" +
                              "Achten Sie unbedingt auf die [WHERE]-Klausel, da sonst alle Schüler betroffen werden!",
                SetupScript = "CREATE TABLE Schueler (id INTEGER PRIMARY KEY, name TEXT, klasse INTEGER);" +
                              "INSERT INTO Schueler VALUES (1, 'Max', 12); " +
                              "INSERT INTO Schueler VALUES (2, 'Lisa', 11);" +
                              "INSERT INTO Schueler VALUES (3, 'Max', 12);",
                VerificationQuery = "SELECT * FROM Schueler",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "id", Type = "INT", StrictName = false },
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "klasse", Type = "INT", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "1", "Max", "13" },
                    new[] { "2", "Lisa", "11" },
                    new[] { "3", "Max", "12" }
                },
                MaterialDocs = "start-hint: UPDATE Syntax\n" +
                               "{|UPDATE Tabelle SET spalte = neuerWert WHERE Bedingung;|}" +
                               "Nutzen Sie am besten immer den Primärschlüssel ([id]) in der WHERE-Klausel, da Namen oder Klassen mehrfach vorkommen können.\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Schueler",
                        Columns = new List<RColumn>
                            { new() { Name = "id", IsPk = true }, new() { Name = "name" }, new() { Name = "klasse" } }
                    }
                },
                DiagramPaths = new List<string>(),
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "UPDATE ... SET ... WHERE" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 7,
                Section = "Sektion 1: SQL Grundlagen",
                SkipCode = SqlLevelCodes.CodesList[6],
                NextLevelCode = SqlLevelCodes.CodesList[7],
                Title = "Daten Löschen (DELETE)",
                Description = "Alle Schüler der Klasse 13 haben die Schule verlassen (Abitur bestanden).\n\n" +
                              "Aufgabe:\n" +
                              "Löschen Sie alle Einträge aus der Tabelle [Schueler], bei denen die Klasse 13 ist.",
                SetupScript = "CREATE TABLE Schueler (id INTEGER PRIMARY KEY, name TEXT, klasse INTEGER);" +
                              "INSERT INTO Schueler VALUES (1, 'AbiAbsolvent', 13); " +
                              "INSERT INTO Schueler VALUES (2, 'BleibtHier', 12); " +
                              "INSERT INTO Schueler VALUES (3, 'AuchWeg', 13);",
                VerificationQuery = "SELECT * FROM Schueler",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "id", Type = "INT", StrictName = false },
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "klasse", Type = "INT", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "2", "BleibtHier", "12" }
                },
                MaterialDocs = "start-hint: DELETE Syntax\n" +
                               "{|DELETE FROM Tabelle WHERE Bedingung;|}" +
                               "Vorsicht: [DELETE FROM Tabelle] (ohne Where) löscht den gesamten Inhalt!\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Schueler",
                        Columns = new List<RColumn>
                            { new() { Name = "id", IsPk = true }, new() { Name = "name" }, new() { Name = "klasse" } }
                    }
                },
                DiagramPaths = new List<string>(),
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "DELETE ... WHERE" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 8,
                Section = "Sektion 1: SQL Grundlagen",
                SkipCode = SqlLevelCodes.CodesList[7],
                NextLevelCode = SqlLevelCodes.CodesList[8],
                Title = "Klausurphase",
                Description = "Dies ist eine komplexe Abfrage zum Abschluss der Grundlagen.\n\n" +
                              "Aufgabe:\n" +
                              "Ermitteln Sie [schueler] und [notenpunkte] aller Klausuren im Fach 'Informatik', die **schlechter als 5 Punkte** sind.\n" +
                              "Sortieren Sie das Ergebnis absteigend nach den Notenpunkten.",
                SetupScript =
                    "CREATE TABLE Klausur (id INTEGER PRIMARY KEY, schueler TEXT, fach TEXT, notenpunkte INTEGER);" +
                    "INSERT INTO Klausur (schueler, fach, notenpunkte) VALUES ('Max', 'Mathe', 12);" +
                    "INSERT INTO Klausur (schueler, fach, notenpunkte) VALUES ('Lisa', 'Informatik', 14);" +
                    "INSERT INTO Klausur (schueler, fach, notenpunkte) VALUES ('Tom', 'Informatik', 3);" +
                    "INSERT INTO Klausur (schueler, fach, notenpunkte) VALUES ('Sarah', 'Informatik', 4);" +
                    "INSERT INTO Klausur (schueler, fach, notenpunkte) VALUES ('Jan', 'Deutsch', 2);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "schueler", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "notenpunkte", Type = "INT", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Sarah", "4" },
                    new[] { "Tom", "3" }
                },
                MaterialDocs = "start-tipp: Vorgehensweise\n" +
                               "1. [SELECT]: Welche Spalten brauche ich?\n" +
                               "2. [FROM]: Welche Tabelle?\n" +
                               "3. [WHERE]: Welche Bedingungen müssen gleichzeitig gelten ([AND])?\n" +
                               "4. [ORDER BY]: Wie soll sortiert werden?\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Klausur",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "schueler" }, new() { Name = "fach" },
                            new() { Name = "notenpunkte" }
                        }
                    }
                },
                DiagramPaths = new List<string>(),
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "Logische Operatoren (AND, OR, NOT)" },
                OptionalPrerequisites = new List<string>()
            },

            // --- SECTION 2 ---
            new()
            {
                Id = 9,
                Section = "Sektion 2: Relationen & Joins",
                SkipCode = SqlLevelCodes.CodesList[8],
                NextLevelCode = SqlLevelCodes.CodesList[9],
                Title = "Der Schlüssel zum Erfolg (PK & FK)",
                Description =
                    "Wir befinden uns in der Datenbank einer Schulbibliothek. Um Redundanzen zu vermeiden, wurden Bücher und Autoren in zwei getrennte Tabellen aufgeteilt (Normalisierung).\n\n" +
                    "Das Buch 'Faust' speichert nicht mehr den Namen 'Goethe', sondern referenziert diesen über eine ID (Fremdschlüssel).\n\n" +
                    "**Aufgabe:**\n" +
                    "Ermitteln Sie die [id] des Autors mit dem Nachnamen 'Goethe' aus der Tabelle [Autor], um zu verstehen, welche Zahl in der Buch-Tabelle verwendet wird.",
                SetupScript = "CREATE TABLE Autor (id INTEGER PRIMARY KEY, vorname TEXT, nachname TEXT);" +
                              "CREATE TABLE Buch (id INTEGER PRIMARY KEY, titel TEXT, autorid_FK INTEGER);" +
                              "INSERT INTO Autor VALUES (101, 'Johann Wolfgang von', 'Goethe');" +
                              "INSERT INTO Autor VALUES (102, 'Friedrich', 'Schiller');" +
                              "INSERT INTO Buch VALUES (1, 'Faust', 101);" +
                              "INSERT INTO Buch VALUES (2, 'Die Räuber', 102);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "id", Type = "INT", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "101" }
                },
                MaterialDocs = "start-hint: Primärschlüssel\n" +
                               "Jeder Datensatz in der Tabelle [Autor] ist durch die [id] eindeutig identifizierbar.\n" +
                               "In der Tabelle [Buch] wird diese ID genutzt, um auf den Autor zu verweisen.\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Autor",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "vorname" }, new() { Name = "nachname" }
                        }
                    },
                    new()
                    {
                        Name = "Buch",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "titel" },
                            new() { Name = "autorid", IsFk = true }
                        }
                    }
                },
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec2\\lvl9-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Autor {\n    id <<key>>\n    vorname\n    nachname\n}\nentity Buch {\n    id <<key>>\n    titel\n    autorid_FK\n}\nrelationship verfasst {\n}\nAutor -(1,1)- verfasst\nverfasst -(0,n)- Buch\n@endchen"
                },
                Prerequisites = new List<string> { "Primärschlüssel (PRIMARY KEY)", "Fremdschlüssel (FOREIGN KEY)" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 10,
                Section = "Sektion 2: Relationen & Joins",
                SkipCode = SqlLevelCodes.CodesList[9],
                NextLevelCode = SqlLevelCodes.CodesList[10],
                Title = "Die erste Verbindung (Implicit Join)",
                Description =
                    "Nun sollen die Daten aus beiden Tabellen zusammengeführt werden. Wir nutzen dazu zunächst die klassische Schreibweise (impliziter Join) über die [WHERE]-Klausel.\n\n" +
                    "**Aufgabe:**\n" +
                    "Geben Sie eine Liste aller [titel] und der zugehörigen [nachname]n der Autoren aus.\n" +
                    "Nutzen Sie die Syntax: [FROM Buch, Autor] und verknüpfen Sie die Tabellen im [WHERE]-Teil, indem Sie den Fremdschlüssel ([Buch.autorid]) mit dem Primärschlüssel ([Autor.id]) gleichsetzen.\n\n" +
                    "**Hinweis:** Im relationalen Datenmodell werden Fremdschlüssel mit einer Raute ([#]) dargestellt, da SQL-Datenbanken keine Rauten bei der Benennung akzeptiert, wird stattdessen standardmäßig [_FK] genutzt.",
                SetupScript = "CREATE TABLE Autor (id INTEGER PRIMARY KEY, vorname TEXT, nachname TEXT);" +
                              "CREATE TABLE Buch (id INTEGER PRIMARY KEY, titel TEXT, autorid_FK INTEGER);" +
                              "INSERT INTO Autor VALUES (1, 'Johann', 'Goethe');" +
                              "INSERT INTO Autor VALUES (2, 'Franz', 'Kafka');" +
                              "INSERT INTO Buch VALUES (10, 'Faust', 1);" +
                              "INSERT INTO Buch VALUES (11, 'Die Verwandlung', 2);" +
                              "INSERT INTO Buch VALUES (12, 'Der Prozess', 2);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "titel", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "nachname", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Faust", "Goethe" },
                    new[] { "Die Verwandlung", "Kafka" },
                    new[] { "Der Prozess", "Kafka" }
                },
                MaterialDocs = "start-hint: Kartesisches Produkt\n" +
                               "Wenn Sie nur [FROM Buch, Autor] schreiben, wird jedes Buch mit jedem Autor kombiniert.\n" +
                               "Erst durch [WHERE Buch.autorid = Autor.id] filtern Sie die korrekten Paare heraus.\n" +
                               "Testen Sie dies gerne aus, um den Unterschied visuell zu erkennen.\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Autor",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "norname" }, new() { Name = "nachname" }
                        }
                    },
                    new()
                    {
                        Name = "Buch",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "titel" },
                            new() { Name = "autorid", IsFk = true }
                        }
                    }
                },
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec2\\lvl10-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Autor {\n    id <<key>>\n    nachname\n}\nentity Buch {\n    id <<key>>\n    titel\n    autorid_FK\n}\nrelationship verfasst {\n}\nAutor -(1,1)- verfasst\nverfasst -(0,n)- Buch\n@endchen"
                },
                Prerequisites = new List<string> { "Implicit Join" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 11,
                Section = "Sektion 2: Relationen & Joins",
                SkipCode = SqlLevelCodes.CodesList[10],
                NextLevelCode = SqlLevelCodes.CodesList[11],
                Title = "Modernes Verbinden (INNER JOIN)",
                Description =
                    "Der SQL-Standard sieht für Verknüpfungen den [JOIN]-Operator vor. Dieser trennt die Verknüpfungslogik sauber von der Filterlogik.\n\n" +
                    "**Aufgabe:**\n" +
                    "Erstellen Sie exakt dieselbe Liste wie im vorherigen Level ([titel], [nachname]), nutzen Sie diesmal jedoch den **INNER JOIN**.",
                SetupScript = "CREATE TABLE Autor (id INTEGER PRIMARY KEY, vorname TEXT, nachname TEXT);" +
                              "CREATE TABLE Buch (id INTEGER PRIMARY KEY, titel TEXT, autorid_FK INTEGER);" +
                              "INSERT INTO Autor VALUES (1, 'Johann', 'Goethe');" +
                              "INSERT INTO Autor VALUES (2, 'Franz', 'Kafka');" +
                              "INSERT INTO Buch VALUES (10, 'Faust', 1);" +
                              "INSERT INTO Buch VALUES (11, 'Die Verwandlung', 2);" +
                              "INSERT INTO Buch VALUES (12, 'Der Prozess', 2);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "titel", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "nachname", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Faust", "Goethe" },
                    new[] { "Die Verwandlung", "Kafka" },
                    new[] { "Der Prozess", "Kafka" }
                },
                MaterialDocs = "start-hint: JOIN Syntax\n" +
                               "{|SELECT ...\nFROM TabelleA\nJOIN TabelleB ON TabelleA.FK = TabelleB.PK;|}\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Autor",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "vorname" }, new() { Name = "nachname" }
                        }
                    },
                    new()
                    {
                        Name = "Buch",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "id", IsPk = true }, new() { Name = "titel" },
                            new() { Name = "autorid", IsFk = true }
                        }
                    }
                },
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec2\\lvl10-1.svg"
                },
                PlantUMLSources = new List<string>(), // shared
                Prerequisites = new List<string> { "JOINs verstehen", "INNER JOIN ... ON" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 12,
                Section = "Sektion 2: Relationen & Joins",
                SkipCode = SqlLevelCodes.CodesList[11],
                NextLevelCode = SqlLevelCodes.CodesList[12],
                Title = "Wer liest was? (3-Wege Join)",
                Description =
                    "Die Datenbank wurde erweitert. Schüler können nun Bücher ausleihen. Da ein Schüler viele Bücher leiht und ein Buch (über die Zeit) von vielen Schülern geliehen wird, existiert eine Relationstabelle.\n\n" +
                    "**Aufgabe:**\n" +
                    "Ermitteln Sie, welcher Schüler welches Buch ausgeliehen hat.\n" +
                    "Geben Sie den [name]n des Schülers und den [titel] des Buches aus.",
                SetupScript = "CREATE TABLE Schueler (id INTEGER PRIMARY KEY, name TEXT, klasse TEXT);" +
                              "CREATE TABLE Buch (id INTEGER PRIMARY KEY, titel TEXT);" +
                              "CREATE TABLE ausleihe (schuelerid_FK INTEGER, buchid_FK INTEGER, datum TEXT);" +
                              "INSERT INTO Schueler VALUES (1, 'Max', '10b');" +
                              "INSERT INTO Schueler VALUES (2, 'Lisa', '12a');" +
                              "INSERT INTO Buch VALUES (100, 'Faust');" +
                              "INSERT INTO Buch VALUES (101, 'Nathan der Weise');" +
                              "INSERT INTO ausleihe VALUES (1, 100, '2023-01-01');" +
                              "INSERT INTO ausleihe VALUES (2, 101, '2023-01-05');" +
                              "INSERT INTO ausleihe VALUES (2, 100, '2023-02-01');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "titel", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Max", "Faust" },
                    new[] { "Lisa", "Nathan der Weise" },
                    new[] { "Lisa", "Faust" }
                },
                MaterialDocs = "start-hint: Kette von Joins\n" +
                               "Sie müssen von Tabelle A nach B und von B nach C springen:\n" +
                               "{|FROM Schueler\n" +
                               "JOIN ausleihe ON ...\n" +
                               "JOIN Buch ON ...|}\n" +
                               ":end-hint\n" +
                               "start-tipp: Schreibfaul? (Aliase)\n" +
                               "Statt immer den vollen Tabellennamen zu tippen, können Sie Kürzel definieren:\n" +
                               "{|FROM Schueler s JOIN ausleihe a ON ...|}" +
                               "Danach können Sie [s.name] statt [Schueler.name] schreiben.\n" +
                               ":end-hint",
                IsRelationalModelReadOnly = true,
                InitialRelationalModel = new List<RTable>
                {
                    new()
                    {
                        Name = "Schueler",
                        Columns = new List<RColumn>
                            { new() { Name = "id", IsPk = true }, new() { Name = "name" }, new() { Name = "klasse" } }
                    },
                    new()
                    {
                        Name = "Buch",
                        Columns = new List<RColumn> { new() { Name = "id", IsPk = true }, new() { Name = "titel" } }
                    },
                    new()
                    {
                        Name = "ausleihe",
                        Columns = new List<RColumn>
                        {
                            new() { Name = "schuelerid", IsFk = true, IsPk = true },
                            new() { Name = "buchid", IsFk = true, IsPk = true }, new() { Name = "datum" }
                        }
                    }
                },
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec2\\lvl12-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Schueler {\n    id <<key>>\n    name\n}\nentity Buch {\n    id <<key>>\n    titel\n}\nrelationship ausleihe {\n    schuelerid_FK <<key>>\n    buchid_FK <<key>>\n    datum\n}\nSchueler -(0,m)- ausleihe\nausleihe -(0,n)- Buch\n@endchen"
                },
                Prerequisites = new List<string> { "Ausführungsreihenfolge von SQL-Klauseln" },
                OptionalPrerequisites = new List<string> { "Aliase (AS)" }
            },
            new()
            {
                Id = 13,
                Section = "Sektion 2: Relationen & Joins",
                SkipCode = SqlLevelCodes.CodesList[12],
                NextLevelCode = SqlLevelCodes.CodesList[13],
                Title = "Klasse 10b",
                Description =
                    "Der Direktor benötigt eine Übersicht über das Leseverhalten einer spezifischen Klasse.\n\n" +
                    "**Hinweis:** Ab diesem Level wird das relationale Datenbankmodel (Schema) nicht mehr gegeben sein, stattdessen können Sie es selbst vom gegebenen ER-Diagramm aus ableiten.\n\n" +
                    "**Aufgabe:**\n" +
                    "Nutzen Sie das gegebene ER-Diagramm.\n" +
                    "Geben Sie [name] und [titel] aller Ausleihen aus, aber **nur** für Schüler der Klasse '10b'.",
                SetupScript = "CREATE TABLE Schueler (id INTEGER PRIMARY KEY, name TEXT, klasse TEXT);" +
                              "CREATE TABLE Buch (id INTEGER PRIMARY KEY, titel TEXT);" +
                              "CREATE TABLE ausleihe (schuelerid_FK INTEGER, buchid_FK INTEGER, datum TEXT);" +
                              "INSERT INTO Schueler VALUES (1, 'Max', '10b');" +
                              "INSERT INTO Schueler VALUES (2, 'Lisa', '12a');" +
                              "INSERT INTO Schueler VALUES (3, 'Tom', '10b');" +
                              "INSERT INTO Buch VALUES (100, 'Faust');" +
                              "INSERT INTO Buch VALUES (101, 'HTML für Anfänger');" +
                              "INSERT INTO ausleihe VALUES (1, 100, '2023-01-01');" +
                              "INSERT INTO ausleihe VALUES (2, 100, '2023-01-05');" +
                              "INSERT INTO ausleihe VALUES (3, 101, '2023-02-01');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "titel", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Max", "Faust" },
                    new[] { "Tom", "HTML für Anfänger" }
                },
                MaterialDocs = "start-tipp: Kombination\n" +
                               "Verbinden Sie erst alle Tabellen per [JOIN].\n" +
                               "Filtern Sie das Ergebnis am Ende mit einer [WHERE]-Klausel.\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec2\\lvl13-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Schueler {\n    id <<key>>\n    name\n    klasse\n}\nentity Buch {\n    id <<key>>\n    titel\n}\nrelationship ausleihe {\n    schuelerid_FK <<key>>\n    buchid_FK <<key>>\n    datum\n}\nSchueler -(0,n)- ausleihe\nausleihe -(0,m)- Buch\n@endchen"
                },
                Prerequisites = new List<string>
                    { "ER-Diagramm lesen (Chen-Notation)", "Relationales Schema aus ER-Diagramm ableiten" },
                OptionalPrerequisites = new List<string>()
            },

            // --- SECTION 3 ---
            new()
            {
                Id = 14,
                Section = "Sektion 3: NULL & Outer Joins",
                SkipCode = SqlLevelCodes.CodesList[13],
                NextLevelCode = SqlLevelCodes.CodesList[14],
                Title = "VIPs ohne Tisch (LEFT JOIN)",
                Description =
                    "Wir verwalten ein exklusives Konzert. Es gibt eine Liste von VIPs und eine separate Tabelle für Tischreservierungen.\n\n" +
                    "Das Problem: Ein normaler [INNER JOIN] würde VIPs, die noch keine Reservierung haben, einfach 'verschlucken' (nicht anzeigen).\n\n" +
                    "**Aufgabe:**\n" +
                    "Erstellen Sie eine Liste aller VIPs und ihrer Tischnummern.\n" +
                    "Nutzen Sie einen **LEFT JOIN**, damit auch VIPs angezeigt werden, die noch keine Reservierung haben (bei diesen ist die Tischnummer dann [NULL]).\n\n" +
                    "Tipp: Probieren Sie ruhig auch einmal [INNER JOIN] statt [LEFT JOIN], um zu sehen, wie die VIPs ohne Tisch verschwinden.",
                SetupScript = "CREATE TABLE Vip (id INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Reservierung (vipid_FK INTEGER, tischNr INTEGER);" +
                              "INSERT INTO Vip VALUES (1, 'Taylor Swift');" +
                              "INSERT INTO Vip VALUES (2, 'Elon Musk');" +
                              "INSERT INTO Vip VALUES (3, 'Beyoncé');" +
                              "INSERT INTO Reservierung VALUES (1, 101);" +
                              "INSERT INTO Reservierung VALUES (3, 102);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "tischNr", Type = "INT", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Taylor Swift", "101" },
                    new[] { "Elon Musk", "NULL" },
                    new[] { "Beyoncé", "102" }
                },
                MaterialDocs = "start-hint: Warum LEFT?\n" +
                               "Der [LEFT JOIN] heißt so, weil er **alle** Datensätze der linken Tabelle (die vor dem JOIN steht, hier: Vip) behält.\n" +
                               "Findet er in der rechten Tabelle (Reservierung) keinen Partner, füllt er die Lücken mit [NULL] auf.\n" +
                               "Syntax:\n" +
                               "{|SELECT ... \nFROM LinkeTabelle \nLEFT JOIN RechteTabelle ON ...|}\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec3\\lvl14-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Vip {\n    id <<key>>\n    name\n}\nentity Reservierung {\n    vipid_FK <<key>>\n    tischNr\n}\nrelationship bucht {\n}\nVip -(1,1)- bucht\nbucht -(0,1)- Reservierung\n@endchen"
                },
                Prerequisites = new List<string> { "LEFT JOIN", "NULL-Werte" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 15,
                Section = "Sektion 3: NULL & Outer Joins",
                SkipCode = SqlLevelCodes.CodesList[14],
                NextLevelCode = SqlLevelCodes.CodesList[15],
                Title = "Die Geister-Gäste (IS NULL)",
                Description =
                    "Das Event-Team muss dringend wissen, welche VIPs noch **keinen** Sitzplatz haben, um ihnen einen zuzuweisen.\n\n" +
                    "Dies ist eine der häufigsten Abitur-Aufgabenstellungen: 'Finden Sie Datensätze, die keine Entsprechung haben'.\n\n" +
                    "**Aufgabe:**\n" +
                    "Nutzen Sie erneut den [LEFT JOIN] aus der vorherigen Aufgabe.\n" +
                    "Erweitern Sie die Abfrage um eine [WHERE]-Klausel, die nur die VIPs filtert, bei denen die [tischNr] leer ([NULL]) ist.\n" +
                    "Geben Sie nur den [name]n aus.",
                SetupScript = "CREATE TABLE Vip (id INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Reservierung (vipid_FK INTEGER, tischNr INTEGER);" +
                              "INSERT INTO Vip VALUES (1, 'Taylor Swift');" +
                              "INSERT INTO Vip VALUES (2, 'Elon Musk');" +
                              "INSERT INTO Vip VALUES (3, 'Beyoncé');" +
                              "INSERT INTO Reservierung VALUES (1, 101);" +
                              "INSERT INTO Reservierung VALUES (3, 102);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Elon Musk" }
                },
                MaterialDocs = "start-hint: Auf NULL prüfen\n" +
                               "In SQL kann man nicht [= NULL] schreiben.\n" +
                               "Man muss den Operator [IS NULL] verwenden:\n" +
                               "{|WHERE spalte IS NULL|}\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec3\\lvl14-1.svg"
                },
                PlantUMLSources = new List<string>(), // shared
                Prerequisites = new List<string> { "IS NULL / IS NOT NULL" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 16,
                Section = "Sektion 3: NULL & Outer Joins",
                SkipCode = SqlLevelCodes.CodesList[15],
                NextLevelCode = SqlLevelCodes.CodesList[16],
                Title = "Doppelte Einträge (DISTINCT)",
                Description =
                    "Für eine gezielte Marketingkampagne benötigen wir eine Liste aller Städte, aus denen unsere VIP-Gäste anreisen.\n\n" +
                    "In der Tabelle [Gast] kommen Städte mehrfach vor (z.B. kommen viele Gäste aus Berlin). Zudem gibt es nun eine Tabelle [Ticket], die angibt, welchen Bereich ein Gast gebucht hat.\n\n" +
                    "**Aufgabe:**\n" +
                    "Ermitteln Sie eine Liste der Städte aus der Tabelle [Gast], aber **nur** von Gästen, die ein Ticket für den Bereich 'VIP' haben.\n" +
                    "Jede Stadt darf dabei **nur einmal** in der Ergebnisliste auftauchen.",
                SetupScript = "CREATE TABLE Gast (id INTEGER PRIMARY KEY, name TEXT, stadt TEXT);" +
                              "CREATE TABLE Ticket (id INTEGER PRIMARY KEY, gastid_FK INTEGER, bereich TEXT);" +
                              "INSERT INTO Gast VALUES (1, 'Müller', 'Berlin');" +
                              "INSERT INTO Gast VALUES (2, 'Schmidt', 'München');" +
                              "INSERT INTO Gast VALUES (3, 'Schneider', 'Berlin');" +
                              "INSERT INTO Gast VALUES (4, 'Fischer', 'Hamburg');" +
                              "INSERT INTO Gast VALUES (5, 'Weber', 'München');" +
                              "INSERT INTO Ticket VALUES (101, 1, 'VIP');" +
                              "INSERT INTO Ticket VALUES (102, 2, 'Standard');" +
                              "INSERT INTO Ticket VALUES (103, 3, 'VIP');" +
                              "INSERT INTO Ticket VALUES (104, 4, 'Standard');" +
                              "INSERT INTO Ticket VALUES (105, 5, 'VIP');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "stadt", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Berlin" },
                    new[] { "München" }
                },
                MaterialDocs = "start-hint: Duplikate entfernen\n" +
                               "Nutzen Sie das Schlüsselwort [DISTINCT] direkt nach dem [SELECT]:\n" +
                               "{|SELECT DISTINCT spalte FROM Tabelle|}\n" +
                               ":end-hint\n" +
                               "start-tipp: Kombination\n" +
                               "Sie müssen zuerst die beiden Tabellen verknüpfen ([JOIN]) und nach dem Bereich filtern ([WHERE]), bevor Sie die Städte ohne Duplikate ausgeben.\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec3\\lvl16-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Gast {\n    id <<key>>\n    name\n    stadt\n}\nentity Ticket {\n    id <<key>>\n    gastid_FK\n    bereich\n}\nrelationship bucht {\n}\nGast -(1,1)- bucht\nbucht -(0,n)- Ticket\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "DISTINCT" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 17,
                Section = "Sektion 3: NULL & Outer Joins",
                SkipCode = SqlLevelCodes.CodesList[16],
                NextLevelCode = SqlLevelCodes.CodesList[17],
                Title = "Die Problem-Gäste",
                Description = "Dies ist die Abschlussprüfung für Sektion 3.\n\n" +
                              "Das Event-Team ist in Panik: Einige VIPs hängen 'in der Luft' und könnten unzufrieden sein. Wir brauchen eine Liste dieser Personen.\n\n" +
                              "**Regeln:**\n" +
                              "1. Ein VIP gilt als Problem-Gast, wenn er **noch gar keine Reservierung** getätigt hat.\n" +
                              "2. Ein VIP gilt ebenfalls als Problem-Gast, wenn er eine Reservierung für den **'Hauptbereich'** hat, aber dort **noch keine Tischnummer zugewiesen** wurde.\n" +
                              "3. VIPs, denen in anderen Bereichen (z. B. 'VIP-Lounge') noch kein Tisch zugewiesen wurde, sind für diese Liste irrelevant.\n" +
                              "4. Jeder Name darf **nur einmal** auf der Liste stehen.\n\n" +
                              "**Aufgabe:**\n" +
                              "Geben Sie die [Name]n dieser Problem-VIPs aus.",
                SetupScript = "CREATE TABLE Vip (id INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Reservierung (vipid_FK INTEGER, bereich TEXT, tischNr INTEGER);" +
                              "INSERT INTO Vip VALUES (1, 'Taylor Swift');" +
                              "INSERT INTO Vip VALUES (2, 'Elon Musk');" +
                              "INSERT INTO Vip VALUES (3, 'Beyoncé');" +
                              "INSERT INTO Vip VALUES (4, 'Ed Sheeran');" +
                              "INSERT INTO Vip VALUES (5, 'Rihanna');" +
                              "INSERT INTO Vip VALUES (6, 'Drake');" +
                              "INSERT INTO Reservierung VALUES (1, 'Hauptbereich', 101);" +
                              "INSERT INTO Reservierung VALUES (1, 'VIP-Lounge', 12);" +
                              "INSERT INTO Reservierung VALUES (2, 'VIP-Lounge', 110);" +
                              "INSERT INTO Reservierung VALUES (3, 'Hauptbereich', NULL);" +
                              "INSERT INTO Reservierung VALUES (4, 'Hauptbereich', 105);" +
                              "INSERT INTO Reservierung VALUES (6, 'VIP-Lounge', NULL);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Beyoncé" },
                    new[] { "Rihanna" }
                },
                MaterialDocs = "start-tipp: Strategie\n" +
                               "1. Starten Sie mit der [Vip]-Tabelle und nutzen Sie einen [LEFT JOIN] zur Reservierung. Ein normaler JOIN würde VIPs ohne Reservierung verwerfen!\n" +
                               "2. Im [WHERE]-Teil müssen Sie zwei Fälle mit [OR] kombinieren:\n" +
                               "   - Fall A: [bereich] ist 'Hauptbereich' UND [tischNr] ist NULL.\n" +
                               "   - Fall B: Der VIP hat gar keine Reservierung (die FK-Spalte der rechten Tabelle ist [NULL]).\n" +
                               "3. Nutzen Sie Klammern für die UND/ODER Logik und [DISTINCT] gegen Duplikate.\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec3\\lvl17-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Vip {\n    id <<key>>\n    name\n}\nentity Reservierung {\n    vipid_FK <<key>>\n    bereich\n    tischNr\n}\nrelationship bucht {\n}\nVip -(1,1)- bucht\nbucht -(0,n)- Reservierung\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "Logische Operatoren (AND, OR, NOT)" },
                OptionalPrerequisites = new List<string>()
            },

            // --- SECTION 4 ---
            new()
            {
                Id = 18,
                Section = "Sektion 4: Aggregation & Mathe",
                SkipCode = SqlLevelCodes.CodesList[17],
                NextLevelCode = SqlLevelCodes.CodesList[18],
                Title = "Der Warenkorb (Rechnen im Select)",
                Description =
                    "Wir werten die Datenbank eines E-Commerce-Shops aus. Im ersten Schritt müssen wir den Wert einzelner Warenkorb-Positionen berechnen.\n\n" +
                    "Zur Erklärung: Eine **Position** (oder Bestellposition) repräsentiert eine einzelne Zeile auf einem Kassenbon oder in einem Warenkorb (z.B. '3x Socken'). Sie verknüpft das eigentliche Produkt mit der gekauften Menge.\n\n" +
                    "**Aufgabe:**\n" +
                    "Berechnen Sie für jede Position den Gesamtpreis (Preis multipliziert mit der Menge). \n" +
                    "Geben Sie die [bezeichnung] des Produkts und den berechneten Wert unter dem Alias 'Gesamt' aus.",
                SetupScript = "CREATE TABLE Produkt (id INTEGER PRIMARY KEY, bezeichnung TEXT, preis REAL);" +
                              "CREATE TABLE Position (id INTEGER PRIMARY KEY, produktid_FK INTEGER, menge INTEGER);" +
                              "INSERT INTO Produkt VALUES (1, 'T-Shirt', 19.99);" +
                              "INSERT INTO Produkt VALUES (2, 'Hose', 49.90);" +
                              "INSERT INTO Produkt VALUES (3, 'Socken', 5.50);" +
                              "INSERT INTO Position VALUES (101, 1, 2);" +
                              "INSERT INTO Position VALUES (102, 2, 1);" +
                              "INSERT INTO Position VALUES (103, 3, 3);" +
                              "INSERT INTO Position VALUES (104, 1, 1);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "bezeichnung", Type = "VARCHAR(255)", StrictName = true },
                    new() { Name = "Gesamt", Type = "DOUBLE", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "T-Shirt", "39.98" },
                    new[] { "Hose", "49.9" },
                    new[] { "Socken", "16.5" },
                    new[] { "T-Shirt", "19.99" }
                },
                MaterialDocs = "start-hint: Rechnen in SQL\n" +
                               "Sie können in der [SELECT]-Klausel mathematische Operatoren verwenden, um Spalten miteinander zu verrechnen:\n" +
                               "{|SELECT spalteA, spalteX * spalteY AS AliasName FROM ...|}\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec4\\lvl18-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Produkt {\n    id <<key>>\n    bezeichnung\n    preis\n}\nentity Position {\n    id <<key>>\n    produktid_FK\n    menge\n}\nrelationship enthaelt {\n}\nProdukt -(1,1)- enthaelt\nenthaelt -(0,n)- Position\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "Arithmetische Ausdrücke" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 19,
                Section = "Sektion 4: Aggregation & Mathe",
                SkipCode = SqlLevelCodes.CodesList[18],
                NextLevelCode = SqlLevelCodes.CodesList[19],
                Title = "Der Tagesumsatz (SUM)",
                Description =
                    "Die Geschäftsführung möchte wissen, wie viel Geld heute insgesamt eingenommen wurde.\n\n" +
                    "**Hinweis:** Ab diesem Level werden Fremdschlüssel nicht mehr im ER-Diagramm angezeigt. Sie müssen anhand der Kardinalitäten selbst ableiten, wie die Tabellen verknüpft werden (siehe Material).\n\n" +
                    "**Aufgabe:**\n" +
                    "Ermitteln Sie den gesamten Umsatz (Summe aus Preis * Menge) für alle Bestellungen, die am '2024-02-28' getätigt wurden.\n" +
                    "Geben Sie das Ergebnis als 'Tagesumsatz' aus.",
                SetupScript = "CREATE TABLE Bestellung (id INTEGER PRIMARY KEY, datum TEXT);" +
                              "CREATE TABLE Position (id INTEGER PRIMARY KEY, bestellungid_FK INTEGER, preis REAL, menge INTEGER);" +
                              "INSERT INTO Bestellung VALUES (1, '2024-02-28');" +
                              "INSERT INTO Bestellung VALUES (2, '2024-02-27');" +
                              "INSERT INTO Position VALUES (101, 1, 10.0, 2);" +
                              "INSERT INTO Position VALUES (102, 1, 15.0, 1);" +
                              "INSERT INTO Position VALUES (103, 2, 5.0, 4);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "Tagesumsatz", Type = "DOUBLE", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "35" }
                },
                MaterialDocs = "start-hint: Überführung ins relationale Modell\n" +
                               "1. [1:1-Beziehung]: Der Primärschlüssel einer der beiden Tabellen wird als Fremdschlüssel in die andere Tabelle übernommen.\n" +
                               "2. [1:n-Beziehung]: Der Primärschlüssel der 1-Seite wird als Fremdschlüssel in die Tabelle der n-Seite eingetragen.\n" +
                               "3. [n:m-Beziehung]: Es entsteht eine neue Verknüpfungstabelle, die die Primärschlüssel beider Tabellen als Fremdschlüssel enthält.\n" +
                               ":end-hint\n" +
                               "start-tipp: Datumswerte in SQL\n" +
                               "Ein Datum wird in SQL standardmäßig als Text im Format ['YYYY-MM-DD'] (Jahr-Monat-Tag) geschrieben, z.B. '2024-02-28'.\n" +
                               ":end-hint\n" +
                               "start-hint: Aggregatfunktionen\n" +
                               "Nutzen Sie die Funktion [SUM()], um alle Einzelwerte einer Spalte (oder einer Berechnung) zu addieren.\n" +
                               "{|SELECT SUM(spalteA * spalteB) FROM ...|}\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec4\\lvl19-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Bestellung {\n    id <<key>>\n    datum\n}\nentity Position {\n    id <<key>>\n    preis\n    menge\n}\nrelationship umfasst {\n}\nBestellung -(1,1)- umfasst\numfasst -(1,n)- Position\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "SUM()", "1:1 Beziehungen", "1:n Beziehungen", "n:m Beziehungen" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 20,
                Section = "Sektion 4: Aggregation & Mathe",
                SkipCode = SqlLevelCodes.CodesList[19],
                NextLevelCode = SqlLevelCodes.CodesList[20],
                Title = "Topseller (GROUP & ORDER BY)",
                Description =
                    "Welches Produkt wurde wie oft verkauft? Wir wollen unsere Verkaufsschlager identifizieren.\n\n" +
                    "Denken Sie daran: Überlegen Sie sich, wie die Tabellen verknüpft werden (siehe Material).\n\n" +
                    "**Aufgabe:**\n" +
                    "Ermitteln Sie den [name]n der Produkte und die insgesamt verkaufte Menge (als 'Anzahl').\n" +
                    "Sortieren Sie die Liste **absteigend** nach der Anzahl, sodass das meistverkaufte Produkt oben steht.",
                SetupScript = "CREATE TABLE Produkt (id INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Position (id INTEGER PRIMARY KEY, produktid_FK INTEGER, menge INTEGER);" +
                              "INSERT INTO Produkt VALUES (1, 'T-Shirt');" +
                              "INSERT INTO Produkt VALUES (2, 'Hose');" +
                              "INSERT INTO Produkt VALUES (3, 'Schuhe');" +
                              "INSERT INTO Position VALUES (101, 1, 3);" +
                              "INSERT INTO Position VALUES (102, 1, 2);" +
                              "INSERT INTO Position VALUES (103, 2, 1);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = true },
                    new() { Name = "Anzahl", Type = "INT", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "T-Shirt", "5" },
                    new[] { "Hose", "1" }
                },
                MaterialDocs = "start-hint: Überführung ins relationale Modell\n" +
                               "1. [1:1-Beziehung]: Der Primärschlüssel einer der beiden Tabellen wird als Fremdschlüssel in die andere Tabelle übernommen.\n" +
                               "2. [1:n-Beziehung]: Der Primärschlüssel der 1-Seite wird als Fremdschlüssel in die Tabelle der n-Seite eingetragen.\n" +
                               "3. [n:m-Beziehung]: Es entsteht eine neue Verknüpfungstabelle, die die Primärschlüssel beider Tabellen als Fremdschlüssel enthält.\n" +
                               ":end-hint\n" +
                               "start-tipp: Gruppieren\n" +
                               "Wenn Sie nach einer Kategorie oder einem Namen zusammenfassen wollen, nutzen Sie [GROUP BY spalte].\n" +
                               "Die restlichen Spalten in der [SELECT]-Anweisung müssen dann aggregiert werden (z.B. mit [SUM]).\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec4\\lvl20-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Produkt {\n    id <<key>>\n    name\n}\nentity Position {\n    id <<key>>\n    menge\n}\nrelationship verkauft {\n}\nProdukt -(1,1)- verkauft\nverkauft -(0,n)- Position\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "GROUP BY", "SUM()" },
                OptionalPrerequisites = new List<string> { "ORDER BY (ASC / DESC)" }
            },
            new()
            {
                Id = 21,
                Section = "Sektion 4: Aggregation & Mathe",
                SkipCode = SqlLevelCodes.CodesList[20],
                NextLevelCode = SqlLevelCodes.CodesList[21],
                Title = "Die Ladenhüter (HAVING)",
                Description =
                    "Wir möchten unser Sortiment bereinigen und Kategorien finden, die sich schlecht verkaufen.\n\n" +
                    "**Aufgabe:**\n" +
                    "Ermitteln Sie die [kategorie] und die insgesamt verkaufte Menge als 'Verkauft'.\n" +
                    "Zeigen Sie **nur** Kategorien an, bei denen die Summe der verkauften Menge **kleiner als 5** ist.\n" +
                    "Sortieren Sie die Liste, damit die am schlechtesten verkauften Produkte zuerst gelistet sind.",
                SetupScript = "CREATE TABLE Produkt (id INTEGER PRIMARY KEY, kategorie TEXT);" +
                              "CREATE TABLE Position (id INTEGER PRIMARY KEY, produktid_FK INTEGER, menge INTEGER);" +
                              "INSERT INTO Produkt VALUES (1, 'Elektronik');" +
                              "INSERT INTO Produkt VALUES (2, 'Kleidung');" +
                              "INSERT INTO Produkt VALUES (3, 'Bücher');" +
                              "INSERT INTO Position VALUES (101, 1, 2);" +
                              "INSERT INTO Position VALUES (102, 1, 1);" +
                              "INSERT INTO Position VALUES (103, 2, 10);" +
                              "INSERT INTO Position VALUES (104, 3, 4);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "kategorie", Type = "VARCHAR(255)", StrictName = true },
                    new() { Name = "Verkauft", Type = "INT", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Elektronik", "3" },
                    new[] { "Bücher", "4" }
                },
                MaterialDocs = "start-hint: Überführung ins relationale Modell\n" +
                               "1. [1:1-Beziehung]: Der Primärschlüssel einer der beiden Tabellen wird als Fremdschlüssel in die andere Tabelle übernommen.\n" +
                               "2. [1:n-Beziehung]: Der Primärschlüssel der 1-Seite wird als Fremdschlüssel in die Tabelle der n-Seite eingetragen.\n" +
                               "3. [n:m-Beziehung]: Es entsteht eine neue Verknüpfungstabelle, die die Primärschlüssel beider Tabellen als Fremdschlüssel enthält.\n" +
                               ":end-hint\n" +
                               "start-hint: HAVING vs. WHERE\n" +
                               "Die [WHERE]-Klausel filtert Daten **vor** der Gruppierung.\n" +
                               "Um nach einem aggregierten Wert wie [SUM()] zu filtern, müssen Sie die [HAVING]-Klausel **nach** dem [GROUP BY] verwenden:\n" +
                               "{|GROUP BY spalteX HAVING SUM(spalteY) < 10|}\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec4\\lvl21-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Produkt {\n    id <<key>>\n    kategorie\n}\nentity Position {\n    id <<key>>\n    menge\n}\nrelationship verkauft {\n}\nProdukt -(1,1)- verkauft\nverkauft -(0,n)- Position\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "HAVING" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 22,
                Section = "Sektion 4: Aggregation & Mathe",
                SkipCode = SqlLevelCodes.CodesList[21],
                NextLevelCode = SqlLevelCodes.CodesList[22],
                Title = "Die Umsatzanalyse",
                Description =
                    "Der Abteilungsleiter verlangt einen umfassenden Bericht zum aktuellen Geschäftsjahr.\n\n" +
                    "**Aufgabe:**\n" +
                    "Zeigen Sie alle Produktkategorien und deren Gesamtumsatz als 'Gesamtumsatz' an.\n" +
                    "Es sollen jedoch **nur** Kategorien angezeigt werden, deren Gesamtumsatz **über 100 Euro** liegt.\n" +
                    "Sortieren Sie das Ergebnis **absteigend** nach dem Gesamtumsatz.",
                SetupScript = "CREATE TABLE Produkt (id INTEGER PRIMARY KEY, kategorie TEXT, preis REAL);" +
                              "CREATE TABLE Position (id INTEGER PRIMARY KEY, produktid_FK INTEGER, menge INTEGER);" +
                              "INSERT INTO Produkt VALUES (1, 'Elektronik', 250.0);" +
                              "INSERT INTO Produkt VALUES (2, 'Kleidung', 20.0);" +
                              "INSERT INTO Produkt VALUES (3, 'Medien', 15.0);" +
                              "INSERT INTO Position VALUES (101, 1, 1);" + // elektroniks split into 2 rows (250 * 1)
                              "INSERT INTO Position VALUES (102, 1, 1);" + // (250 * 1) -> total: 500
                              "INSERT INTO Position VALUES (103, 2, 3);" + // kleidung split into 2 rows (20 * 3)
                              "INSERT INTO Position VALUES (104, 2, 3);" + // (20 * 3) -> total: 120
                              "INSERT INTO Position VALUES (105, 3, 2);", // medien (15 * 2) -> total: 30

                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "kategorie", Type = "VARCHAR(255)", StrictName = true },
                    new() { Name = "Gesamtumsatz", Type = "DOUBLE", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Elektronik", "500" },
                    new[] { "Kleidung", "120" }
                },
                MaterialDocs = "start-tipp: Alles kombinieren\n" +
                               "1. Tabellen per [JOIN] verknüpfen.\n" +
                               "2. Mit [GROUP BY] zusammenfassen.\n" +
                               "3. Mit [HAVING] filtern.\n" +
                               "4. Mit [ORDER BY] sortieren.\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec4\\lvl22-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Produkt {\n    id <<key>>\n    kategorie\n    preis\n}\nentity Position {\n    id <<key>>\n    menge\n}\nrelationship verkauft {\n}\nProdukt -(1,1)- verkauft\nverkauft -(0,n)- Position\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "GROUP BY", "HAVING", "INNER JOIN ... ON", "ORDER BY (ASC / DESC)" },
                OptionalPrerequisites = new List<string>()
            },

            // --- SECTION 5 ---
            new()
            {
                Id = 23,
                Section = "Sektion 5: Datumsfunktionen",
                SkipCode = SqlLevelCodes.CodesList[22],
                NextLevelCode = SqlLevelCodes.CodesList[23],
                Title = "Der Check-in (YEAR & MONTH)",
                Description = "Willkommen im Hotel-Management-System!\n\n" +
                              "**WICHTIG:** Ab sofort müssen Sie das relationale Schema selbst aus dem ER-Diagramm ableiten (Überführung in die 3. Normalform).\n" +
                              "Ebenfalls werden die Aufgabenstellungen verständlich anspruchsvoller, da nichts mehr hervorgehoben wird und die Sprache mehr den Abiturstandards entspricht.\n\n" +
                              "**Aufgabe:**\n" +
                              "Zeigen Sie den Namen des Gastes und das Anreisedatum für alle Buchungen an, die im Jahr 2024 stattfinden.",
                SetupScript = "CREATE TABLE Gast (id INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Buchung (id INTEGER PRIMARY KEY, Gastid_FK INTEGER, anreise TEXT, abreise TEXT);" +
                              "INSERT INTO Gast VALUES (1, 'Thomas Müller');" +
                              "INSERT INTO Gast VALUES (2, 'Sabine Schmidt');" +
                              "INSERT INTO Gast VALUES (3, 'Lisa Meier');" +
                              "INSERT INTO Buchung VALUES (101, 1, '2024-05-12', '2024-05-20');" +
                              "INSERT INTO Buchung VALUES (102, 2, '2023-12-28', '2024-01-05');" +
                              "INSERT INTO Buchung VALUES (103, 3, '2024-08-01', '2024-08-14');" +
                              "INSERT INTO Buchung VALUES (104, 1, '2025-02-10', '2025-02-15');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "anreise", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Thomas Müller", "2024-05-12" },
                    new[] { "Lisa Meier", "2024-08-01" }
                },
                MaterialDocs = "start-hint: Datumsfunktionen\n" +
                               "Mit der Funktion [YEAR(spalte)] können Sie das Jahr aus einem Datum extrahieren, mit [MONTH(spalte)] den Monat.\n" +
                               "Beispiel:\n" +
                               "{|WHERE YEAR(Bestelldatum) = \"2023\"|}\n" +
                               ":end-hint\n",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec5\\lvl23-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Gast {\n    id <<key>>\n    name\n}\nentity Buchung {\n    id <<key>>\n    anreise\n    abreise\n}\nrelationship taetigt {\n}\nGast -(1,1)- taetigt\ntaetigt -(0,n)- Buchung\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "YEAR()", "MONTH()", "Normalisierung (3NF)" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 24,
                Section = "Sektion 5: Datumsfunktionen",
                SkipCode = SqlLevelCodes.CodesList[23],
                NextLevelCode = SqlLevelCodes.CodesList[24],
                Title = "Die Hochsaison (BETWEEN)",
                Description =
                    "Wir erwarten in den Sommerferien einen großen Ansturm und müssen das Personal planen.\n\n" +
                    "**Aufgabe:**\n" +
                    "Geben Sie alle Namen der Gäste aus, deren Anreisedatum im Zeitraum vom '2024-07-01' bis zum '2024-08-31' (jeweils einschließlich) liegt.",
                SetupScript = "CREATE TABLE Gast (id INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Buchung (id INTEGER PRIMARY KEY, gastid_FK INTEGER, anreise TEXT, abreise TEXT);" +
                              "INSERT INTO Gast VALUES (1, 'Familie Richter');" +
                              "INSERT INTO Gast VALUES (2, 'Familie Baum');" +
                              "INSERT INTO Gast VALUES (3, 'Herr Klee');" +
                              "INSERT INTO Buchung VALUES (101, 1, '2024-07-15', '2024-07-29');" +
                              "INSERT INTO Buchung VALUES (102, 2, '2024-06-25', '2024-07-05');" + // start before july
                              "INSERT INTO Buchung VALUES (103, 3, '2024-08-30', '2024-09-05');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Familie Richter" },
                    new[] { "Herr Klee" }
                },
                MaterialDocs = "start-hint: Zeitraum filtern (BETWEEN)\n" +
                               "Statt [spalte >= wert1 AND spalte <= wert2] zu schreiben, bietet SQL den eleganteren [BETWEEN]-Operator:\n" +
                               "Beispiel:\n" +
                               "{|WHERE kaufdatum BETWEEN '2023-01-01' AND '2023-12-31'|}\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec5\\lvl23-1.svg"
                },
                PlantUMLSources = new List<string>(), // shared
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "BETWEEN" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 25,
                Section = "Sektion 5: Datumsfunktionen",
                SkipCode = SqlLevelCodes.CodesList[24],
                NextLevelCode = SqlLevelCodes.CodesList[25],
                Title = "Überfällig (NOW / Datumsvergleich)",
                Description = "Das System soll prüfen, ob Gäste vergessen haben auszuchecken.\n\n" +
                              "**Aufgabe:**\n" +
                              "Ermitteln Sie die Identifikationsnummer der Buchung und den Namen des Gastes für alle Buchungen, deren Abreisedatum bereits in der Vergangenheit liegt.",
                SetupScript = "CREATE TABLE Gast (id INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Buchung (id INTEGER PRIMARY KEY, gastid_FK INTEGER, anreise TEXT, abreise TEXT);" +
                              "INSERT INTO Gast VALUES (1, 'Max Mustermann');" +
                              "INSERT INTO Gast VALUES (2, 'Anna Nass');" +
                              "INSERT INTO Buchung VALUES (101, 1, '2020-01-01', '2020-01-10');" + // expired
                              "INSERT INTO Buchung VALUES (102, 2, '2050-05-01', '2050-05-15');", // future
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "id", Type = "INT", StrictName = false },
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "101", "Max Mustermann" }
                },
                MaterialDocs = "start-hint: Die aktuelle Zeit (NOW)\n" +
                               "Die Funktion [NOW()] liefert den aktuellen Zeitstempel (Datum und Uhrzeit) des Systems zurück.\n" +
                               "Datumsangaben können in SQL ganz normal mit [<] oder [>] verglichen werden, um zu prüfen, ob ein Zeitpunkt bereits verstrichen ist.\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec5\\lvl23-1.svg"
                },
                PlantUMLSources = new List<string>(), // shared
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "NOW()", "Datumsvergleiche" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 26,
                Section = "Sektion 5: Datumsfunktionen",
                SkipCode = SqlLevelCodes.CodesList[25],
                NextLevelCode = SqlLevelCodes.CodesList[26],
                Title = "Aufenthaltsdauer (DATEDIFF)",
                Description =
                    "Um Rechnungen stellen zu können, müssen wir wissen, wie viele Nächte ein Gast bei uns verbringt.\n\n" +
                    "**Aufgabe:**\n" +
                    "Geben Sie für jede Buchung die Identifikationsnummer und die berechnete Aufenthaltsdauer (als 'Naechte') in Tagen aus.",
                SetupScript =
                    "CREATE TABLE Buchung (id INTEGER PRIMARY KEY, gastid_FK INTEGER, anreise TEXT, abreise TEXT);" +
                    "INSERT INTO Buchung VALUES (101, 1, '2024-01-01', '2024-01-05');" + // 4 nights
                    "INSERT INTO Buchung VALUES (102, 2, '2024-02-10', '2024-02-20');" + // 10 nights
                    "INSERT INTO Buchung VALUES (103, 3, '2024-03-01', '2024-03-02');", // 1 night
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "id", Type = "INT", StrictName = false },
                    new() { Name = "Naechte", Type = "INT", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "101", "4" },
                    new[] { "102", "10" },
                    new[] { "103", "1" }
                },
                MaterialDocs = "start-hint: Differenz berechnen (DATEDIFF)\n" +
                               "Die Funktion [DATEDIFF(enddatum, startdatum)] berechnet die Differenz zwischen zwei Daten in Tagen.\n\n" +
                               "Beispiel:\n" +
                               "{|SELECT DATEDIFF('2023-12-31', '2023-12-01'); -- Ergibt 30|}" +
                               "[DATEDIFF] tut folgendes: ['2023-12-31' - '2023-12-01' = 30]\n" +
                               "Deshalb ist die Reihenfolge nicht irrelevant!\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec5\\lvl26-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Buchung {\n    id <<key>>\n    anreise\n    abreise\n}\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "DATEDIFF()", "Aliase (AS)" },
                OptionalPrerequisites = new List<string> { "TIMEDIFF()" }
            },
            new()
            {
                Id = 27,
                Section = "Sektion 5: Datumsfunktionen",
                SkipCode = SqlLevelCodes.CodesList[26],
                NextLevelCode = SqlLevelCodes.CodesList[27],
                Title = "Feedback-Mails (DATE_ADD & LIMIT)",
                Description =
                    "Das Hotel bittet Gäste nach der Abreise um Feedback. Ein automatisiertes System soll E-Mails vorbereiten.\n\n" +
                    "**ER-Diagramm:** Das ER-Modell wurde deutlich erweitert. Achten Sie auf die Kardinalitäten und setzen Sie diese in das Relationenmodell um.\n\n" +
                    "**Aufgabe:**\n" +
                    "Das Hotel verschickt genau 7 Tage nach Abreise eine E-Mail.\n" +
                    "Geben Sie die Identifikationsnummer der Buchung, den Namen des Gastes und das berechnete Versanddatum als 'FeedbackDatum' aus.\n" +
                    "Damit das System nicht überlastet wird, sollen nur die 3 spätesten Abreisen (chronologisch absteigend sortiert) ausgelesen werden.",
                SetupScript = "CREATE TABLE Gast (id INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Zimmer (id INTEGER PRIMARY KEY, nummer TEXT);" +
                              "CREATE TABLE Buchung (id INTEGER PRIMARY KEY, gastid_FK INTEGER, zimmerid_FK INTEGER, anreise TEXT, abreise TEXT);" +
                              "INSERT INTO Gast VALUES (1, 'Klaus');" +
                              "INSERT INTO Gast VALUES (2, 'Berta');" +
                              "INSERT INTO Zimmer VALUES (1, '101');" +
                              "INSERT INTO Buchung VALUES (101, 1, 1, '2024-05-01', '2024-05-10');" +
                              "INSERT INTO Buchung VALUES (102, 2, 1, '2024-06-01', '2024-06-05');" +
                              "INSERT INTO Buchung VALUES (103, 1, 1, '2024-07-01', '2024-07-15');" +
                              "INSERT INTO Buchung VALUES (104, 2, 1, '2024-08-01', '2024-08-10');" +
                              "INSERT INTO Buchung VALUES (105, 1, 1, '2024-09-01', '2024-09-05');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "id", Type = "INT", StrictName = false },
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "FeedbackDatum", Type = "VARCHAR(255)", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "105", "Klaus", "2024-09-12" }, // 05.09. + 7
                    new[] { "104", "Berta", "2024-08-17" }, // 10.08. + 7
                    new[] { "103", "Klaus", "2024-07-22" } // 15.07. + 7
                },
                MaterialDocs = "start-hint: Datums-Addition (DATE_ADD)\n" +
                               "Mit [DATE_ADD] können Sie einem Datum einen bestimmten Zeitraum hinzufügen:\n" +
                               "Beispiel (14 Tage zu einem Kaufdatum addieren):\n" +
                               "{|DATE_ADD(kaufdatum, INTERVAL 14 DAY)|}\n" +
                               ":end-hint\n" +
                               "start-hint: Ergebnisse begrenzen (LIMIT)\n" +
                               "Wenn Sie nur die ersten X Ergebnisse einer Abfrage benötigen, fügen Sie am Ende den Befehl [LIMIT X] ein.\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec5\\lvl27-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Gast {\n    id <<key>>\n    name\n}\nentity Buchung {\n    id <<key>>\n    anreise\n    abreise\n}\nentity Zimmer {\n    id <<key>>\n    nummer\n}\nrelationship taetigt {\n}\nrelationship reserviert {\n}\nGast -(1,1)- taetigt\ntaetigt -(0,n)- Buchung\nBuchung -(0,n)- reserviert\nreserviert -(1,1)- Zimmer\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "DATE_ADD()", "LIMIT", "ORDER BY (ASC / DESC)" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 28,
                Section = "Sektion 5: Datumsfunktionen",
                SkipCode = SqlLevelCodes.CodesList[27],
                NextLevelCode = SqlLevelCodes.CodesList[28],
                Title = "Die Hotel-Bilanz",
                Description =
                    "Der Chef möchte zum Jahresabschluss die treuesten Kunden belohnen. Diese Mini-Prüfung verlangt alles aus Sektion 4 und 5!\n\n" +
                    "**Aufgabe:**\n" +
                    "Ermitteln Sie den Namen der Top 3 Gäste, die im Jahr 2024 angereist sind und in einem **Premium-Zimmer (Zimmernummern '101' bis '109')** übernachtet haben, basierend auf der Gesamtsumme ihrer gebuchten Nächte (nennen Sie diese Spalte 'GesamtNaechte').\n" +
                    "Sortieren Sie die Liste absteigend, sodass der Gast mit den meisten Gesamtnächten auf Platz 1 steht.",
                SetupScript = "CREATE TABLE Gast (id INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Zimmer (id INTEGER PRIMARY KEY, nummer TEXT);" +
                              "CREATE TABLE Buchung (id INTEGER PRIMARY KEY, gastid_FK INTEGER, zimmerid_FK INTEGER, anreise TEXT, abreise TEXT);" +
                              "INSERT INTO Gast VALUES (1, 'Herr Müller');" +
                              "INSERT INTO Gast VALUES (2, 'Frau Schmidt');" +
                              "INSERT INTO Gast VALUES (3, 'Familie Wagner');" +
                              "INSERT INTO Gast VALUES (4, 'Herr Schulz');" +
                              "INSERT INTO Gast VALUES (5, 'Frau Klein');" +
                              "INSERT INTO Gast VALUES (6, 'Herr Richter');" +
                              "INSERT INTO Gast VALUES (7, 'Frau Baum');" +
                              "INSERT INTO Zimmer VALUES (1, '101');" +
                              "INSERT INTO Zimmer VALUES (2, '105');" +
                              "INSERT INTO Zimmer VALUES (3, '109');" +
                              "INSERT INTO Zimmer VALUES (4, '202');" +
                              "INSERT INTO Buchung VALUES (101, 1, 1, '2024-01-01', '2024-01-11');" + // 10 nights (müller)
                              "INSERT INTO Buchung VALUES (102, 1, 2, '2024-05-01', '2024-05-06');" + // 5 nights -> müller total: 15
                              "INSERT INTO Buchung VALUES (103, 2, 3, '2024-02-01', '2024-02-21');" + // 20 nights -> schmidt total: 20
                              "INSERT INTO Buchung VALUES (104, 3, 4, '2024-07-01', '2024-07-08');" + // 7 nights -> ignored (wrong room)
                              "INSERT INTO Buchung VALUES (105, 4, 1, '2023-12-01', '2023-12-31');" + // 30 nights -> ignored (wrong year)
                              "INSERT INTO Buchung VALUES (106, 5, 2, '2024-03-01', '2024-03-08');" + // 7 nights -> klein total: 7
                              "INSERT INTO Buchung VALUES (107, 6, 1, '2024-04-01', '2024-04-13');" + // 12 nights -> richter total: 12
                              "INSERT INTO Buchung VALUES (108, 7, 3, '2024-06-01', '2024-06-10');", // 9 nights -> baum total: 9
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "GesamtNaechte", Type = "INT", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Frau Schmidt", "20" },
                    new[] { "Herr Müller", "15" },
                    new[] { "Herr Richter", "12" }
                },
                MaterialDocs = "start-tipp: Die perfekte Kombination\n" +
                               "Denken Sie an die SQL-Reihenfolge:\n" +
                               "1. [SELECT] (Mit einer Aggregationsfunktion)\n" +
                               "2. [FROM] und [JOIN]\n" +
                               "3. [WHERE] (Filterung)\n" +
                               "4. [GROUP BY]\n" +
                               "5. [ORDER BY]\n" +
                               "6. [LIMIT]\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec5\\lvl27-1.svg"
                },
                PlantUMLSources = new List<string>(), // shared
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "DATEDIFF()", "YEAR()", "BETWEEN" },
                OptionalPrerequisites = new List<string>()
            },

            // --- SECTION 6 ---
            new()
            {
                Id = 29,
                Section = "Sektion 6: Subqueries & Komplexes",
                SkipCode = SqlLevelCodes.CodesList[28],
                NextLevelCode = SqlLevelCodes.CodesList[29],
                Title = "Filmabend (Subquery mit IN)",
                Description = "Wir analysieren die Datenbank eines Streaming-Dienstes.\n\n" +
                              "**Hinweis zur Namenskonvention:** Ab dieser Sektion passen wir die Benennung der Primär- und Fremdschlüssel an die realen Abiturprüfungen an. Primärschlüssel heißen nicht mehr pauschal 'id', sondern tragen ein Kürzel der Entität (z.B. 'nid' für Nutzer, 'fid' für Film). Fremdschlüssel haben kein '_FK'-Suffix mehr, sondern heißen exakt so wie der Primärschlüssel der referenzierten Tabelle.\n\n" +
                              "Formulieren Sie eine SQL-Anweisung, welche die Namen aller Nutzer ausgibt, die in ihrem Verlauf mindestens einen Film angeschaut haben, der dem Genre 'Action' zugeordnet ist.\n\n" +
                              "**Wichtig:** Verwenden Sie für die Lösung zwingend eine Unterabfrage mit dem [IN]-Operator, anstatt einen herkömmlichen [JOIN] über alle Tabellen zu bilden.",
                SetupScript = "CREATE TABLE Nutzer (nid INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Film (fid INTEGER PRIMARY KEY, titel TEXT, genre TEXT);" +
                              "CREATE TABLE schaut (nid INTEGER, fid INTEGER, datum TEXT);" +
                              "INSERT INTO Nutzer VALUES (1, 'Alice');" +
                              "INSERT INTO Nutzer VALUES (2, 'Bob');" +
                              "INSERT INTO Nutzer VALUES (3, 'Charlie');" +
                              "INSERT INTO Film VALUES (10, 'Die Hard', 'Action');" +
                              "INSERT INTO Film VALUES (11, 'Titanic', 'Drama');" +
                              "INSERT INTO Film VALUES (12, 'Mad Max', 'Action');" +
                              "INSERT INTO schaut VALUES (1, 10, '2023-01-01');" +
                              "INSERT INTO schaut VALUES (2, 11, '2023-02-01');" +
                              "INSERT INTO schaut VALUES (3, 12, '2023-03-01');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Alice" },
                    new[] { "Charlie" }
                },
                MaterialDocs = "start-hint: Subqueries (IN)\n" +
                               "Eine Unterabfrage wird innerhalb einer anderen Abfrage ausgeführt. Mit dem [IN]-Operator können Sie prüfen, ob ein Wert in der Ergebnismenge der Unterabfrage enthalten ist:\n" +
                               "{|SELECT ...\nWHERE fk IN\n    (SELECT ...);|}\n" +
                               ":end-hint\n" +
                               "start-hint: Joins vereinfachen (USING)\n" +
                               "Da Primär- und Fremdschlüssel nun exakt denselben Namen tragen (z.B. 'fid'), können Sie anstelle von [ON a.fid = b.fid] die kürzere [USING]-Syntax verwenden:\n" +
                               "{|JOIN Tabelle b USING (fid)|}\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec6\\lvl29-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Nutzer {\n    nid <<key>>\n    name\n}\nentity Film {\n    fid <<key>>\n    titel\n    genre\n}\nrelationship schaut {\n    datum\n}\nNutzer -(0,n)- schaut\nschaut -(0,m)- Film\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "Subquery im WHERE mit IN", "IN / NOT IN", "n:m Beziehungen" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 30,
                Section = "Sektion 6: Subqueries & Komplexes",
                SkipCode = SqlLevelCodes.CodesList[29],
                NextLevelCode = SqlLevelCodes.CodesList[30],
                Title = "Die Unberührten (Subquery mit NOT IN)",
                Description =
                    "Für eine Aufräumaktion auf den Servern sollen Filme identifiziert werden, die von der Nutzerschaft ignoriert werden.\n\n" +
                    "Entwickeln Sie einen SQL-Befehl, der die Titel aller Filme ermittelt, die noch nie von einem Nutzer angeschaut wurden (die also nicht im Verlauf auftauchen).\n" +
                    "Nutzen Sie hierfür das Ausschlussprinzip mithilfe einer Unterabfrage.",
                SetupScript = "CREATE TABLE Nutzer (nid INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Film (fid INTEGER PRIMARY KEY, titel TEXT, genre TEXT);" +
                              "CREATE TABLE schaut (nid INTEGER, fid INTEGER, datum TEXT);" +
                              "INSERT INTO Nutzer VALUES (1, 'Alice');" +
                              "INSERT INTO Film VALUES (10, 'Die Hard', 'Action');" +
                              "INSERT INTO Film VALUES (11, 'Titanic', 'Drama');" +
                              "INSERT INTO Film VALUES (12, 'Avatar', 'Sci-Fi');" +
                              "INSERT INTO schaut VALUES (1, 10, '2023-01-01');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "titel", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Titanic" },
                    new[] { "Avatar" }
                },
                MaterialDocs = "start-hint: Ausschlussprinzip (NOT IN)\n" +
                               "Analog zum [IN]-Operator prüft [NOT IN], ob ein Wert explizit **nicht** in der Menge der Unterabfrage vorkommt.\n" +
                               "Dies ist oft effizienter und lesbarer als komplexe [LEFT JOIN]s mit [IS NULL] Abfragen für denselben Zweck.\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec6\\lvl29-1.svg"
                },
                PlantUMLSources = new List<string>(), // shared
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "Subquery im WHERE mit NOT IN", "IN / NOT IN" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 31,
                Section = "Sektion 6: Subqueries & Komplexes",
                SkipCode = SqlLevelCodes.CodesList[30],
                NextLevelCode = SqlLevelCodes.CodesList[31],
                Title = "Intelligentes Einfügen (INSERT mit Subselect)",
                Description =
                    "Das System erhält eine Anfrage von der Frontend-Applikation. Ein neuer Eintrag soll in die Watchlist eines spezifischen Nutzers eingefügt werden. Die App übermittelt jedoch nur den Namen des Nutzers, nicht dessen ID.\n\n" +
                    "Entwickeln Sie die SQL-Anweisung, um einen neuen Datensatz in die Watchlist einzufügen.\n" +
                    "Der betroffene Nutzer trägt den Namen 'Neo'. Der Film, der zur Watchlist hinzugefügt werden soll, besitzt die ID 5.\n" +
                    "Ermitteln Sie die benötigte Nutzer-ID dynamisch über eine Unterabfrage innerhalb des Befehls.",
                SetupScript = "CREATE TABLE Nutzer (nid INTEGER PRIMARY KEY, name TEXT);" +
                              "CREATE TABLE Film (fid INTEGER PRIMARY KEY, titel TEXT);" +
                              "CREATE TABLE merkt_vor (nid INTEGER, fid INTEGER);" +
                              "INSERT INTO Nutzer VALUES (1, 'Trinity');" +
                              "INSERT INTO Nutzer VALUES (2, 'Neo');" +
                              "INSERT INTO Film VALUES (5, 'Matrix 4');",
                VerificationQuery =
                    "SELECT n.name, w.fid FROM merkt_vor w JOIN Nutzer n USING (nid) WHERE n.name = 'Neo'",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "fid", Type = "INT", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Neo", "5" }
                },
                MaterialDocs = "start-hint: INSERT mit dynamischen Werten\n" +
                               "Anstatt feste Werte (Literale) in die [VALUES]-Klammer zu schreiben, können Sie an der entsprechenden Position auch einen Subselect in runden Klammern einfügen, sofern dieser exakt einen Wert zurückliefert:\n" +
                               "{|INSERT INTO Tabelle (id, wert)\nVALUES ((SELECT ...), 10);|}\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec6\\lvl31-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Nutzer {\n    nid <<key>>\n    name\n}\nentity Film {\n    fid <<key>>\n    titel\n}\nrelationship merkt_vor {\n}\nNutzer -(0,n)- merkt_vor\nmerkt_vor -(0,m)- Film\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string> { "INSERT INTO ... VALUES", "Scalar Subquery (einzelner Wert)" },
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 32,
                Section = "Sektion 6: Subqueries & Komplexes",
                SkipCode = SqlLevelCodes.CodesList[31],
                NextLevelCode = SqlLevelCodes.CodesList[32],
                Title = "Die Abo-Analyse",
                Description =
                    "Das Management verlangt eine komplexe Datenanalyse zum aktuellen Nutzerverhalten bezüglich bestimmter Filmgenres.\n\n" +
                    "Implementieren Sie eine SQL-Anweisung für die Ermittlung der Namen der Nutzer und die Bezeichnung ihres jeweiligen Abonnements unter folgenden Bedingungen:\n" +
                    "- Der Nutzer hat im Jahr 2024 mindestens einen Film aus dem Genre 'Sci-Fi' in seinem Verlauf verzeichnet.\n" +
                    "- Der Nutzer hat sich den Film mit dem Titel 'Matrix' jedoch nicht auf seiner Watchlist gemerkt.\n\n" +
                    "Jeder Nutzer soll in der Ergebnisliste eindeutig aufgeführt werden. Leiten Sie das Relationenmodell selbstständig aus dem erweiterten ER-Diagramm ab.",
                SetupScript = "CREATE TABLE Abo (aboid INTEGER PRIMARY KEY, bezeichnung TEXT);" +
                              "CREATE TABLE Nutzer (nid INTEGER PRIMARY KEY, name TEXT, aboid INTEGER);" +
                              "CREATE TABLE Film (fid INTEGER PRIMARY KEY, titel TEXT, genre TEXT);" +
                              "CREATE TABLE schaut (nid INTEGER, fid INTEGER, datum TEXT);" +
                              "CREATE TABLE merkt_vor (nid INTEGER, fid INTEGER);" +
                              "INSERT INTO Abo VALUES (1, 'Basis');" +
                              "INSERT INTO Abo VALUES (2, 'Premium');" +
                              "INSERT INTO Nutzer VALUES (1, 'Anna', 2);" +
                              "INSERT INTO Nutzer VALUES (2, 'Ben', 1);" +
                              "INSERT INTO Nutzer VALUES (3, 'Clara', 2);" +
                              "INSERT INTO Nutzer VALUES (4, 'David', 1);" +
                              "INSERT INTO Film VALUES (10, 'Dune', 'Sci-Fi');" +
                              "INSERT INTO Film VALUES (11, 'Interstellar', 'Sci-Fi');" +
                              "INSERT INTO Film VALUES (12, 'Matrix', 'Sci-Fi');" +
                              "INSERT INTO Film VALUES (13, 'Joker', 'Drama');" +
                              "INSERT INTO schaut VALUES (1, 10, '2024-05-10');" +
                              "INSERT INTO schaut VALUES (2, 11, '2024-06-15');" +
                              "INSERT INTO merkt_vor VALUES (2, 12);" +
                              "INSERT INTO schaut VALUES (3, 10, '2023-10-01');" +
                              "INSERT INTO schaut VALUES (4, 13, '2024-01-05');",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "name", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "bezeichnung", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "Anna", "Premium" }
                },
                MaterialDocs = "start-tipp: Verschachtelung\n" +
                               "Nutzen Sie reguläre [JOIN]s (gerne mit [USING]), um die Bedingungen für das Genre und das Jahr (via [YEAR(datum)]) zu verknüpfen.\n" +
                               "Die zweite Bedingung lässt sich am elegantesten mit einem [NOT IN] im [WHERE]-Bereich lösen, der auf die Watchlist-Tabelle und den spezifischen Filmtitel abfragt.\n" +
                               ":end-hint",
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec6\\lvl32-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Abo {\n    aboid <<key>>\n    bezeichnung\n}\nentity Nutzer {\n    nid <<key>>\n    name\n}\nentity Film {\n    fid <<key>>\n    titel\n    genre\n}\nrelationship schliesst_ab {\n}\nrelationship schaut {\n    datum\n}\nrelationship merkt_vor {\n}\nAbo -(1,1)- schliesst_ab\nschliesst_ab -(0,n)- Nutzer\nNutzer -(0,n)- schaut\nschaut -(0,m)- Film\nNutzer -(0,n)- merkt_vor\nmerkt_vor -(0,m)- Film\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string>(), // none, so user cant reveal hints to themselves
                OptionalPrerequisites = new List<string>()
            },

            // --- SECTION 7 --- (abi levels)
            new()
            {
                Id = 33,
                Section = "Sektion 7: Die Abschlussprüfung",
                SkipCode = SqlLevelCodes.CodesList[32],
                NextLevelCode = SqlLevelCodes.CodesList[33],
                Title = "Die Ladenhüter der Medizin (Teil 1)",
                Difficulty = "Abitur",
                Description = "Zur Verwaltung der Logistik eines Krankenhauses wurde eine Datenbank entwickelt. " +
                              "In ihr werden Bestellungen von medizinischen Artikeln durch Mitarbeiterinnen und Mitarbeiter " +
                              "(im Folgenden Mitarbeiter genannt) erfasst sowie die zugehörigen Lagerstandorte und Artikelbestände verwaltet. " +
                              "Ein Entity-Relationship-Modell (ERM) der Datenbank ist im Material dargestellt.\n\n" +
                              "2.1 Die Lagerverantwortlichen vermuten, dass einige Artikel im Sortiment dauerhaft ungenutzt bleiben. " +
                              "Entwickeln Sie eine SQL-Anweisung, die die Artikelnummern und Bezeichnungen aller Artikel ermittelt, " +
                              "die bisher in keiner Bestellung enthalten waren. " +
                              "Die Ausgabe ist alphabetisch nach der Bezeichnung zu sortieren.",
                SetupScript = "CREATE TABLE Mitarbeiter (mid INTEGER PRIMARY KEY, nachname TEXT, vorname TEXT);" +
                              "CREATE TABLE Lager (lid INTEGER PRIMARY KEY, standort TEXT);" +
                              "CREATE TABLE Artikel (artid INTEGER PRIMARY KEY, bezeichnung TEXT, preis REAL);" +
                              "CREATE TABLE Bestellung (bid INTEGER PRIMARY KEY, datum TEXT, mid INTEGER, lid INTEGER);" +
                              "CREATE TABLE beinhaltet (bid INTEGER, artid INTEGER, menge INTEGER, PRIMARY KEY (bid, artid));" +
                              "INSERT INTO Mitarbeiter VALUES (1, 'Müller', 'Hans'), (2, 'Schmidt', 'Anna'), (3, 'Meier', 'Lukas');" +
                              "INSERT INTO Lager VALUES (1, 'Hauptlager'), (2, 'Notaufnahme'), (3, 'Station A');" +
                              "INSERT INTO Artikel VALUES (101, 'Ibuprofen 400mg', 5.50), (102, 'Verbandszeug', 12.00), (103, 'Defibrillator', 12500.00), (104, 'Skalpell', 18.50), (105, 'Pflaster', 2.50), (106, 'MRT-Gerät', 850000.00), (107, 'Stethoskop', 45.00);" +
                              "INSERT INTO Bestellung VALUES (1001, '2024-10-01', 1, 1);" +
                              "INSERT INTO beinhaltet VALUES (1001, 101, 50), (1001, 102, 20);" +
                              "INSERT INTO Bestellung VALUES (1002, '2024-10-05', 2, 2);" +
                              "INSERT INTO beinhaltet VALUES (1002, 103, 1), (1002, 104, 10);" +
                              "INSERT INTO Bestellung VALUES (1003, '2024-10-10', 1, 3);" +
                              "INSERT INTO beinhaltet VALUES (1003, 101, 10), (1003, 105, 100);" +
                              "INSERT INTO Bestellung VALUES (1004, '2024-10-15', 3, 1);" +
                              "INSERT INTO beinhaltet VALUES (1004, 106, 1), (1004, 103, 2);" +
                              "INSERT INTO Bestellung VALUES (1005, '2024-10-20', 2, 1);" +
                              "INSERT INTO beinhaltet VALUES (1005, 104, 5);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "artid", Type = "INT", StrictName = false },
                    new() { Name = "bezeichnung", Type = "VARCHAR(255)", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "107", "Stethoskop" }
                },
                MaterialDocs =
                    "Hinweise: Überführen Sie das Modell gedanklich in das Relationenmodell in der 3. Normalform. Alle Fremdschlüssel tragen exakt den gleichen Namen wie die Primärschlüssel der referenzierten Entitäten.\n\n" +
                    "Die referentielle Integrität soll bewahrt bleiben. Es gibt keine kaskadierende Löschung. Mit Variablen kann ein Wert für weitere Anweisungen genutzt werden.\n" +
                    "{|SET @variablenName = (SELECT ... FROM ... LIMIT 1);|}",
                IsRelationalModelSectionShared = true,
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec7\\lvl33-1.svg"
                },
                PlantUMLSources = new List<string>
                {
                    "@startchen\nentity Mitarbeiter {\n    mid <<key>>\n    name\n}\nentity Bestellung {\n    bid <<key>>\n    datum\n}\nentity Lager {\n    lid <<key>>\n    standort\n}\nentity Artikel {\n    artid <<key>>\n    bezeichnung\n    preis\n}\nrelationship gibt_auf {\n}\nrelationship geliefert_an {\n}\nrelationship beinhaltet {\n    menge\n}\nMitarbeiter -(1,1)- gibt_auf\ngibt_auf -(0,n)- Bestellung\nLager -(1,1)- geliefert_an\ngeliefert_an -(0,n)- Bestellung\nBestellung -(0,n)- beinhaltet\nbeinhaltet -(0,m)- Artikel\n@endchen"
                },
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string>(),
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 34,
                Section = "Sektion 7: Die Abschlussprüfung",
                SkipCode = SqlLevelCodes.CodesList[33],
                NextLevelCode = SqlLevelCodes.CodesList[34],
                Title = "Die Kostenkontrolle (Teil 2)",
                Difficulty = "Abitur",
                Description =
                    "2.2 Das Controlling des Krankenhauses führt eine Kostenanalyse der getätigten Bestellungen durch. " +
                    "Entwickeln Sie einen SQL-Befehl, der für jede Bestellung die Bestellnummer sowie das Datum ausgibt und " +
                    "zusätzlich den berechneten Gesamtwert der Bestellung als Gesamtwert ermittelt. " +
                    "Es sollen ausschließlich Bestellungen berücksichtigt werden, deren Gesamtwert 10.000 Euro übersteigt. " +
                    "Die Ausgabe ist absteigend nach dem Gesamtwert zu sortieren.",
                SetupScript = "CREATE TABLE Mitarbeiter (mid INTEGER PRIMARY KEY, nachname TEXT, vorname TEXT);" +
                              "CREATE TABLE Lager (lid INTEGER PRIMARY KEY, standort TEXT);" +
                              "CREATE TABLE Artikel (artid INTEGER PRIMARY KEY, bezeichnung TEXT, preis REAL);" +
                              "CREATE TABLE Bestellung (bid INTEGER PRIMARY KEY, datum TEXT, mid INTEGER, lid INTEGER);" +
                              "CREATE TABLE beinhaltet (bid INTEGER, artid INTEGER, menge INTEGER, PRIMARY KEY (bid, artid));" +
                              "INSERT INTO Mitarbeiter VALUES (1, 'Müller', 'Hans'), (2, 'Schmidt', 'Anna'), (3, 'Meier', 'Lukas');" +
                              "INSERT INTO Lager VALUES (1, 'Hauptlager'), (2, 'Notaufnahme'), (3, 'Station A');" +
                              "INSERT INTO Artikel VALUES (101, 'Ibuprofen 400mg', 5.50), (102, 'Verbandszeug', 12.00), (103, 'Defibrillator', 12500.00), (104, 'Skalpell', 18.50), (105, 'Pflaster', 2.50), (106, 'MRT-Gerät', 850000.00), (107, 'Stethoskop', 45.00);" +
                              "INSERT INTO Bestellung VALUES (1001, '2024-10-01', 1, 1);" +
                              "INSERT INTO beinhaltet VALUES (1001, 101, 50), (1001, 102, 20);" +
                              "INSERT INTO Bestellung VALUES (1002, '2024-10-05', 2, 2);" +
                              "INSERT INTO beinhaltet VALUES (1002, 103, 1), (1002, 104, 10);" +
                              "INSERT INTO Bestellung VALUES (1003, '2024-10-10', 1, 3);" +
                              "INSERT INTO beinhaltet VALUES (1003, 101, 10), (1003, 105, 100);" +
                              "INSERT INTO Bestellung VALUES (1004, '2024-10-15', 3, 1);" +
                              "INSERT INTO beinhaltet VALUES (1004, 106, 1), (1004, 103, 2);" +
                              "INSERT INTO Bestellung VALUES (1005, '2024-10-20', 2, 1);" +
                              "INSERT INTO beinhaltet VALUES (1005, 104, 5);",
                VerificationQuery = "",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "bid", Type = "INT", StrictName = false },
                    new() { Name = "datum", Type = "VARCHAR(255)", StrictName = false },
                    new() { Name = "Gesamtwert", Type = "DOUBLE", StrictName = true }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { "1004", "2024-10-15", "875000" },
                    new[] { "1002", "2024-10-05", "12685" }
                },
                MaterialDocs =
                    "Hinweise: Überführen Sie das Modell gedanklich in das Relationenmodell in der 3. Normalform. Alle Fremdschlüssel tragen exakt den gleichen Namen wie die Primärschlüssel der referenzierten Entitäten.\n\n" +
                    "Die referentielle Integrität soll bewahrt bleiben. Es gibt keine kaskadierende Löschung. Mit Variablen kann ein Wert für weitere Anweisungen genutzt werden.\n" +
                    "{|SET @variablenName = (SELECT ... FROM ... LIMIT 1);|}",
                IsRelationalModelSectionShared = true,
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec7\\lvl33-1.svg"
                },
                PlantUMLSources = new List<string>(),
                AuxiliaryIds = new List<string>(),
                Prerequisites = new List<string>(),
                OptionalPrerequisites = new List<string>()
            },
            new()
            {
                Id = 35,
                Section = "Sektion 7: Die Abschlussprüfung",
                SkipCode = SqlLevelCodes.CodesList[34],
                NextLevelCode = SqlLevelCodes.CodesList[35],
                Title = "Stornierung (Teil 3)",
                Difficulty = "Abitur",
                Description =
                    "2.3 Aufgrund eines Systemfehlers wurde die zuletzt erfasste Bestellung der Mitarbeiterin Schmidt " +
                    "für den Standort Notaufnahme irrtümlich in die Datenbank eingetragen und muss vollständig entfernt werden.\n\n" +
                    "Entwickeln Sie die SQL-Anweisungen, um diese Bestellung unter Bewahrung der referentiellen Integrität " +
                    "aus allen betroffenen Tabellen zu löschen.",
                SetupScript = "CREATE TABLE Mitarbeiter (mid INTEGER PRIMARY KEY, nachname TEXT, vorname TEXT);" +
                              "CREATE TABLE Lager (lid INTEGER PRIMARY KEY, standort TEXT);" +
                              "CREATE TABLE Artikel (artid INTEGER PRIMARY KEY, bezeichnung TEXT, preis REAL);" +
                              "CREATE TABLE Bestellung (bid INTEGER PRIMARY KEY, datum TEXT, mid INTEGER, lid INTEGER);" +
                              "CREATE TABLE beinhaltet (bid INTEGER, artid INTEGER, menge INTEGER, PRIMARY KEY (bid, artid));" +
                              "INSERT INTO Mitarbeiter VALUES (1, 'Müller', 'Hans'), (2, 'Schmidt', 'Anna'), (3, 'Meier', 'Lukas');" +
                              "INSERT INTO Lager VALUES (1, 'Hauptlager'), (2, 'Notaufnahme'), (3, 'Station A');" +
                              "INSERT INTO Artikel VALUES (101, 'Ibuprofen 400mg', 5.50), (102, 'Verbandszeug', 12.00), (103, 'Defibrillator', 12500.00), (104, 'Skalpell', 18.50);" +
                              $"INSERT INTO Bestellung VALUES ({b1}, '2024-10-01', 1, 1);" +
                              $"INSERT INTO beinhaltet VALUES ({b1}, 101, 50);" +
                              $"INSERT INTO Bestellung VALUES ({b2}, '2024-10-05', 2, 1);" +
                              $"INSERT INTO beinhaltet VALUES ({b2}, 102, 20);" +
                              $"INSERT INTO Bestellung VALUES ({b3}, '2024-10-10', 3, 2);" +
                              $"INSERT INTO beinhaltet VALUES ({b3}, 103, 1);" +
                              $"INSERT INTO Bestellung VALUES ({b4}, '2024-10-15', 2, 2);" +
                              $"INSERT INTO beinhaltet VALUES ({b4}, 104, 10);" +
                              $"INSERT INTO Bestellung VALUES ({bTarget}, '2024-10-20', 2, 2);" + // schmidt, notaufnahme, neuestes datum (goal)
                              $"INSERT INTO beinhaltet VALUES ({bTarget}, 101, 5);" +
                              $"INSERT INTO Bestellung VALUES ({b6}, '2024-10-25', 1, 3);" +
                              $"INSERT INTO beinhaltet VALUES ({b6}, 102, 15);",
                VerificationQuery =
                    "SELECT bid, artid FROM Bestellung JOIN beinhaltet USING (bid) ORDER BY bid ASC, artid ASC",
                ExpectedSchema = new List<SqlExpectedColumn>
                {
                    new() { Name = "bid", Type = "INT", StrictName = false },
                    new() { Name = "artid", Type = "INT", StrictName = false }
                },
                ExpectedResult = new List<string[]>
                {
                    new[] { b1.ToString(), "101" },
                    new[] { b2.ToString(), "102" },
                    new[] { b3.ToString(), "103" },
                    new[] { b4.ToString(), "104" },
                    new[] { b6.ToString(), "102" }
                },
                MaterialDocs =
                    "Hinweise: Überführen Sie das Modell gedanklich in das Relationenmodell in der 3. Normalform. Alle Fremdschlüssel tragen exakt den gleichen Namen wie die Primärschlüssel der referenzierten Entitäten.\n\n" +
                    "Die referentielle Integrität soll bewahrt bleiben. Es gibt keine kaskadierende Löschung. Mit Variablen kann ein Wert für weitere Anweisungen genutzt werden.\n" +
                    "{|SET @variablenName = (SELECT ... FROM ... LIMIT 1);|}",
                IsRelationalModelSectionShared = true,
                DiagramPaths = new List<string>
                {
                    "imgsql\\sec7\\lvl33-1.svg"
                },
                PlantUMLSources = new List<string>(),
                AuxiliaryIds = new List<string> { bTarget.ToString() },
                Prerequisites = new List<string>(),
                OptionalPrerequisites = new List<string>()
            }
        };
    }
}