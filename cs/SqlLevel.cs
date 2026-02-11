using System.Collections.Generic;

namespace AbiturEliteCode.cs
{
    // SQL LEVEL CRATION GUIDE
    // the app uses SQLite but we are trying our best to emulate MySQL syntax and behavior as that is what we are supposed to learn and is expected from us in the abitur

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
            "SEL", "WHE", "ORD", "AGG", "INS", "UPD", "DEL", "EX1",
            "ER1", "ER2", "ER3", "ER4"
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
                                  "Die Tabelle besitzt folgende Spalten:\n" +
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
                    Title = "Aggregationen",
                    Description = "Die Statistik-Abteilung benötigt Kennzahlen zum Bestand.\n\n" +
                                  "Aufgabe:\n" +
                                  "Ermitteln Sie die Anzahl aller Bücher (als Spalte 'Anzahl') und den Durchschnittspreis aller Bücher (als Spalte 'Durchschnitt') der Tabelle [Buch].\n" +
                                  "Es werden keine [GROUP BY] Klauseln benötigt, da wir über die gesamte Tabelle aggregieren.",
                    SetupScript = "CREATE TABLE Buch (ID INTEGER PRIMARY KEY, Titel TEXT, Preis REAL);" +
                                  "INSERT INTO Buch (Titel, Preis) VALUES ('A', 10.0);" +
                                  "INSERT INTO Buch (Titel, Preis) VALUES ('B', 20.0);" +
                                  "INSERT INTO Buch (Titel, Preis) VALUES ('C', 15.0);",
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "3", "15" } // 15 books avg: 3.0
                    },
                    MaterialDocs = "start-hint: Aggregatfunktionen\n" +
                                   "- [COUNT(*)]: Zählt Zeilen\n" +
                                   "- [AVG(spalte)]: Berechnet Durchschnitt\n" +
                                   "- [SUM(spalte)]: Berechnet Summe\n" +
                                   "- [MAX(spalte)] / [MIN(spalte)]: Maximum/Minimum\n" +
                                   "Nutzen Sie [AS], um den Spalten Namen zu geben: [SELECT COUNT(*) AS Anzahl ...]\n" +
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
                                  "Tabelle [Schueler]: [ID] (Int), [Name] (Text), [Klasse] (Int)\n\n" +
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
                                  "Achten Sie unbedingt auf die WHERE-Klausel!",
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
                    Section = "Sektion 2: ER-Modellierung",
                    SkipCode = SqlLevelCodes.CodesList[8],
                    NextLevelCode = SqlLevelCodes.CodesList[9],
                    Title = "1:n Beziehungen (JOIN)",
                    Description = "In relationalen Datenbanken werden Beziehungen oft über **Fremdschlüssel** (Foreign Keys) realisiert.\n\n" +
                                  "Das Diagramm zeigt eine klassische **1:n Beziehung**: Eine Klasse hat *viele* Schüler, aber ein Schüler gehört zu genau *einer* Klasse.\n" +
                                  "Der Fremdschlüssel [K_ID_FK] in der Tabelle [Schueler] verweist auf den Primärschlüssel der [Klasse].\n\n" +
                                  "Aufgabe:\n" +
                                  "Geben Sie eine Liste aller Schüler aus, die den **Namen des Schülers** und die **Bezeichnung der Klasse** enthält.\n" +
                                  "Verbinden Sie dazu die beiden Tabellen.",
                    SetupScript = "CREATE TABLE Klasse (ID INTEGER PRIMARY KEY, Bezeichnung TEXT);" +
                                  "CREATE TABLE Schueler (ID INTEGER PRIMARY KEY, Name TEXT, K_ID_FK INTEGER);" +
                                  "INSERT INTO Klasse VALUES (1, '12-Informatik');" +
                                  "INSERT INTO Klasse VALUES (2, '11-Mathe');" +
                                  "INSERT INTO Schueler VALUES (101, 'Jan', 1);" +
                                  "INSERT INTO Schueler VALUES (102, 'Lea', 1);" +
                                  "INSERT INTO Schueler VALUES (103, 'Tom', 2);",
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "Jan", "12-Informatik" },
                        new[] { "Lea", "12-Informatik" },
                        new[] { "Tom", "11-Mathe" }
                    },
                    MaterialDocs = "start-hint: JOIN Syntax\n" +
                                   "{|SELECT ... \n" +
                                   "FROM TabelleA \n" +
                                   "JOIN TabelleB ON TabelleA.PK = TabelleB.FK;|}\n" +
                                   "Nutzen Sie die Punkt-Notation (z.B. [Schueler.Name]), um Spalten eindeutig zuzuordnen.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "imgsql\\sec2\\lvl9-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nhide circle\nskinparam linetype ortho\n\nentity \"Klasse\" {\n  *ID\n  --\n  Bezeichnung\n}\n\nentity \"Schueler\" {\n  *ID\n  --\n  Name\n  #K_ID_FK\n}\n\nKlasse ||..|{ Schueler : enthaelt\n@enduml"
                    }
                },
                new SqlLevel
                {
                    Id = 10,
                    Section = "Sektion 2: ER-Modellierung",
                    SkipCode = SqlLevelCodes.CodesList[9],
                    NextLevelCode = SqlLevelCodes.CodesList[10],
                    Title = "m:n Beziehungen (Verknüpfungstabelle)",
                    Description = "Das Diagramm zeigt eine **m:n Beziehung**: Ein Schüler kann mehrere AGs besuchen, und eine AG hat mehrere Schüler.\n" +
                                  "In SQL wird dies durch eine dritte Tabelle (hier [Teilnahme]) aufgelöst, die zwei Fremdschlüssel enthält.\n\n" +
                                  "Aufgabe:\n" +
                                  "Ermitteln Sie die Titel aller AGs, an denen der Schüler 'Max' teilnimmt.\n" +
                                  "Sie müssen über drei Tabellen joinen: [Schueler] -> [Teilnahme] -> [AG].",
                    SetupScript = "CREATE TABLE Schueler (ID INTEGER PRIMARY KEY, Name TEXT);" +
                                  "CREATE TABLE AG (ID INTEGER PRIMARY KEY, Titel TEXT);" +
                                  "CREATE TABLE Teilnahme (S_ID INTEGER, AG_ID INTEGER);" +
                                  "INSERT INTO Schueler VALUES (1, 'Max');" +
                                  "INSERT INTO Schueler VALUES (2, 'Lisa');" +
                                  "INSERT INTO AG VALUES (10, 'Robotics');" +
                                  "INSERT INTO AG VALUES (20, 'Schach');" +
                                  "INSERT INTO Teilnahme VALUES (1, 10);" +
                                  "INSERT INTO Teilnahme VALUES (1, 20);" +
                                  "INSERT INTO Teilnahme VALUES (2, 20);",
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "Robotics" },
                        new[] { "Schach" }
                    },
                    MaterialDocs = "start-tipp: Mehrfach-Join\n" +
                                   "Die Struktur sieht so aus:\n" +
                                   "{|SELECT AG.Titel\n" +
                                   "FROM Schueler\n" +
                                   "JOIN Teilnahme ON Schueler.ID = Teilnahme.S_ID\n" +
                                   "JOIN AG ON Teilnahme.AG_ID = AG.ID\n" +
                                   "WHERE ...;|}\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "imgsql\\sec2\\lvl10-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nhide circle\nskinparam linetype ortho\n\nentity \"Schueler\" {\n  *ID\n  --\n  Name\n}\n\nentity \"Teilnahme\" {\n  #S_ID\n  #AG_ID\n}\n\nentity \"AG\" {\n  *ID\n  --\n  Titel\n}\n\nSchueler ||..|{ Teilnahme\nTeilnahme }|..|| AG\n@enduml"
                    }
                },
                new SqlLevel
                {
                    Id = 11,
                    Section = "Sektion 2: ER-Modellierung",
                    SkipCode = SqlLevelCodes.CodesList[10],
                    NextLevelCode = SqlLevelCodes.CodesList[11],
                    Title = "Kardinalitäten (LEFT JOIN)",
                    Description = "In einem Krankenhaus gibt es Mitarbeiter und Parkplätze. Die Kardinalität ist **[0..1]** zu **[0..1]**.\n" +
                                  "Das bedeutet: Ein Mitarbeiter *kann* einen Parkplatz haben (muss aber nicht), und ein Parkplatz gehört maximal zu einem Mitarbeiter.\n\n" +
                                  "Das Diagramm zeigt, dass der Fremdschlüssel [M_ID] in der Tabelle [Parkplatz] liegt.\n\n" +
                                  "Aufgabe:\n" +
                                  "Finden Sie die Namen aller Mitarbeiter, die **keinen** Parkplatz besitzen.\n" +
                                  "Nutzen Sie einen [LEFT JOIN] und prüfen Sie auf [NULL].",
                    SetupScript = "CREATE TABLE Mitarbeiter (ID INTEGER PRIMARY KEY, Name TEXT);" +
                                  "CREATE TABLE Parkplatz (ID INTEGER PRIMARY KEY, Nummer INTEGER, M_ID INTEGER);" +
                                  "INSERT INTO Mitarbeiter VALUES (1, 'Dr. House');" +
                                  "INSERT INTO Mitarbeiter VALUES (2, 'Schwester Stefanie');" +
                                  "INSERT INTO Mitarbeiter VALUES (3, 'Dr. Grey');" +
                                  "INSERT INTO Parkplatz VALUES (100, 1, 1);" +
                                  "INSERT INTO Parkplatz VALUES (101, 2, 3);", // house and grey have spots
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "Schwester Stefanie" }
                    },
                    MaterialDocs = "start-hint: Outer Joins\n" +
                                   "Ein [INNER JOIN] würde Mitarbeiter ohne Parkplatz gar nicht anzeigen.\n" +
                                   "Ein [LEFT JOIN] behält alle Einträge der linken Tabelle (Mitarbeiter). Wenn kein Partner rechts gefunden wird, sind die Spalten dort [NULL].\n" +
                                   "Bedingung: [WHERE Parkplatz.ID IS NULL]\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "imgsql\\sec2\\lvl11-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nhide circle\nskinparam linetype ortho\n\nentity \"Mitarbeiter\" {\n  *ID\n  --\n  Name\n}\n\nentity \"Parkplatz\" {\n  *ID\n  --\n  Nummer\n  #M_ID\n}\n\nMitarbeiter |o..o| Parkplatz\n@enduml"
                    }
                },
                new SqlLevel
                {
                    Id = 12,
                    Section = "Sektion 2: ER-Modellierung",
                    SkipCode = SqlLevelCodes.CodesList[11],
                    NextLevelCode = "",
                    Title = "Komplexe Abfrage (Social Media)",
                    Description = "Dieses Szenario orientiert sich an einer echten Abituraufgabe (vgl. ABI 2023).\n" +
                                  "Gegeben sind Nutzer, die Beiträge erstellen. Andere Nutzer können diese Beiträge liken.\n\n" +
                                  "Struktur:\n" +
                                  "- [Nutzer] (Name, ...)\n" +
                                  "- [Beitrag] (ID, Titel, AutorName_FK)\n" +
                                  "- [Likes] (NutzerName_FK, BeitragID_FK)\n\n" +
                                  "Aufgabe:\n" +
                                  "Ermitteln Sie für jeden Beitrag den **Titel**, den **Autor** und die **Anzahl der Likes**.\n" +
                                  "Sortieren Sie absteigend nach der Anzahl der Likes.",
                    SetupScript = "CREATE TABLE Nutzer (Name TEXT PRIMARY KEY);" +
                                  "CREATE TABLE Beitrag (ID INTEGER PRIMARY KEY, Titel TEXT, Autor TEXT);" +
                                  "CREATE TABLE Likes (Nutzer TEXT, BeitragID INTEGER);" +
                                  "INSERT INTO Nutzer VALUES ('Anna'); INSERT INTO Nutzer VALUES ('Ben'); INSERT INTO Nutzer VALUES ('Chris');" +
                                  "INSERT INTO Beitrag VALUES (1, 'Mein Urlaub', 'Anna');" +
                                  "INSERT INTO Beitrag VALUES (2, 'Essen', 'Anna');" +
                                  "INSERT INTO Beitrag VALUES (3, 'Katzenvideo', 'Ben');" +
                                  // Likes
                                  "INSERT INTO Likes VALUES ('Ben', 1);" +
                                  "INSERT INTO Likes VALUES ('Chris', 1);" + // post 1: 2 Likes
                                  "INSERT INTO Likes VALUES ('Anna', 3);" + // post 3: 1 Like
                                  "INSERT INTO Likes VALUES ('Chris', 3);" + // post 3: 2 Likes
                                  "INSERT INTO Likes VALUES ('Ben', 3);", // post 3: 3 Likes total
                    VerificationQuery = "",
                    ExpectedResult = new List<string[]>
                    {
                        new[] { "Katzenvideo", "Ben", "3" },
                        new[] { "Mein Urlaub", "Anna", "2" },
                        new[] { "Essen", "Anna", "0" }
                    },
                    MaterialDocs = "start-tipp: Gruppierung\n" +
                                   "Wenn Sie [COUNT()] nutzen, müssen Sie die nicht-aggregierten Spalten (Titel, Autor) im [GROUP BY] angeben.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "imgsql\\sec2\\lvl12-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nhide circle\nskinparam linetype ortho\n\nentity \"Nutzer\" {\n  *Name\n}\n\nentity \"Beitrag\" {\n  *ID\n  --\n  Titel\n  #Autor\n}\n\nentity \"Likes\" {\n  #Nutzer\n  #BeitragID\n}\n\nNutzer ||..o{ Beitrag : verfasst\nNutzer }|..|{ Likes : liked\nLikes }|..|| Beitrag\n@enduml"
                    }
                }
            };
        }
    }
}