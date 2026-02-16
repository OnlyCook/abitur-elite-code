using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AbiturEliteCode.cs
{
    // LEVEL CREATION GUIDE
    // bound to the abitur in hesse (PI - Praktische Informatik), must use their syntax and structure
    // all diagrams must show java code only to match the abitur (difference in c# should be elaborated via hints), although c# code is always expected in the result/answer
    // levels get more difficult as they get more complex and require comprehension in a larger scale as well as independence
    // the uml diagrams must show all classes mentioned in a task (the lower the level the less classes/smaller classes in such a diagram)
    // every section should work on 1 broad topic, thus the lower levels are easier/introducing it, the last level is a mini-exam which includes most (if not all) elements learned in said section (plus elements from previous sections -> they build on each other)
    // every used external reference/implementation (for example List<T> or a custom class such as Server) must be attached to the materials tab (also in java, difference with c# explained through hints)
    // (nearly) every single level is bound to a diagram of any kind as the abitur is structure like this, the only exception to this may be given pseudo code which has to be translated to c# code
    // uml diagrams in need of conversion into c# code in the abitur are: uml class diagrams, uml sequence diagrams, and Nassi–Shneiderman diagrams, also given can be pseudo-code
    // the further the level progression the less handholding the user will get, although if the user had to implement a class for example which has to be used exactly as is in the next level, may be already implemented to not repeat the exact same thing (if it has changed a solid amout, the user should re-implement it though)
    // getters and setters should match the abiturs scheme of "getVariable()" [so in c# "GetVariable()"], using "{ get; set; }" should be avoided
    // in the abitur basically all classes (if they have any attributes that is) have a constructor which should be represented in the uml class diagram (list attributes of a class, for example, should be able to be initalized inside their constructor, as thats how the abitur does it, initalizing them in the declaration should be possible, but not the expected way)
    // on later levels (19+) the getters and setters of a class should not be included in the diagram as the user has to know that those are available anyways, that is because in the abitur we often get this note: "Auf alle Attribute kann mittels get-Methoden zugegriffen werden." i would like to add this to the more difficult levels so that the user becomes familiar with these background getters/setters that arent explicitly stated/defined by the user
    // what you commonly also see in the uml class diagrams in the abitur exams is "static int autowert = 0" or an id for a class, which the user must increment in the constructor (or in rare cases) set to a given id, this mechanic should also be added in more difficult levels where its appropriate
    // note: adequate abitur level language should be used as well as technical vocabulary

    // for sequence diagrams: keeping the exact given order of calls is important
    // null/out of bounds checks should be included by the user (in the abitur these commonly make the students lose points)
    // pseudo-code is not attached to the uml/diagrams but the materials tab as plain text

    // for nassi-shneiderman diagrams we use Structorizer which uses pascal for their diagrams through code

    // if the plantuml should actually add a new line instead of converting it by the python script that reads the plantuml source code, then use: "\n/"
    // note: in the abitur association attributes arent included in the actual class, but are indicated on the association only (this also includes its access modifiers: +/-/#), the attribute should be placed on the side of the class its referencing (and not on the side of the class which is referencing it), lists are marked with an asterisk and single attributes with a number (multiplicity)
    // if a class does not have a reference to the other (at all) it should be marked using an X on the associating arrow (note: if there is an X that means this side should not have a multiplicity as there is no reference to it)
    // note: if a method is returning 'void' it shouldnt be marked (as its like this in the abitur) [this contraint is exluded from auxiliary class diagrams]

    // on the final levels (22+) the user should just get all the diagrams at all times so that they can get what they need themselves (less handholding, higher expected indepedence from the user)

    public class Level
    {
        public int Id { get; set; }
        public string Section { get; set; }
        public string SkipCode { get; set; }
        public string NextLevelCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string StarterCode { get; set; }
        public string MaterialDocs { get; set; }
        public List<string> DiagramPaths { get; set; } = new List<string>(); // max of 3
        public List<string> PlantUMLSources { get; set; } = new List<string>(); // max of 3
        public string NassiShneiderSource { get; set; }
        public bool NoUMLAutoScale { get; set; } = false; // set to true for large (especially wide) diagrams
        public List<string> AuxiliaryIds { get; set; } = new List<string>();
        public List<string> Prerequisites { get; set; } = new List<string>();
        public List<string> OptionalPrerequisites { get; set; } = new List<string>();
    }

    public static class LevelCodes
    {
        public static string[] CodesList = {
            "ZOO", "CAP", "ABS", "LST", "ALG",
            "LOG", "INV", "SRT", "LNK", "EXP",
            "NAV", "COL", "DAT", "ELT",
            "COM", "CHK", "SEN", "PRO"
        };
    }

    public static class SharedDiagrams
    {
        public static string ListT = "@startuml\nclass \"List<T>\" {\n  + add(item : T) : void\n  + remove(item : T) : void\n  + get(index : int) : T\n  + size() : int\n  + contains(item : T) : boolean\n}\n@enduml";

        public static string Paket = "@startuml\nclass Paket {\n  - gewicht : double\n  - zielort : String\n  + Paket(ziel : String, gew : double)\n  + getGewicht() : double\n  + getZielort() : String\n}\n@enduml";

        public static string LocalDate = "@startuml\nclass LocalDate {\n  + {static} now() : LocalDate\n  + isAfter(other : LocalDate) : boolean\n  + isBefore(other : LocalDate) : boolean\n  + minusMonths(months : long) : LocalDate\n  + plusDays(days : long) : LocalDate\n}\n@enduml";

        public static string Serial = "@startuml\nclass Serial {\n  + Serial(port : String, baud : int, db : int, sb : int, p : int)\n  + open() : boolean\n  + close() : void\n  + read() : int\n  + write(val : int)\n  + dataAvailable() : int\n}\n@enduml";

        public static string FunkModul = "@startuml\nclass FunkModul {\n  + FunkModul()\n  + send(cmd : String)\n  + receive() : char\n}\n@enduml";
    }

    public static class AuxiliaryImplementations
    {
        public static string GetCode(string auxId)
        {
            return auxId switch
            {
                "Paket" => @"
                    public class Paket
                    {
                        private double gewicht;
                        private string zielort;
                        
                        public Paket(string ziel, double gew) 
                        { 
                            this.zielort = ziel; 
                            this.gewicht = gew; 
                        }

                        public double GetGewicht() { return gewicht; }
                        public string GetZielort() { return zielort; }
                    }",
                "LocalDate" => @"
                    // Mock für die Anzeige im Materials-Tab
                    // In C# nutzen Sie stattdessen struct DateTime
                    public class LocalDate
                    {
                        public static LocalDate Now() { return new LocalDate(); }
                        public bool IsAfter(LocalDate other) { return true; }
                        public bool IsBefore(LocalDate other) { return true; }
                        public LocalDate MinusMonths(long months) { return this; }
                    }",
                "Serial" => @"
                    using System.Collections.Generic;
                    public class Serial {
                        private Queue<int> _readBuffer = new Queue<int>();
                        public Serial(string p, int b, int d, int s, int par) {}
                        public bool Open() { return true; }
                        public void Close() {}
                        
                        // Mock Aufstellung zum Testen
                        public void SetTestBytes(int[] bytes) {
                            foreach(var b in bytes) _readBuffer.Enqueue(b);
                        }
                        
                        public int Read() {
                            if (_readBuffer.Count > 0) return _readBuffer.Dequeue();
                            return -1;
                        }
                        public int DataAvailable() { return _readBuffer.Count; }
                        public void Write(int val) { }
                    }",
                "FunkModul" => @"
                    public class FunkModul {
                        public void Send(string cmd) {}
                        public char Receive() { return (char)0x06; }
                    }",
                _ => ""
            };
        }
    }

    public static class Curriculum
    {
        public static List<Level> GetLevels()
        {
            string listDocsHints =
@"start-hint: List-Vergleich Java vs. C#
In C# heißt die Klasse ebenfalls [List<T>].
• Java: [list.add(item)] → C#: [list.Add(item)] usw.
• Java: [list.get(i)] → C#: [list|[i|]] oder [list.ElementAt(i)]
• Java: [list.size()] → C#: [list.Count]
• Java: [boolean] → C#: [bool]
• Listen müssen mit [new List<T>()] initialisiert werden
:end-hint";

            string localDateHint =
@"start-hint: Datumsvergleich Java vs. C#
Im Abitur (Java) wird oft [LocalDate] verwendet (siehe oben). In C# nutzen wir [DateTime].
• Java: [date.isAfter(other)] → C#: [date > other]
• Java: [date.isBefore(other)] → C#: [date < other]
• Java: [LocalDate.now()] → C#: [DateTime.Now]
• Java: [date.minusMonths(1)] → C#: [date.AddMonths(-1)]
:end-hint";

            return new List<Level>
            {
                // --- SECTION 1 ---
                new Level
                {
                    Id = 1,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = LevelCodes.CodesList[0],
                    NextLevelCode = LevelCodes.CodesList[1],
                    Title = "Klasse Tier Implementieren",
                    Description = "Überführen Sie die Klasse [Tier] aus dem UML-Klassendiagramm in C#-Code.\n\n" +
                                  "Das Diagramm zeigt die Java-Notation. Beachten Sie die Unterschiede zu C#.",
                    StarterCode = "public class Tier\n{\n    // Implementation hier\n}",
                    MaterialDocs = "start-hint: Datentypen & Modifier\n" +
                                   "In Java schreibt man [String], in C# [string].\n" +
                                   "Beide Sprachen verwenden [int] für Ganzzahlen.\n" +
                                   "Private Felder werden mit [-], public mit [+] markiert.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec1\\lvl1-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Tier {\n  - name : String\n  - alter : int\n  + Tier(name : String, alter : int)\n}\n@enduml"
                    },
                    Prerequisites = new List<string>
                    {
                        "Defining a Class", "Fields", "Public Access Modifier", "Private Access Modifier",
                        "Basic Types", "Strings", "Integers", "Default Constructors", "Parameterized Constructors"
                    }
                },
                new Level
                {
                    Id = 2,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = LevelCodes.CodesList[1],
                    NextLevelCode = LevelCodes.CodesList[2],
                    Title = "Kapselung und Validierung",
                    Description = "Implementieren Sie Datenkapselung für das Attribut [alter].\n\n" +
                                  "Aufgaben:\n" +
                                  "1. Ergänzen Sie einen Getter [GetAlter()] und einen Setter [SetAlter(int neuesAlter)].\n" +
                                  "2. Der Setter darf das Alter nur ändern, wenn der neue Wert größer als der alte ist.\n" +
                                  "3. Der Getter gibt den aktuellen Wert des Alters zurück.",
                    StarterCode = "public class Tier\n{\n    private int alter = 5;\n    \n    // Implementation hier\n}",
                    MaterialDocs = "start-hint: Namenskonventionen\n" +
                                   "In Java schreibt man [getAlter()] und [setAlter()]. In C# verwenden wir die gleiche Namenskonvention, jedoch mit großem Anfangsbuchstaben: [GetAlter()] und [SetAlter()].\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Logik im Setter\n" +
                                   "Verwenden Sie eine [if]-Bedingung im Setter, um zu prüfen, ob [neuesAlter > alter] ist.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec1\\lvl2-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Tier {\n  - alter : int\n  + setAlter(neuesAlter : int)\n  + getAlter() : int\n}\n@enduml"
                    },
                    Prerequisites = new List<string>
                    {
                        "Properties", "If statements", "Comparison operators", "Return values", "Defining void methods"
                    }
                },
                new Level
                {
                    Id = 3,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = LevelCodes.CodesList[2],
                    NextLevelCode = LevelCodes.CodesList[3],
                    Title = "Abstrakte Klassen und Vererbung",
                    Description = "Implementieren Sie die abstrakte Klasse [Tier] und die abgeleitete Klasse [Loewe].\n\n" +
                                  "Anforderungen:\n" +
                                  "1. [Tier] ist eine abstrakte Klasse mit einem geschützten Attribut [name].\n" +
                                  "2. [Loewe] erbt von [Tier] und implementiert die Methode [Bruellen()].\n" +
                                  "3. Der Konstruktor von [Loewe] ruft den Basis-Konstruktor auf.",
                    StarterCode = "// Implementieren Sie beide Klassen vollständig\n",
                    MaterialDocs = "start-hint: Syntax für Vererbung\n" +
                                   "In Java schreibt man [public abstract class Tier], in C# identisch.\n" +
                                   "Vererbung: Java verwendet [extends], C# verwendet [:].\n" +
                                   ":end-hint\n\n" +
                                   "start-tipp: Konstruktor & Basis\n" +
                                   "Der Konstruktor von Loewe sollte [base(name)] aufrufen, um den Konstruktor der Basisklasse aufzurufen.\n" +
                                   ":end-hint\n" +
                                   "start-hint: Methode Bruellen\n" +
                                   "Die Methode [Bruellen()] kann einen beliebigen String zurückgeben, z.B. \"ROAR!\"\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec1\\lvl3-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nabstract class Tier {\n  # name : String\n  + Tier(name : String)\n}\nclass Loewe {\n  - laenge : int\n  + Loewe(name : String, laenge : int)\n  + bruellen() : String\n}\nTier <|-- Loewe\n@enduml"
                    },
                    Prerequisites = new List<string>
                    {
                        "Inheritance Basics", "Abstract Classes", "The base Keyword", "Abstract Methods", "Method Overriding"
                    }
                },
                new Level
                {
                    Id = 4,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = LevelCodes.CodesList[3],
                    NextLevelCode = LevelCodes.CodesList[4],
                    Title = "Gehege-Verwaltung mit List",
                    Description = "Erstellen Sie die Klassen [Tier] sowie [Gehege], letztere soll eine Liste von Tieren verwalten.\n\n" +
                                  "Die Klasse [Gehege] soll folgende Funktionalität bieten:\n" +
                                  "• Einen Konstruktor, der die Liste initialisiert\n" +
                                  "• Eine Methode [Hinzufuegen(Tier t)], die ein Tier zur Liste hinzufügt\n" +
                                  "• Eine Methode [AnzahlTiere()], die die Anzahl der Tiere zurückgibt\n\n" +
                                  "Hinweis: Verwenden Sie für dieses Level eine normale (nicht abstrakte) Klasse Tier.",
                    StarterCode = "// Erstellen Sie die Klassen Gehege und Tier vollständig selbst\n",
                    MaterialDocs = listDocsHints,
                    DiagramPaths = new List<string>
                    {
                        "img\\sec1\\lvl4-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Gehege {\n  + Gehege()\n  + hinzufuegen(t : Tier)\n  + anzahlTiere() : int\n}\nclass Tier {\n  + Tier()\n}\nGehege x--> \"*\" Tier : -bewohner\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "ListT" },
                    Prerequisites = new List<string>
                    {
                        "Creating Lists", "Adding to Lists", "Accessing List Elements", "Count and Sum"
                    }
                },
                new Level
                {
                    Id = 5,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = LevelCodes.CodesList[4],
                    NextLevelCode = LevelCodes.CodesList[5],
                    Title = "Algorithmus: Das Älteste Tier",
                    Description = "Implementieren Sie die Methode [ErmittleAeltestes()] in der Klasse [Gehege].\n\n" +
                                  "Die Methode soll:\n" +
                                  "• Das Tier mit dem höchsten Alter zurückgeben\n" +
                                  "• Bei mehreren Tieren mit gleichem Höchstalter das erste gefundene zurückgeben\n" +
                                  "• Bei leerer Liste [null] zurückgeben\n\n" +
                                  "Dies ist das Abschluss-Level von Sektion 1. Es kombiniert alle bisherigen Konzepte.",
                    StarterCode = "public class Gehege\n{\n    private List<Tier> bewohner;\n    \n    // Implementation hier\n}\n\npublic class Tier\n{\n    private int alter;\n    \n    public Tier(int alter)\n    {\n        this.alter = alter;\n    }\n    \n    public int GetAlter()\n    {\n        return alter;\n    }\n}\n",
                    MaterialDocs = listDocsHints + "\n\n" +
                                   "start-tipp: Strategie für die Suche\n" +
                                   "1. Erstellen Sie vor der Schleife eine Variable [Tier aeltestes = null;].\n" +
                                   "2. Iterieren Sie durch alle Tiere ([foreach]).\n" +
                                   "3. Prüfen Sie: Ist [aeltestes] noch null? ODER ist das aktuelle Tier älter als [aeltestes]?\n" +
                                   "4. Wenn ja: Setzen Sie [aeltestes] auf das aktuelle Tier.\n" +
                                   "5. Geben Sie am Ende [aeltestes] zurück.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec1\\lvl5-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Gehege {\n  + Gehege()\n  + ermittleAeltestes() : Tier\n}\nclass Tier {\n  - alter : int\n  + Tier(alter : int)\n  + getAlter() : int\n}\nGehege x--> \"*\" Tier : -bewohner\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "ListT" },
                    Prerequisites = new List<string>
                    {
                        "For-Each Loops", "If statements", "Comparison operators", "Returning Values"
                    }
                },

                // --- SECTION 2 ---
                new Level
                {
                    Id = 6,
                    Section = "Sektion 2: Datenstrukturen & Algorithmen",
                    SkipCode = LevelCodes.CodesList[5],
                    NextLevelCode = LevelCodes.CodesList[6],
                    Title = "Das Warenlager (Suche)",
                    Description = "Das Logistik-Zentrum benötigt eine Funktion, um das **leichteste** Paket für Eil-Kurierfahrten zu finden.\n\n" +
                                  "Aufgabe:\n" +
                                  "1. Überführen Sie das Klassendiagramm exakt in Code (Klasse [Paket] und [Lager]).\n" +
                                  "2. Implementieren Sie [ErmittleLeichtestes()], die das Paket mit dem **niedrigsten** Gewicht zurückgibt.\n",
                    StarterCode = "public class Lager\n{\n    // Liste und Methoden\n}\n\npublic class Paket\n{\n    // Attribute, Konstruktor, Getter\n}",
                    MaterialDocs = listDocsHints + "\n\n" +
                                   "start-tipp: Startwert für Suche\n" +
                                   "Nutzen Sie eine Variable [leichtestesPaket], die Sie initial auf das erste Element der Liste setzen (falls vorhanden) oder auf null.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec2\\lvl6-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Lager {\n  + Lager()\n  + hinzufuegen(p : Paket)\n  + ermittleLeichtestes() : Paket\n}\nclass Paket {\n  - gewicht : double\n  - zielort : String\n  + Paket(ziel : String, gew : double)\n  + getGewicht() : double\n}\nLager x--> \"*\" Paket : -pakete\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "ListT" },
                    Prerequisites = new List<string>
                    {
                        "Creating Lists", "For-Each Loops", "Doubles", "If statements"
                    }
                },
                new Level
                {
                    Id = 7,
                    Section = "Sektion 2: Datenstrukturen & Algorithmen",
                    SkipCode = LevelCodes.CodesList[6],
                    NextLevelCode = LevelCodes.CodesList[7],
                    Title = "Die Inventur (Filtern)",
                    Description = "Für den Versand müssen Pakete gefiltert werden.\n\n" +
                                  "Aufgabe:\n" +
                                  "Implementieren Sie die Klasse [Lager].\n" +
                                  "Die [FilterePakete(String ort)] Methode soll eine **neue Liste** zurückgeben, die nur Pakete enthält, die:\n" +
                                  "1. An den übergebenen [ort] adressiert sind.\n" +
                                  "2. **Und** schwerer als 10.0 kg sind.",
                    StarterCode = "public class Lager\n{\n    private List<Paket> pakete;\n\n    public List<Paket> FilterePakete(string ort)\n    {\n        // Implementation hier\n        return null;\n    }\n}",
                    MaterialDocs = "start-hint: C# Operatoren\n" +
                                   "Strings vergleicht man in C# mit [==] oder [Equals()].\n" +
                                   "[&&] ist der Operator für das logische UND.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec2\\lvl7-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Lager {\n  + Lager()\n  + hinzufuegen(Paket p)\n  + filterePakete(ort : String) : List<Paket>\n}\nLager x--> \"*\" Paket : -pakete\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "Paket" },
                    Prerequisites = new List<string>
                    {
                        "Logical AND", "String Comparisons", "Adding to Lists", "Return values"
                    }
                },
                new Level
                {
                    Id = 8,
                    Section = "Sektion 2: Datenstrukturen & Algorithmen",
                    SkipCode = LevelCodes.CodesList[7],
                    NextLevelCode = LevelCodes.CodesList[8],
                    Title = "Die Sortiermaschine (Bubble Sort)",
                    Description = "Die Pakete müssen vor dem Verladen nach Gewicht aufsteigend sortiert werden.\n\n" +
                                  "Aufgabe:\n" +
                                  "Implementieren Sie den **Bubble Sort** Algorithmus in der Methode [Sortiere()].\n" +
                                  "Sie dürfen **keine** fertigen Sortierfunktionen (wie .Sort() oder .OrderBy()) verwenden.",
                    StarterCode = "public class Lager\n{\n    private List<Paket> pakete;\n\n    public void Sortiere()\n    {\n        // Bubble Sort Implementation\n    }\n}",
                    MaterialDocs = "start-hint: Bubble Sort Logik\n" +
                                   "1. Äußere Schleife von [i=0] bis [n-1]\n" +
                                   "2. Innere Schleife von [j=0] bis [n-i-1]\n" +
                                   "3. Wenn [pakete|[j|]] schwerer als [pakete|[j+1|]] -> Tauschen.\n" +
                                   ":end-hint\n\n" +
                                   "start-tipp: Tausch-Logik (Swap)\n" +
                                   "{|Paket temp = pakete[j];\n" +
                                   "pakete[j] = pakete[j+1];\n" +
                                   "pakete[j+1] = temp;|}\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec2\\lvl8-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Lager {\n  + sortiere()\n}\nLager x--> \"*\" Paket : -pakete\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "Paket" },
                    Prerequisites = new List<string>
                    {
                        "For Loops", "Accessing List Elements", "Modifying Array Elements", "Variables"
                    }
                },
                new Level
                {
                    Id = 9,
                    Section = "Sektion 2: Datenstrukturen & Algorithmen",
                    SkipCode = LevelCodes.CodesList[8],
                    NextLevelCode = LevelCodes.CodesList[9],
                    Title = "Der Gabelstapler (Verkettete Liste)",
                    Description = "Das Förderband wird als einfach verkettete Liste modelliert. Jedes Element ist ein [Knoten].\n\n" +
                                  "Aufgabe:\n" +
                                  "1. Implementieren Sie die Klasse [Knoten] gemäß Diagramm.\n" +
                                  "2. Implementieren Sie die Methode [Anhaengen(Paket p)] in der Klasse [Foerderband].\n\n" +
                                  "Logik für Anhaengen:\n" +
                                  "Ist das Band leer ([kopf] ist null), wird der neue Knoten zum Kopf.\n" +
                                  "Sonst müssen Sie bis zum letzten Knoten laufen und den neuen Knoten dort anhängen.",
                    StarterCode = "public class Foerderband\n{\n    // Implementation\n}\n\npublic class Knoten\n{\n    // Implementation\n}\n",
                    MaterialDocs = "start-hint: Wie funktionieren Verkettete Listen? (Simpel)\n" +
                                   "Stell dir eine Schnitzeljagd (Schatzsuche) vor:\n" +
                                   "• Der [kopf] ist der erste Zettel, den du in die Hand bekommst.\n" +
                                   "• Jeder Zettel (Knoten) hat einen Inhalt (das Paket) und einen Hinweis, wo der **nächste** Zettel liegt (Referenz [nachfolger]).\n" +
                                   "• Wenn auf einem Zettel kein Hinweis mehr steht (Referenz ist [null]), bist du am Ende der Kette angekommen.\n\n" +
                                   "Um hinten etwas anzuhängen, musst du also beim Kopf starten und dich 'hochhangeln', bis du den Knoten findest, dessen [nachfolger] leer ist.\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Traversierung (Laufen)\n" +
                                   "Traversierung in Java:\n" +
                                   "{|Knoten aktuell = kopf;\n" +
                                   "while(aktuell.getNachfolger() != null) {\n" +
                                   "   aktuell = aktuell.getNachfolger();\n" +
                                   "}|}\n" +
                                   "Nach der Schleife ist [aktuell] das letzte Element.\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Sonderfall leere Liste\n" +
                                   "Wenn kein Kopf existiert, setzen Sie den Kopf auf den neuen Knoten.\n" +
                                   "**Wichtig**: Schreiben Sie direkt danach ein [return;]. Damit wird die Methode sofort beendet, denn wenn es keinen Kopf gab ist der neue Kopf gleichzeitig das Ende der verketteten Liste und das Ziel der Methode ist erfüllt.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec2\\lvl9-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Knoten {\n  + Knoten(p : Paket)\n  + getNachfolger() : Knoten\n  + setNachfolger(k : Knoten)\n}\nclass Foerderband {\n  + anhaengen(p : Paket)\n}\nFoerderband x--> \"0..1\" Knoten : -kopf\nKnoten x--> \"0..1\" Knoten : -nachfolger\nKnoten x--> \"1\" Paket : -inhalt\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "Paket" },
                    Prerequisites = new List<string>
                    {
                        "Defining a Class", "Fields", "While Loops", "Variables"
                    }
                },
                new Level
                {
                    Id = 10,
                    Section = "Sektion 2: Datenstrukturen & Algorithmen",
                    SkipCode = LevelCodes.CodesList[9],
                    NextLevelCode = LevelCodes.CodesList[10],
                    Title = "Die Express-Lieferung",
                    Description = "Dies ist die Abschlussprüfung für Sektion 2.\n\n" +
                                  "Aufgabe:\n" +
                                  "1. Ergänzen Sie den Konstruktor der Klasse [LogistikZentrum], um die Liste [allePakete] zu initialisieren.\n" +
                                  "2. Implementieren Sie [GetTop3Schwere(String ort)].\n" +
                                  "   -> Die Methode gibt die **3 schwersten Pakete** für einen Zielort zurück (absteigend sortiert).\n\n" +
                                  "Anforderungen:\n" +
                                  "• Filtern nach Ort\n" +
                                  "• Sortieren nach Gewicht (Absteigend)\n" +
                                  "• Maximal 3 Elemente zurückgeben.",
                    StarterCode = "public class LogistikZentrum\n{\n    private List<Paket> allePakete;\n\n    public List<Paket> GetTop3Schwere(string ort)\n    {\n        return null;\n    }\n}",
                    MaterialDocs = "start-tipp: Transferleistung\n" +
                                   "Sie können Ihre Bubble-Sort Logik wiederverwenden oder Hilfslisten nutzen.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec2\\lvl10-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass LogistikZentrum {\n  + LogistikZentrum()\n  + getTop3Schwere(ort : String) : List<Paket>\n}\nLogistikZentrum x--> \"*\" Paket : -allePakete\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "Paket" },
                    Prerequisites = new List<string>
                    {
                        "Sorting Lists", "Accessing List Elements", "Constructors"
                    },
                    OptionalPrerequisites = new List<string>
                    {
                        "Where for Filtering"
                    }
                },

                // --- SECTION 3 ---
                new Level
                {
                    Id = 11,
                    Section = "Sektion 3: Beziehungen & Navigation",
                    SkipCode = LevelCodes.CodesList[10],
                    NextLevelCode = LevelCodes.CodesList[11],
                    Title = "Das Klassenzimmer (Bedingte Logik)",
                    Description = "Wir analysieren die Leistung einer Schulklasse.\n\n" +
                                  "Aufgabe:\n" +
                                  "1. Implementieren Sie die Klasse [Schueler].\n" +
                                  "2. Implementieren Sie die Klasse [Klasse].\n" +
                                  "3. Schreiben Sie die Methode [BerechneSchnittBestanden()].\n\n" +
                                  "Logik:\n" +
                                  "Berechnen Sie den Notendurchschnitt (double), aber berücksichtigen Sie nur Schüler, die **bestanden haben** (Note > 4 Punkte).\n" +
                                  "Gibt es keine bestandenen Prüfungen, geben Sie 0.0 zurück.",
                    StarterCode = "public class Klasse\n{\n}\n\npublic class Schueler\n{\n}\n",
                    MaterialDocs = "start-hint: Initialisierung\n" +
                                   "Im Abitur ist es üblich, Listen im Konstruktor zu instanziieren: [liste = new List<T>();]\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Filtern und Zählen\n" +
                                   "Sie dürfen nicht durch [liste.Count] teilen, sondern durch die Anzahl der Schüler, die tatsächlich in die Summe eingegangen sind (Counter in der Schleife).\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec3\\lvl11-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Klasse {\n  - bezeichnung : String\n  + Klasse(bez : String)\n  + addSchueler(s : Schueler)\n  + berechneSchnittBestanden() : double\n}\nclass Schueler {\n  - note : int\n  + Schueler(note : int)\n  + getNote() : int\n}\nKlasse x--> \"*\" Schueler : -schuelerListe\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "ListT" },
                    Prerequisites = new List<string>
                    {
                        "Creating Lists", "Constructors", "For-Each Loops", "If statements", "Doubles"
                    }
                },
                new Level
                {
                    Id = 12,
                    Section = "Sektion 3: Beziehungen & Navigation",
                    SkipCode = LevelCodes.CodesList[11],
                    NextLevelCode = LevelCodes.CodesList[12],
                    Title = "Das Kollegium (Geschachtelte Listen)",
                    Description = "Die Schulverwaltung muss Lehrer identifizieren, die überlastet sind.\n\n" +
                                  "Aufgabe:\n" +
                                  "1. Implementieren Sie die Klassen [Lehrer] und [Schule] (inkl. Konstruktoren!).\n" +
                                  "2. Ein Lehrer hat eine Liste von Klassen. Stellen Sie sicher, dass diese Liste von außen abrufbar ist.\n" +
                                  "3. Implementieren Sie [FindeVielBeschaeftigte()] in der Klasse [Schule].\n" +
                                  "   -> Die Methode iteriert über alle Lehrer und prüft manuell auf der Liste des Lehrers, ob er in **mehr als 2** Klassen unterrichtet." +
                                  "\n\nHinweis: Die Klasse [Klasse] dient hier nur als Datenobjekt und kann leer bleiben.",
                    StarterCode = "public class Schule\n{\n    public List<Lehrer> FindeVielBeschaeftigte()\n    {\n        return null;\n    }\n}\n\npublic class Lehrer\n{\n}\n\npublic class Klasse\n{\n    // Kann leer bleiben\n}\n",
                    MaterialDocs = "start-hint: Verschachtelte Navigation\n" +
                                   "Hier ist die Kette: Schule -> Lehrer -> Liste<Klasse>.\n" +
                                   "Sie müssen die Liste des Lehrers abrufen (z.B. [l.GetKlassen()]) und *darauf* die Eigenschaft [.Count] prüfen.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec3\\lvl12-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Schule {\n  + Schule()\n  + addLehrer(l : Lehrer)\n  + findeVielBeschaeftigte() : List<Lehrer>\n}\nclass Lehrer {\n  + Lehrer()\n  + addKlasse(k : Klasse)\n  + getKlassen() : List<Klasse>\n}\nclass Klasse {\n}\nSchule x--> \"*\" Lehrer : -lehrerListe\nLehrer x--> \"*\" Klasse : -klassen\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "ListT" },
                    Prerequisites = new List<string>
                    {
                        "Creating Lists", "For-Each Loops", "If statements", "Count and Sum"
                    }
                },
                new Level
                {
                    Id = 13,
                    Section = "Sektion 3: Beziehungen & Navigation",
                    SkipCode = LevelCodes.CodesList[12],
                    NextLevelCode = LevelCodes.CodesList[13],
                    Title = "Zeugniskonferenz (Datumslogik)",
                    Description = "Das System muss prüfen, ob Schüler im letzten Monat unentschuldigt gefehlt haben.\n\n" +
                                  "Aufgabe:\n" +
                                  "1. Implementieren Sie die Klasse [Fehltag] und [Schueler].\n" +
                                  "2. Implementieren Sie [HatKritischGefehlt()] in der Klasse [Schueler].\n\n" +
                                  "Logik:\n" +
                                  "Die Methode gibt [true] zurück, wenn der Schüler mindestens einen Fehltag hat, der:\n" +
                                  "• **Nicht entschuldigt** ist\n" +
                                  "• **Und** im letzten Monat lag (Datum ist **nach** [Heute - 1 Monat]).",
                    StarterCode = "public class Schueler\n{\n    public bool HatKritischGefehlt()\n    {\n        return false;\n    }\n}\n\npublic class Fehltag\n{\n}\n",
                    MaterialDocs = localDateHint,
                    DiagramPaths = new List<string>
                    {
                        "img\\sec3\\lvl13-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Schueler {\n  + Schueler()\n  + addFehltag(f : Fehltag)\n  + hatKritischGefehlt() : boolean\n}\nclass Fehltag {\n  - datum : LocalDate\n  - entschuldigt : boolean\n  + Fehltag(d : LocalDate, e : boolean)\n  + getDatum() : LocalDate\n  + istEntschuldigt() : boolean\n}\nSchueler x--> \"*\" Fehltag : -fehltage\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "ListT", "LocalDate" },
                    Prerequisites = new List<string>
                    {
                        "DateTime Basics", "Comparing Dates", "Date Arithmetic", "Booleans", "If statements"
                    }
                },
                new Level
                {
                    Id = 14,
                    Section = "Sektion 3: Beziehungen & Navigation",
                    SkipCode = LevelCodes.CodesList[13],
                    NextLevelCode = LevelCodes.CodesList[14],
                    Title = "Der Elternbrief",
                    Description = "Abschlussprüfung Sektion 3: Generieren Sie Warnbriefe für gefährdete Schüler.\n\n" +
                                  "Aufgabe:\n" +
                                  "1. Implementieren Sie die Struktur: [Schule] -> [Klasse] -> [Schueler].\n" +
                                  "2. Implementieren Sie [ErstelleWarnungen()] in der Klasse [Schule].\n\n" +
                                  "Logik:\n" +
                                  "Iterieren Sie durch **alle Klassen** und **alle Schüler**.\n" +
                                  "Wenn ein Schüler weniger als 5 Punkte hat, fügen Sie folgende Zeile zum Ergebnis-String hinzu:\n" +
                                  "\"Warnung an Eltern von [Name] (Klasse [Bezeichnung]): Note [Note] ist kritisch!\\n\"",
                    StarterCode = "public class Schule\n{\n    public string ErstelleWarnungen()\n    {\n        string bericht = \"\";\n        // Implementation\n        return bericht;\n    }\n}\n\npublic class Klasse\n{\n}\n\npublic class Schueler\n{\n}\n",
                    MaterialDocs = "start-hint: String Aufbau\n" +
                                   "Nutzen Sie den Verkettungsoperator [+] oder [+=], um den String zusammenzubauen.\n" +
                                   "Vergessen Sie nicht den Zeilenumbruch [\\n] am Ende jeder Warnung.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec3\\lvl14-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Schule {\n  + Schule()\n  + addKlasse(k : Klasse)\n  + erstelleWarnungen() : String\n}\nclass Klasse {\n  - bezeichnung : String\n  + Klasse(bez : String)\n  + addSchueler(s : Schueler)\n  + getSchueler() : List<Schueler>\n  + getBezeichnung() : String\n}\nclass Schueler {\n  - name : String\n  - note : int\n  + Schueler(name : String, note : int)\n  + getNote() : int\n  + getName() : String\n}\nSchule x--> \"*\" Klasse : -klassen\nKlasse x--> \"*\" Schueler : -schueler\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "ListT" },
                    Prerequisites = new List<string>
                    {
                        "Nested Loops", "String concatenation", "Accessing List Elements"
                    }
                },

                // --- SECTION 4 ---
                new Level
                {
                    Id = 15,
                    Section = "Sektion 4: Kommunikation & Protokolle",
                    SkipCode = LevelCodes.CodesList[14],
                    NextLevelCode = LevelCodes.CodesList[15],
                    Title = "Protokoll-Parser (Switch)",
                    Description = "Das Kontrollzentrum sendet Befehle an den Mars Rover. Das Protokoll unterstützt verschiedene Kommandotypen.\n\n" +
                                  "Aufgabe:\n" +
                                  "1. Überführen Sie das Klassendiagramm exakt in Code ([Rover] und [Kontrollzentrum]).\n" +
                                  "2. Im Konstruktor von [Kontrollzentrum] muss der Rover mit einer beliebigen ID (z.B. \"Curiosity\") initialisiert werden.\n" +
                                  "3. Implementieren Sie die Methode [VerarbeiteKommando(string befehl)] im Kontrollzentrum.\n\n" +
                                  "Die Methode empfängt Strings und muss diese am Semikolon [;] zerlegen. Nutzen Sie eine [switch]-Anweisung (oder else-if):\n" +
                                  "• Bei \"TR\" -> Rufen Sie [Turn] mit der übergebenen Richtung auf (z.B. \"TR;LEFT\").\n" +
                                  "• Bei \"MV\" -> Rufen Sie [Move] mit den zwei übergebenen Koordinaten auf (z.B. \"MV;50;90\").\n" +
                                  "• Bei \"SC\" -> Rufen Sie [Scan] mit dem übergebenen Ziel auf (z.B. \"SC;ROCK\") und speichern Sie den Rückgabewert im Attribut [letzterScan].",
                    StarterCode = "public class Kontrollzentrum\n{\n    // Implementation hier\n}\n\npublic class Rover\n{\n    // Implementation hier\n}",
                    MaterialDocs = localDateHint + "\n\n" +
                                   "start-hint: Strings zerlegen\n" +
                                   "In C# nutzen Sie [befehl.Split(';')]. Das Ergebnis ist ein Array von Strings (string[]).\n" +
                                   "Beispiel: \"MV;50;90\" -> [\"MV\", \"50\", \"90\"].\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Arrays in C#\n" +
                                   "Ein Array mit zwei Integern erstellen Sie im Code mit [new int|[2|]].\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec4\\lvl15-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Rover {\n  - id : String\n  - position : int[]\n  - ausrichtung : String\n  + Rover(id : String)\n  + turn(richtung : String)\n  + move(x : int, y : int)\n  + scan(ziel : String) : LocalDate\n}\nclass Kontrollzentrum {\n  - letzterScan : LocalDate\n  + Kontrollzentrum()\n  + verarbeiteKommando(befehl : String)\n}\nKontrollzentrum x--> \"1\" Rover : -rover\n@enduml"
                    },
                    AuxiliaryIds = new List<string> { "LocalDate" },
                    Prerequisites = new List<string>
                    {
                        "String Operations", "Arrays", "Type Conversion", "Switch Statements", "DateTime Basics"
                    }
                },
                new Level
                {
                    Id = 16,
                    Section = "Sektion 4: Kommunikation & Protokolle",
                    SkipCode = LevelCodes.CodesList[15],
                    NextLevelCode = LevelCodes.CodesList[16],
                    Title = "Paket-Validierung (Struktogramm)",
                    Description = "Um Übertragungsfehler in Netzwerken zu vermeiden, werden Datenpakete beim Empfang validiert.\n\n" +
                                  "Ein Paket-Array ist wie folgt aufgebaut:\n" +
                                  "Index 0: Header (Paket-ID)\n" +
                                  "Index 1 bis Länge-2: Die eigentlichen Nutzdaten (Payload)\n" +
                                  "Letzter Index: Die übertragene Prüfsumme\n\n" +
                                  "Aufgabe:\n" +
                                  "1. Implementieren Sie die Klassen [DatenPaket] und [NetzwerkKnoten] gemäß des Klassendiagramms.\n" +
                                  "2. Setzen Sie das Nassi-Shneiderman-Diagramm (Struktogramm) exakt in die Methode [ValidierePaket] um.",
                    StarterCode = "public class DatenPaket\n{\n    // Implementation hier\n}\n\npublic class NetzwerkKnoten\n{\n    // Implementation hier\n}",
                    MaterialDocs = "start-hint: Modulo Operator\n" +
                                   "Der Modulo-Operator in C# ist [%].\n" +
                                   "Beispiel: [10 % 3] ergibt [1].\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Array Länge\n" +
                                   "Die Länge eines Arrays erhalten Sie in C# mit [array.Length].\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec4\\lvl16-1.svg",
                        "img\\sec4\\lvl16-2.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass DatenPaket {\n  - inhalt : int[]\n  + DatenPaket(daten : int[])\n  + getInhalt() : int[]\n}\nclass NetzwerkKnoten {\n  - knotenId : String\n  + NetzwerkKnoten(id : String)\n  + validierePaket(p : DatenPaket) : int\n}\n@enduml"
                    },
                    NassiShneiderSource = @"PROGRAM ValidierePaket;

VAR
  daten      : ARRAY OF INTEGER;
  summe      : INTEGER;
  i          : INTEGER;
  pruefsumme : INTEGER;
  ergebnis   : INTEGER;

BEGIN
  daten := p.getInhalt();
  
  IF length(daten) < 3 THEN
  BEGIN
    ergebnis := -1;
  END
  ELSE
  BEGIN
    summe := 0;
    i := 1;
    
    WHILE i < length(daten) - 1 DO
    BEGIN
      summe := summe + daten[i];
      i := i + 1;
    END;
    
    pruefsumme := summe MOD 256;
    
    IF pruefsumme = daten[length(daten) - 1] THEN
    BEGIN
      ergebnis := daten[0];
    END
    ELSE
    BEGIN
      ergebnis := -2;
    END;
  END;
END.",
                    NoUMLAutoScale = true,
                    Prerequisites = new List<string>
                    {
                        "While Loops", "Arrays", "Arithmetic Operators", "If statements"
                    }
                },
                new Level
                {
                    Id = 17,
                    Section = "Sektion 4: Kommunikation & Protokolle",
                    SkipCode = LevelCodes.CodesList[16],
                    NextLevelCode = LevelCodes.CodesList[17],
                    Title = "Die Sicherheitsschleuse (Serielle Schnittstelle)",
                    Description = "Der Zugang zum Rover-Hangar wird durch ein RFID-System gesichert. Sie müssen die Hardware-Ansteuerung implementieren.\n\n" +
                                  "Aufgabe 1: Implementieren Sie die Klasse [Rover].\n" +
                                  "• Sie benötigt eine ID (String) und eine [fahrzeugNr] (int).\n" +
                                  "• Die [fahrzeugNr] wird über einen statischen Zähler ([autowert]) automatisch im Konstruktor vergeben (Start bei 1).\n" +
                                  "• [Lock]/[Unlock]: Diese Methoden sollen nur den Status auf der Konsole ausgeben (lasse diese leer).\n\n" +
                                  "Aufgabe 2: Implementieren Sie [RFIDReader].\n" +
                                  "• [ReadCard()] liest Byte für Byte von der Seriellen Schnittstelle (siehe Protokoll unten).\n" +
                                  "• [IsCardAvailable()] gibt an, ob Daten (positive Zahl die nicht [0] ist) an der Schnittstelle zum Lesen bereitstehen (siehe [Serial] in Material).\n" +
                                  "• Protokoll: Warten auf STX (0x02) -> 6 Datenbytes lesen -> Prüfsumme lesen -> Auf ETX (0x03) prüfen.\n" +
                                  "• Validierung: Die XOR-Prüfsumme (alle 6 Datenbytes XOR verknüpfen) muss mit der gelesenen Summe übereinstimmen. Sonst Leerstring zurückgeben.\n\n" +
                                  "Aufgabe 3: Implementieren Sie [Controller].\n" +
                                  "• Der Konstruktor initialisiert [Serial] mit dem übergebenen [port] und den Werten (siehe Material), öffnet den Port und erstellt den Reader.\n" +
                                  "• [Run()] führt eine Endlosschleife aus: Wenn eine Karte verfügbar ist -> ID lesen -> Wenn ID nicht leer -> 'UNLOCK <FahrzeugNr> <CardID>' per Funk senden -> Auf ACK (0x06) prüfen -> [rover.Unlock()] aufrufen.\n\n" +
                                  "Hinweis: Die Klassen [Serial] und [FunkModul] sind gegeben (siehe Material).",
                    StarterCode = "public class Rover\n{\n    // Implementation\n}\n\npublic class RFIDReader\n{\n    private Serial serial;\n    // Implementation\n}\n\npublic class Controller\n{\n    // Implementation\n}",
                    MaterialDocs = "Verwenden Sie zur Initialisierung der seriellen Schnittstelle folgende Parameter:\n" +
                                   "{|Baud=9600, DataBits=8, StopBits=1, Parity=0|}\n" +
                                   "start-hint: Autowert\n" +
                                   "Setzen Sie die [fahrzeugNr] auf das Vorinkrement (\"pre-increment\") des Autowerts ([++autowert]), damit die Nummer bei 1 anfangen muss und es keine Duplikate gibt.\n" +
                                   ":end-hint\n" +
                                   "start-hint: XOR Operator\n" +
                                   "In C# wird der XOR-Operator durch das Zirkumflex-Symbol [^] dargestellt.\n" +
                                   "Beispiel: [int ergebnis = byte1 ^ byte2;]\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Char vs Int\n" +
                                   "Serial.Read() gibt [int] zurück. Für den String müssen Sie diesen in [char] casten.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec4\\lvl17-1.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass Rover {\n  - {static} autowert : int = 0\n  - fahrzeugNr : int\n  - id : String\n  + Rover(id : String)\n  + getFahrzeugNr() : int\n  + unlock()\n  + lock()\n}\nclass Controller {\n  + Controller(port : String, r : Rover)\n  + run()\n}\nclass RFIDReader {\n  + RFIDReader(s : Serial)\n  + readCard() : String\n  + isCardAvailable() : boolean\n}\nclass Serial {\n}\nclass FunkModul {\n}\nController x--> \"1\" Serial : -serial\nController x--> \"1\" FunkModul : -funk\nController x--> \"1\" RFIDReader : -reader\nController x--> \"1\" Rover : -rover\nRFIDReader x--> \"1\" Serial : -serial\n@enduml"
                    },
                    NoUMLAutoScale = true,
                    AuxiliaryIds = new List<string> { "Serial", "FunkModul" },
                    Prerequisites = new List<string>
                    {
                        "Static Fields", "While Loops", "Bitwise Operators", "Casting", "Object Interaction"
                    }
                },
                new Level
                {
                    Id = 18,
                    Section = "Sektion 4: Kommunikation & Protokolle",
                    SkipCode = LevelCodes.CodesList[17],
                    NextLevelCode = "",
                    Title = "Missions-Zentrale",
                    Description = "Abschlussprüfung Sektion 4: Objekt-Kollaboration und Zustandsverwaltung.\n\n" +
                                  "Die Kommunikation mit der Mars-Basis wurde um einen Wartungsdienst erweitert. " +
                                  "Der Datenstrom enthält nun Status-Updates, die den Zustand der Rover verändern.\n\n" +
                                  "**Protokollspezifikation:**\n" +
                                  "Format: |[ROVER_ID|]#|[MSG_TYPE|]#|[VALUE|]|...|...\n" +
                                  "Beispiel: \"CUR#ERR#Motor|PER#BAT#85|CUR#BAT#10\"\n\n" +
                                  "**Aufgaben:**\n" +
                                  "1. Implementieren Sie das Klassendiagramm.\n" +
                                  "2. Implementieren Sie die Logik in der Klasse [Rover]:\n" +
                                  "   • Das Attribut [batterie] startet bei 100.\n" +
                                  "   • Methode [VerarbeiteStatus]: \n" +
                                  "     - Bei Typ \"ERR\": Batterie um 5 verringern, Log hinzufügen.\n" +
                                  "     - Bei Typ \"BAT\": Batterie auf den übergebenen Wert (int) setzen.\n" +
                                  "     - Rückgabewert: [true], wenn Batterie < 20 (kritisch), sonst [false].\n" +
                                  "3. Setzen Sie das Nassi-Shneiderman-Diagramm in [MissionsZentrale] um:\n" +
                                  "   • Zerlegen Sie den Strom.\n" +
                                  "   • Leiten Sie die Daten an den passenden Rover weiter.\n" +
                                  "   • Falls der Rover einen kritischen Status meldet, erstellen Sie ein Ticket beim [WartungsDienst].",
                    StarterCode = "// Implementieren Sie alle Klassen selbständig",
                    MaterialDocs = "start-hint: Integer Parsing\n" +
                                   "Um den Wert für \"BAT\" zu setzen, müssen Sie den String in einen Integer umwandeln:\n" +
                                   "{|int wert = int.Parse(stringWert);|}\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Wartung\n" +
                                   "Die MissionsZentrale muss prüfen, ob [r.VerarbeiteStatus(...)] wahr zurückgibt. Nur dann wird [wartung.ErstelleTicket(...)] aufgerufen.\n" +
                                   ":end-hint",
                    DiagramPaths = new List<string>
                    {
                        "img\\sec4\\lvl18-1.svg",
                        "img\\sec4\\lvl18-2.svg"
                    },
                    PlantUMLSources = new List<string>
                    {
                        "@startuml\nclass MissionsZentrale {\n  + MissionsZentrale()\n  + addRover(r : Rover)\n  + verarbeiteDatenstrom(stream : String)\n}\nclass Rover {\n  - id : String\n  - batterie : int = 100\n  + Rover(id : String)\n  + verarbeiteStatus(typ : String, val : String) : boolean\n  + getId() : String\n}\nclass WartungsDienst {\n  + WartungsDienst()\n  + erstelleTicket(roverId : String, grund : String)\n}\nclass WartungsTicket {\n  - roverId : String\n  - grund : String\n  + WartungsTicket(id : String, grund : String)\n}\nclass LogEintrag {\n  - typ : String\n  - inhalt : String\n  + LogEintrag(typ : String, inhalt : String)\n}\nMissionsZentrale x--> \"*\" Rover : -roverListe\nMissionsZentrale x--> \"1\" WartungsDienst : -wartung\nWartungsDienst x--> \"*\" WartungsTicket : -tickets\nRover x--> \"*\" LogEintrag : -logs\n@enduml"
                    },
                    NassiShneiderSource = @"PROGRAM VerarbeiteDatenstrom;

VAR
    eintraege : ARRAY OF STRING;
    teile     : ARRAY OF STRING;
    istKritisch : BOOLEAN;
    i, k      : INTEGER;

BEGIN
    eintraege := Split(stream, '|');
    i := 0;
    
    WHILE i < length(eintraege) DO
    BEGIN
        teile := Split(eintraege[i], '#');
        k := 0;
        
        WHILE k < roverListe.Count DO
        BEGIN
            r := roverListe[k];
            
            IF r.getId() = teile[0] THEN
            BEGIN
                istKritisch := r.verarbeiteStatus(teile[1], teile[2]);
                
                IF istKritisch = TRUE THEN
                BEGIN
                    wartung.erstelleTicket(r.getId(), 'Kritischer Batteriestatus');
                END;
            END;
            k := k + 1;
        END;
        i := i + 1;
    END;
END.",
                    NoUMLAutoScale = true,
                    AuxiliaryIds = new List<string> { "ListT" },
                    Prerequisites = new List<string>
                    {
                        "String Parsing", "Object Interaction", "State Management", "Integer Parsing"
                    }
                }
            };
        }
    }
}