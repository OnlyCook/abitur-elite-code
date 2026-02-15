using System.Collections.Generic;

namespace AbiturEliteCode.cs
{
    // SQL LEVEL CRATION GUIDE
    // the app uses SQLite but we are trying our best to emulate MySQL syntax and behavior as that is what we are supposed to learn and is expected from us in the abitur

    // regarding "PlantUMLSources" we are using plantuml and only ER-diagrams (Chen's notation)

    // the entity-relationship diagrams should match the ones in the abitur exams, here is how they should be structured:
    // multiplicities are written in the min-max-notation like this for each side: [min,max] (for example: ET1 -(0,n)- REL -(0,m)- ET2; not actually valid just for visualization)
    // primary keys are underlined (<<key>>)
    // attributes are written in camelCase (for example: "anzahlGetränke"), ids have their "id" in uppercase and without underscores (for example: "kID")
    // in the early levels (with ER-diagrams) we include the foreign keys in the diagrams, but in the later levels we do not (the user must know on their own what has what key)

    public class SqlLevel
    {
        public int Id { get; set; }
        public string Section { get; set; }
        public string SkipCode { get; set; }
        public string NextLevelCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string SetupScript { get; set; }
        public string VerificationQuery { get; set; }
        public List<string[]> ExpectedResult { get; set; }
        public string MaterialDocs { get; set; }

        public List<string> DiagramPaths { get; set; } = new List<string>(); // max of 3
        public List<string> PlantUMLSources { get; set; } = new List<string>(); // max of 3
        public List<string> AuxiliaryIds { get; set; } = new List<string>();
    }

    public static class SqlLevelCodes
    {
        public static string[] CodesList = {
            "SEL", "WHE", "ORD", "GRP", "INS", "UPD", "DEL", "EXM",
            "JON", "IMP", "JOI", "JO3", "JOX"
        };
    }

    public static class SqlSharedDiagrams
    {
        // placeholder
    }

    public static class SqlAuxiliaryImplementations
    {
        public static string GetCode(string auxId) => ""; // placeholder
    }

    public static class SqlCurriculum
    {
        public static List<SqlLevel> GetLevels()
        {
            return new List<SqlLevel>
            {
                // --- SECTION 1 ---
                new SqlLevel
                {
                    Id = 1,
                    Section = "Sektion 1: SQL Grundlagen",
                    SkipCode = SqlLevelCodes.CodesList[0],
                    NextLevelCode = SqlLevelCodes.CodesList[1],
                    Title = "Projektion (SELECT)",
                    Description = "In der Datenbank der Schulbibliothek existiert eine Tabelle [Buch].\n\n" +
                                  "Die Tabelle [Buch] besitzt folgende Spalten:\n" +
                                  "- [Titel] (Text)\n" +
                                  "- [Autor] (Text)\n" +
                                  "- [Preis] (Dezimalzahl)\n" +
                                  "- [ISBN] (Text)\n\n" +
                                  "Aufgabe:\n" +
                                  "Wählen Sie nur den [Titel] und den [Preis] aller Bücher aus.",
                    SetupScript = "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT, Autor TEXT, Preis REAL, ISBN TEXT);" +
                                  "INSERT INTO Buch (Titel, Autor, Preis, ISBN) VALUES ('Faust', 'Goethe', 9.99, '978-3');" +
                                  "INSERT INTO Buch (Titel, Autor, Preis, ISBN) VALUES ('Die Verwandlung', 'Kafka', 5.50, '978-4');" +
                                  "INSERT INTO Buch (Titel, Autor, Preis, ISBN) VALUES ('Der Prozess', 'Kafka', 8.90, '978-5');",
                    VerificationQuery = "",
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
                    DiagramPaths = new List<string>(),
                    AuxiliaryIds = new List<string>()
                },
                new SqlLevel
                {
                    Id = 2,
                    Section = "Sektion 1: SQL Grundlagen",
                    SkipCode = SqlLevelCodes.CodesList[1],
                    NextLevelCode = SqlLevelCodes.CodesList[2],
                    Title = "Selektion (WHERE)",
                    Description = "Die Bibliotheksleitung sucht nach günstigen Büchern für den Ausverkauf.\n\n" +
                                  "Aufgabe:\n" +
                                  "Ermitteln Sie alle Spalten ([*]) aller Bücher der Tabelle [Buch], deren Preis **kleiner als 9.00** Euro ist.",
                    SetupScript = "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT, Autor TEXT, Preis REAL, ISBN TEXT);" +
                                  "INSERT INTO Buch (Titel, Autor, Preis, ISBN) VALUES ('Faust', 'Goethe', 9.99, '978-3');" +
                                  "INSERT INTO Buch (Titel, Autor, Preis, ISBN) VALUES ('Die Verwandlung', 'Kafka', 5.50, '978-4');" +
                                  "INSERT INTO Buch (Titel, Autor, Preis, ISBN) VALUES ('Der Prozess', 'Kafka', 8.90, '978-5');",
                    VerificationQuery = "",
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
                    DiagramPaths = new List<string>(),
                    AuxiliaryIds = new List<string>()
                },
                new SqlLevel
                {
                    Id = 3,
                    Section = "Sektion 1: SQL Grundlagen",
                    SkipCode = SqlLevelCodes.CodesList[2],
                    NextLevelCode = SqlLevelCodes.CodesList[3],
                    Title = "Sortierung (ORDER BY)",
                    Description = "Wir suchen alle Bücher eines bestimmten Autors, sortiert nach dem Titel.\n\n" +
                                  "Aufgabe:\n" +
                                  "Wählen Sie [Titel] und [Erscheinungsjahr] aller Bücher von 'Kafka' aus der Tabelle [Buch].\n" +
                                  "Sortieren Sie das Ergebnis **aufsteigend** nach dem Titel.",
                    SetupScript = "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT, Autor TEXT, Erscheinungsjahr INTEGER);" +
                                  "INSERT INTO Buch (Titel, Autor, Erscheinungsjahr) VALUES ('Das Schloss', 'Kafka', 1926);" +
                                  "INSERT INTO Buch (Titel, Autor, Erscheinungsjahr) VALUES ('Faust', 'Goethe', 1808);" +
                                  "INSERT INTO Buch (Titel, Autor, Erscheinungsjahr) VALUES ('Die Verwandlung', 'Kafka', 1915);",
                    VerificationQuery = "",
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
                    DiagramPaths = new List<string>(),
                    AuxiliaryIds = new List<string>()
                },
                new SqlLevel
                {
                    Id = 4,
                    Section = "Sektion 1: SQL Grundlagen",
                    SkipCode = SqlLevelCodes.CodesList[3],
                    NextLevelCode = SqlLevelCodes.CodesList[4],
                    Title = "Gruppierung (GROUP BY)",
                    Description = "Für eine statistische Auswertung sollen Bücher nach ihrem Genre zusammengefasst werden.\n\n" +
                                  "- [Buch] (Titel, Autor, Genre, Preis)\n\n" +
                                  "Aufgabe:\n" +
                                  "Ermitteln Sie für jedes [Genre] den **Durchschnittspreis** (als 'Durchschnitt').\n" +
                                  "Das Ergebnis soll das Genre und den berechneten Durchschnittspreis enthalten.",
                    SetupScript = "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT, Autor TEXT, Genre TEXT, Preis REAL);" +
                                  "INSERT INTO Buch (Titel, Autor, Genre, Preis) VALUES ('Faust', 'Goethe', 'Drama', 10.0);" +
                                  "INSERT INTO Buch (Titel, Autor, Genre, Preis) VALUES ('Iphigenie', 'Goethe', 'Drama', 12.0);" +
                                  "INSERT INTO Buch (Titel, Autor, Genre, Preis) VALUES ('Es', 'King', 'Horror', 15.0);" +
                                  "INSERT INTO Buch (Titel, Autor, Genre, Preis) VALUES ('Shining', 'King', 'Horror', 13.0);" +
                                  "INSERT INTO Buch (Titel, Autor, Genre, Preis) VALUES ('Der Marsianer', 'Weir', 'SciFi', 20.0);",
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "Drama", "11" }, // (10+12)/2 = 11
                        new[] { "Horror", "14" }, // (15+13)/2 = 14
                        new[] { "SciFi", "20" }
                    },
                    MaterialDocs = "start-hint: GROUP BY Syntax\n" +
                                   "Wenn Sie Aggregatfunktionen wie [AVG()], [COUNT()] oder [SUM()] nutzen und gleichzeitig eine normale Spalte (hier Genre) ausgeben wollen, müssen Sie nach dieser Spalte gruppieren:\n" +
                                   "{|SELECT Spalte1, AVG(Spalte2)\nFROM Tabelle\nGROUP BY Spalte1;|}\n" +
                                   ":end-hint\n" +
                                   "start-hint: Spaltenbenennung\n" +
                                   "Um Spalten einen eigenen Namen zu geben, nutzen sie das [AS]-Keyword:\n" +
                                   "{|SELECT AVG(Spalte) AS EigenerName\nFROM Tabelle;|}" +
                                   "Dies wird häufig zusammen mit Aggregatfunktionen genutzt.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>(),
                    AuxiliaryIds = new List<string>()
                },
                new SqlLevel
                {
                    Id = 5,
                    Section = "Sektion 1: SQL Grundlagen",
                    SkipCode = SqlLevelCodes.CodesList[4],
                    NextLevelCode = SqlLevelCodes.CodesList[5],
                    Title = "Daten Einfügen (INSERT)",
                    Description = "Ein neuer Schüler hat sich angemeldet.\n\n" +
                                  "- [Schueler] (ID (Int), Name (Text), Klasse (Int))\n\n" +
                                  "Aufgabe:\n" +
                                  "Fügen Sie den Schüler 'Leon' mit der ID 10 in die Klasse 12 ein.",
                    SetupScript = "CREATE TABLE Schueler (ID INTEGER PRIMARY KEY, Name TEXT, Klasse INTEGER);" +
                                  "INSERT INTO Schueler VALUES (1, 'Max', 11);",
                    VerificationQuery = "SELECT * FROM Schueler WHERE ID = 10",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "10", "Leon", "12" }
                    },
                    MaterialDocs = "start-hint: INSERT Syntax\n" +
                                   "Variante 1 (Alle Spalten):\n" +
                                   "{|INSERT INTO Tabelle VALUES (Wert1, Wert2, ...);|}\n" +
                                   "Variante 2 (Spezifische Spalten):\n" +
                                   "{|INSERT INTO Tabelle (SpalteA, SpalteB) VALUES (WertA, WertB);|}\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>(),
                    AuxiliaryIds = new List<string>()
                },
                new SqlLevel
                {
                    Id = 6,
                    Section = "Sektion 1: SQL Grundlagen",
                    SkipCode = SqlLevelCodes.CodesList[5],
                    NextLevelCode = SqlLevelCodes.CodesList[6],
                    Title = "Daten Ändern (UPDATE)",
                    Description = "Der Schüler 'Max' (ID 1) ist in die Klasse 13 versetzt worden.\n\n" +
                                  "Aufgabe:\n" +
                                  "Aktualisieren Sie den Eintrag von Max in der Tabelle [Schueler], sodass seine Klasse nun 13 ist.\n" +
                                  "Achten Sie unbedingt auf die [WHERE]-Klausel!",
                    SetupScript = "CREATE TABLE Schueler (ID INTEGER PRIMARY KEY, Name TEXT, Klasse INTEGER);" +
                                  "INSERT INTO Schueler VALUES (1, 'Max', 12); " +
                                  "INSERT INTO Schueler VALUES (2, 'Lisa', 11);",
                    VerificationQuery = "SELECT * FROM Schueler WHERE ID = 1",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "1", "Max", "13" }
                    },
                    MaterialDocs = "start-hint: UPDATE Syntax\n" +
                                   "{|UPDATE Tabelle SET Spalte = NeuerWert WHERE Bedingung;|}\n" +
                                   "Ohne [WHERE] würden **alle** Schüler in Klasse 13 versetzt werden.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>(),
                    AuxiliaryIds = new List<string>()
                },
                new SqlLevel
                {
                    Id = 7,
                    Section = "Sektion 1: SQL Grundlagen",
                    SkipCode = SqlLevelCodes.CodesList[6],
                    NextLevelCode = SqlLevelCodes.CodesList[7],
                    Title = "Daten Löschen (DELETE)",
                    Description = "Alle Schüler der Klasse 13 haben die Schule verlassen (Abitur bestanden).\n\n" +
                                  "Aufgabe:\n" +
                                  "Löschen Sie alle Einträge aus der Tabelle [Schueler], bei denen die Klasse 13 ist.",
                    SetupScript = "CREATE TABLE Schueler (ID INTEGER PRIMARY KEY, Name TEXT, Klasse INTEGER);" +
                                  "INSERT INTO Schueler VALUES (1, 'AbiAbsolvent', 13); " +
                                  "INSERT INTO Schueler VALUES (2, 'BleibtHier', 12); " +
                                  "INSERT INTO Schueler VALUES (3, 'AuchWeg', 13);",
                    VerificationQuery = "SELECT * FROM Schueler",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "2", "BleibtHier", "12" }
                    },
                    MaterialDocs = "start-hint: DELETE Syntax\n" +
                                   "{|DELETE FROM Tabelle WHERE Bedingung;|}\n" +
                                   "Vorsicht: [DELETE FROM Tabelle] (ohne Where) löscht den gesamten Inhalt!\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>(),
                    AuxiliaryIds = new List<string>()
                },
                new SqlLevel
                {
                    Id = 8,
                    Section = "Sektion 1: SQL Grundlagen",
                    SkipCode = SqlLevelCodes.CodesList[7],
                    NextLevelCode = SqlLevelCodes.CodesList[8],
                    Title = "Klausurphase",
                    Description = "Dies ist eine komplexe Abfrage zum Abschluss der Grundlagen.\n\n" +
                                  "Gegeben ist die Tabelle [Klausur] mit:\n" +
                                  "- [Schueler] (Text)\n" +
                                  "- [Fach] (Text)\n" +
                                  "- [Notenpunkte] (Integer, 0-15)\n\n" +
                                  "Aufgabe:\n" +
                                  "Ermitteln Sie [Schueler] und [Notenpunkte] aller Klausuren im Fach 'Informatik', die **schlechter als 5 Punkte** (also < 5) sind.\n" +
                                  "Sortieren Sie das Ergebnis absteigend nach den Notenpunkten.",
                    SetupScript = "CREATE TABLE Klausur (ID INTEGER PRIMARY KEY, Schueler TEXT, Fach TEXT, Notenpunkte INTEGER);" +
                                  "INSERT INTO Klausur (Schueler, Fach, Notenpunkte) VALUES ('Max', 'Mathe', 12);" +
                                  "INSERT INTO Klausur (Schueler, Fach, Notenpunkte) VALUES ('Lisa', 'Informatik', 14);" +
                                  "INSERT INTO Klausur (Schueler, Fach, Notenpunkte) VALUES ('Tom', 'Informatik', 3);" +
                                  "INSERT INTO Klausur (Schueler, Fach, Notenpunkte) VALUES ('Sarah', 'Informatik', 4);" +
                                  "INSERT INTO Klausur (Schueler, Fach, Notenpunkte) VALUES ('Jan', 'Deutsch', 2);",
                    VerificationQuery = "",
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
                    DiagramPaths = new List<string>(),
                    AuxiliaryIds = new List<string>()
                },

                // --- SECTION 2 ---
                new SqlLevel
                {
                    Id = 9,
                    Section = "Sektion 2: Die Bibliothek",
                    SkipCode = SqlLevelCodes.CodesList[8],
                    NextLevelCode = SqlLevelCodes.CodesList[9],
                    Title = "Der Schlüssel zum Erfolg (PK & FK)",
                    Description = "Wir befinden uns in der Datenbank einer Schulbibliothek. Um Redundanzen zu vermeiden, wurden Bücher und Autoren in zwei getrennte Tabellen aufgeteilt (Normalisierung).\n\n" +
                                  "Das Buch 'Faust' speichert nicht mehr den Namen 'Goethe', sondern referenziert diesen über eine ID (Fremdschlüssel).\n\n" +
                                  "**Schema:**\n" +
                                  "- [Autor] (--ID--, Vorname, Nachname)\n" +
                                  "- [Buch] (--ID--, Titel, AutorID_FK)\n\n" +
                                  "**Aufgabe:**\n" +
                                  "Ermitteln Sie die [ID] des Autors mit dem Nachnamen 'Goethe' aus der Tabelle [Autor], um zu verstehen, welche Zahl in der Buch-Tabelle verwendet wird.",
                    SetupScript = "CREATE TABLE Autor (ID INTEGER PRIMARY KEY, Vorname TEXT, Nachname TEXT);" +
                                  "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT, AutorID_FK INTEGER);" +
                                  "INSERT INTO Autor VALUES (101, 'Johann Wolfgang von', 'Goethe');" +
                                  "INSERT INTO Autor VALUES (102, 'Friedrich', 'Schiller');" +
                                  "INSERT INTO Buch VALUES (1, 'Faust', 101);" +
                                  "INSERT INTO Buch VALUES (2, 'Die Räuber', 102);",
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "101" }
                    },
                    MaterialDocs = "start-hint: Primärschlüssel\n" +
                                   "Jeder Datensatz in der Tabelle [Autor] ist durch die [ID] eindeutig identifizierbar.\n" +
                                   "In der Tabelle [Buch] wird diese ID genutzt, um auf den Autor zu verweisen.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "imgsql\\sec2\\lvl9-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startchen\nentity Autor {\n    ID <<key>>\n    Vorname\n    Nachname\n}\nentity Buch {\n    ID <<key>>\n    Titel\n    AutorID_FK\n}\nrelationship verfasst {\n}\nAutor -(0,n)- verfasst\nverfasst -(1,1)- Buch\n@endchen"
                    }
                },
                new SqlLevel
                {
                    Id = 10,
                    Section = "Sektion 2: Die Bibliothek",
                    SkipCode = SqlLevelCodes.CodesList[9],
                    NextLevelCode = SqlLevelCodes.CodesList[10],
                    Title = "Die erste Verbindung (Implicit Join)",
                    Description = "Nun sollen die Daten aus beiden Tabellen zusammengeführt werden. Wir nutzen dazu zunächst die klassische Schreibweise (impliziter Join) über die [WHERE]-Klausel.\n\n" +
                                  "**Schema:**\n" +
                                  "- [Autor] (--ID--, Vorname, Nachname)\n" +
                                  "- [Buch] (--ID--, Titel, AutorID_FK)\n\n" +
                                  "**Aufgabe:**\n" +
                                  "Geben Sie eine Liste aller [Titel] und der zugehörigen [Nachname]n der Autoren aus.\n" +
                                  "Nutzen Sie die Syntax: [FROM Buch, Autor] und verknüpfen Sie die Tabellen im [WHERE]-Teil, indem Sie den Fremdschlüssel ([Buch.AutorID_FK]) mit dem Primärschlüssel ([Autor.ID]) gleichsetzen.",
                    SetupScript = "CREATE TABLE Autor (ID INTEGER PRIMARY KEY, Vorname TEXT, Nachname TEXT);" +
                                  "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT, AutorID_FK INTEGER);" +
                                  "INSERT INTO Autor VALUES (1, 'Johann', 'Goethe');" +
                                  "INSERT INTO Autor VALUES (2, 'Franz', 'Kafka');" +
                                  "INSERT INTO Buch VALUES (10, 'Faust', 1);" +
                                  "INSERT INTO Buch VALUES (11, 'Die Verwandlung', 2);" +
                                  "INSERT INTO Buch VALUES (12, 'Der Prozess', 2);",
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "Faust", "Goethe" },
                        new[] { "Die Verwandlung", "Kafka" },
                        new[] { "Der Prozess", "Kafka" }
                    },
                    MaterialDocs = "start-hint: Kartesisches Produkt\n" +
                                   "Wenn Sie nur [FROM Buch, Autor] schreiben, wird jedes Buch mit jedem Autor kombiniert.\n" +
                                   "Erst durch [WHERE Buch.AutorID_FK = Autor.ID] filtern Sie die korrekten Paare heraus.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "imgsql\\sec2\\lvl10-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startchen\nentity Autor {\n    ID <<key>>\n    Nachname\n}\nentity Buch {\n    ID <<key>>\n    Titel\n    AutorID_FK\n}\nrelationship verfasst {\n}\nAutor -(0,n)- verfasst\nverfasst -(1,1)- Buch\n@endchen"
                    }
                },
                new SqlLevel
                {
                    Id = 11,
                    Section = "Sektion 2: Die Bibliothek",
                    SkipCode = SqlLevelCodes.CodesList[10],
                    NextLevelCode = SqlLevelCodes.CodesList[11],
                    Title = "Modernes Verbinden (INNER JOIN)",
                    Description = "Der SQL-Standard sieht für Verknüpfungen den [JOIN]-Operator vor. Dieser trennt die Verknüpfungslogik sauber von der Filterlogik.\n\n" +
                                  "**Aufgabe:**\n" +
                                  "Erstellen Sie exakt dieselbe Liste wie im vorherigen Level ([Titel], [Nachname]), nutzen Sie diesmal jedoch den **INNER JOIN**.",
                    SetupScript = "CREATE TABLE Autor (ID INTEGER PRIMARY KEY, Vorname TEXT, Nachname TEXT);" +
                                  "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT, AutorID_FK INTEGER);" +
                                  "INSERT INTO Autor VALUES (1, 'Johann', 'Goethe');" +
                                  "INSERT INTO Autor VALUES (2, 'Franz', 'Kafka');" +
                                  "INSERT INTO Buch VALUES (10, 'Faust', 1);" +
                                  "INSERT INTO Buch VALUES (11, 'Die Verwandlung', 2);" +
                                  "INSERT INTO Buch VALUES (12, 'Der Prozess', 2);",
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "Faust", "Goethe" },
                        new[] { "Die Verwandlung", "Kafka" },
                        new[] { "Der Prozess", "Kafka" }
                    },
                    MaterialDocs = "start-hint: JOIN Syntax\n" +
                                   "{|SELECT ...\nFROM TabelleA\nJOIN TabelleB ON TabelleA.FK = TabelleB.PK;|}\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "imgsql\\sec2\\lvl11-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startchen\nentity Autor {\n    ID <<key>>\n    Nachname\n}\nentity Buch {\n    ID <<key>>\n    Titel\n    AutorID_FK\n}\nrelationship verfasst {\n}\nAutor -(0,n)- verfasst\nverfasst -(1,1)- Buch\n@endchen"
                    }
                },
                new SqlLevel
                {
                    Id = 12,
                    Section = "Sektion 2: Die Bibliothek",
                    SkipCode = SqlLevelCodes.CodesList[11],
                    NextLevelCode = SqlLevelCodes.CodesList[12],
                    Title = "Wer liest was? (3-Wege Join)",
                    Description = "Die Datenbank wurde erweitert. Schüler können nun Bücher ausleihen. Da ein Schüler viele Bücher leiht und ein Buch (über die Zeit) von vielen Schülern geliehen wird, existiert eine Relationstabelle.\n\n" +
                                  "**Schema:**\n" +
                                  "- [Schueler] (--ID--, Name, Klasse)\n" +
                                  "- [Buch] (--ID--, Titel)\n" +
                                  "- [Ausleihe] (--SchuelerID_FK--, --BuchID_FK--, Datum)\n\n" +
                                  "**Aufgabe:**\n" +
                                  "Ermitteln Sie, welcher Schüler welches Buch ausgeliehen hat.\n" +
                                  "Geben Sie den [Name]n des Schülers und den [Titel] des Buches aus.",
                    SetupScript = "CREATE TABLE Schueler (ID INTEGER PRIMARY KEY, Name TEXT, Klasse TEXT);" +
                                  "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT);" +
                                  "CREATE TABLE Ausleihe (SchuelerID_FK INTEGER, BuchID_FK INTEGER, Datum TEXT);" +
                                  "INSERT INTO Schueler VALUES (1, 'Max', '10b');" +
                                  "INSERT INTO Schueler VALUES (2, 'Lisa', '12a');" +
                                  "INSERT INTO Buch VALUES (100, 'Faust');" +
                                  "INSERT INTO Buch VALUES (101, 'Nathan der Weise');" +
                                  "INSERT INTO Ausleihe VALUES (1, 100, '2023-01-01');" +
                                  "INSERT INTO Ausleihe VALUES (2, 101, '2023-01-05');" +
                                  "INSERT INTO Ausleihe VALUES (2, 100, '2023-02-01');",
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "Max", "Faust" },
                        new[] { "Lisa", "Nathan der Weise" },
                        new[] { "Lisa", "Faust" }
                    },
                    MaterialDocs = "start-hint: Kette von Joins\n" +
                                   "Sie müssen von Tabelle A nach B und von B nach C springen:\n" +
                                   "{|FROM Schueler\n" +
                                   "JOIN Ausleihe ON ...\n" +
                                   "JOIN Buch ON ...|}\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "imgsql\\sec2\\lvl12-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startchen\nentity Schueler {\n    ID <<key>>\n    Name\n}\nentity Buch {\n    ID <<key>>\n    Titel\n}\nrelationship Ausleihe {\n    SchulerID_FK\n    BuchID_FK\n    Datum\n}\nSchueler -(0,n)- Ausleihe\nAusleihe -(0,m)- Buch\n@endchen"
                    }
                },
                new SqlLevel
                {
                    Id = 13,
                    Section = "Sektion 2: Die Bibliothek",
                    SkipCode = SqlLevelCodes.CodesList[12],
                    NextLevelCode = "",
                    Title = "Klasse 10b",
                    Description = "Der Direktor benötigt eine Übersicht über das Leseverhalten einer spezifischen Klasse.\n\n" +
                                  "**Aufgabe:**\n" +
                                  "Nutzen Sie das gegebene ER-Diagramm.\n" +
                                  "Geben Sie [Name] und [Titel] aller Ausleihen aus, aber **nur** für Schüler der Klasse '10b'.",
                    SetupScript = "CREATE TABLE Schueler (ID INTEGER PRIMARY KEY, Name TEXT, Klasse TEXT);" +
                                  "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT);" +
                                  "CREATE TABLE Ausleihe (SchuelerID_FK INTEGER, BuchID_FK INTEGER, Datum TEXT);" +
                                  "INSERT INTO Schueler VALUES (1, 'Max', '10b');" +
                                  "INSERT INTO Schueler VALUES (2, 'Lisa', '12a');" +
                                  "INSERT INTO Schueler VALUES (3, 'Tom', '10b');" +
                                  "INSERT INTO Buch VALUES (100, 'Faust');" +
                                  "INSERT INTO Buch VALUES (101, 'HTML für Anfänger');" +
                                  "INSERT INTO Ausleihe VALUES (1, 100, '2023-01-01');" +
                                  "INSERT INTO Ausleihe VALUES (2, 100, '2023-01-05');" +
                                  "INSERT INTO Ausleihe VALUES (3, 101, '2023-02-01');",
                    VerificationQuery = "",
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
                        "@startchen\nentity Schueler {\n    ID <<key>>\n    Name\n    Klasse\n}\nentity Buch {\n    ID <<key>>\n    Titel\n}\nrelationship Ausleihe {\n    SchulerID_FK\n    BuchID_FK\n    Datum\n}\nSchueler -(0,n)- Ausleihe\nAusleihe -(0,m)- Buch\n@endchen"
                    }
                }
            };
        }
    }
}