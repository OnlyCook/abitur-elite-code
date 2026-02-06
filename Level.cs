using System.Collections.Generic;

namespace AbiturEliteCode
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
    // in the abitur basically all classes have a constructor which should be represented in the uml class diagram is given
    // note: adequate abitur level language should be used as well as technical vocabulary

    // for sequence diagrams: keeping the exact given order of calls is important
    // null/out of bounds checks should be included by the user (in the abitur these commonly make the students lose points)
    // pseudo-code is not attached to the uml/diagrams but the materials tab as plain text

    // if the plantuml should actually add a new line instead of converting it by the python script that reads the plantuml source code, then use: "\n/"
    // note: in the abitur association attributes arent included in the actual class, but are indicated on the association only (this also includes its access modifiers: +/-/#), the attribute should be placed on the side of the class its referencing (and not on the side of the class which is referencing it), lists are marked with an asterisk and single attributes with a number (multiplicity)
    // if a class does not have a reference to the other (at all) it should be marked using an X on the associating arrow (note: if there is an X that means this side should not have a multiplicity as there is no reference ot it)
    // note: if a method is returning 'void' it shouldnt be marked (as its like this in the abitur) [this contraint is exluded from auxiliary class diagrams]

    public class Level
    {
        public int Id { get; set; }
        public string Section { get; set; }
        public string SkipCode { get; set; }
        public string NextLevelCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string StarterCode { get; set; }
        public string DiagramPath { get; set; }
        public string MaterialDocs { get; set; }
        public string PlantUMLSource { get; set; }
        public string AuxiliaryId { get; set; }
    }

    public static class LevelCodes
    {
        public static string[] CodesList = {
            "ZOO", "CAP", "ABS", "LST", "ALG",
            "LOG", "INV", "SRT", "LNK", "EXP",
        };
    }

    public static class SharedDiagrams
    {
        public static string ListT = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass \"List<T>\" {\n  + add(item : T) : void\n  + remove(item : T) : void\n  + get(index : int) : T\n  + size() : int\n  + contains(item : T) : boolean\n}\n@enduml";

        public static string Paket = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Paket {\n  - gewicht : double\n  - zielort : String\n  + Paket(ziel : String, gew : double)\n  + getGewicht() : double\n  + getZielort() : String\n}\n@enduml";
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
                _ => ""
            };
        }
    }

    public static class Curriculum
    {
        public static List<Level> GetLevels()
        {
            string listDocsHints =
                @"Hinweis: In C# heißt die Klasse ebenfalls [List<T>]
• Java: [list.add(item)] → C#: [list.Add(item)] usw.
• Java: [list.get(i)] → C#: [list|[i|]] oder [list.ElementAt(i)]
• Java: [list.size()] → C#: [list.Count]
• Java: [boolean] → C#: [bool]
• Listen müssen mit [new List<T>()] initialisiert werden";

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
                    DiagramPath = "img\\sec1\\lvl1.svg",
                    MaterialDocs = "start-hint: Datentypen & Modifier\n" +
                                   "In Java schreibt man [String], in C# [string].\n" +
                                   "Beide Sprachen verwenden [int] für Ganzzahlen.\n" +
                                   "Private Felder werden mit [-], public mit [+] markiert.\n" +
                                   ":end-hint",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Tier {\n  - name : String\n  - alter : int\n  + Tier(name : String, alter : int)\n}\n@enduml"
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
                    DiagramPath = "img\\sec1\\lvl2.svg",
                    MaterialDocs = "start-hint: Namenskonventionen\n" +
                                   "In Java schreibt man [getAlter()] und [setAlter()]. In C# verwenden wir die gleiche Namenskonvention, jedoch mit großem Anfangsbuchstaben: [GetAlter()] und [SetAlter()].\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Logik im Setter\n" +
                                   "Verwenden Sie eine [if]-Bedingung im Setter, um zu prüfen, ob [neuesAlter > alter] ist.\n" +
                                   ":end-hint",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Tier {\n  - alter : int\n  + setAlter(neuesAlter : int)\n  + getAlter() : int\n}\n@enduml"
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
                    DiagramPath = "img\\sec1\\lvl3.svg",
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
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nabstract class Tier {\n  # name : String\n  + Tier(name : String)\n}\nclass Loewe {\n  - laenge : int\n  + Loewe(name : String, laenge : int)\n  + bruellen() : String\n}\nTier <|-- Loewe\n@enduml"
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
                    DiagramPath = "img\\sec1\\lvl4.svg",
                    MaterialDocs = listDocsHints,
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Gehege {\n  + Gehege()\n  + hinzufuegen(t : Tier)\n  + anzahlTiere() : int\n}\nclass Tier {\n  + Tier()\n}\nGehege x--> \"*\" Tier : -bewohner\n@enduml",
                    AuxiliaryId = "ListT"
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
                    StarterCode = "public class Tier\n{\n    private int alter;\n    \n    public Tier(int alter)\n    {\n        this.alter = alter;\n    }\n    \n    public int GetAlter()\n    {\n        return alter;\n    }\n}\n\npublic class Gehege\n{\n    private List<Tier> bewohner = new List<Tier>();\n    \n    // Implementation hier\n}\n",
                    DiagramPath = "img\\sec1\\lvl5.svg",
                    MaterialDocs = listDocsHints + "\n\n" +
                                   "start-tipp: Strategie für die Suche\n" +
                                   "1. Erstellen Sie vor der Schleife eine Variable [Tier aeltestes = null;].\n" +
                                   "2. Iterieren Sie durch alle Tiere ([foreach]).\n" +
                                   "3. Prüfen Sie: Ist [aeltestes] noch null? ODER ist das aktuelle Tier älter als [aeltestes]?\n" +
                                   "4. Wenn ja: Setzen Sie [aeltestes] auf das aktuelle Tier.\n" +
                                   "5. Geben Sie am Ende [aeltestes] zurück.\n" +
                                   ":end-hint",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Gehege {\n  + ermittleAeltestes() : Tier\n}\nclass Tier {\n  - alter : int\n  + getAlter() : int\n}\nGehege x--> \"*\" Tier : -bewohner\n@enduml",
                    AuxiliaryId = "ListT"
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
                    StarterCode = "public class Paket\n{\n    // Attribute, Konstruktor, Getter\n}\n\npublic class Lager\n{\n    // Liste und Methoden\n}",
                    DiagramPath = "img\\sec2\\lvl6.svg",
                    MaterialDocs = listDocsHints + "\n\n" +
                                   "start-tipp: Startwert für Suche\n" +
                                   "Nutzen Sie eine Variable [leichtestesPaket], die Sie initial auf das erste Element der Liste setzen (falls vorhanden) oder auf null.\n" +
                                   ":end-hint",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Lager {\n  + Lager()\n  + hinzufuegen(p : Paket)\n  + ermittleLeichtestes() : Paket\n}\nclass Paket {\n  - gewicht : double\n  - zielort : String\n  + Paket(ziel : String, gew : double)\n  + getGewicht() : double\n}\nLager x--> \"*\" Paket : -pakete\n@enduml",
                    AuxiliaryId = "ListT"
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
                    StarterCode = "public class Lager\n{\n    private List<Paket> pakete = new List<Paket>();\n\n    public List<Paket> FilterePakete(string ort)\n    {\n        // Implementation hier\n        return null;\n    }\n}",
                    DiagramPath = "img\\sec2\\lvl7.svg",
                    MaterialDocs = "start-hint: C# Operatoren\n" +
                                   "Strings vergleicht man in C# mit [==] oder [Equals()].\n" +
                                   "[&&] ist der Operator für das logische UND.\n" +
                                   ":end-hint",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Lager {\n  + hinzufuegen(Paket p)\n  + filterePakete(ort : String) : List<Paket>\n}\nLager x--> \"*\" Paket : -pakete\n@enduml",
                    AuxiliaryId = "Paket"
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
                    DiagramPath = "img\\sec2\\lvl8.svg",
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
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Lager {\n  + sortiere()\n}\nnote right: Sortierung nach Gewicht (aufsteigend)\nLager x--> \"*\" Paket : -pakete\n@enduml",
                    AuxiliaryId = "Paket"
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
                    StarterCode = "public class Knoten\n{\n    // Implementation\n}\n\npublic class Foerderband\n{\n    private Knoten kopf;\n\n    public void Anhaengen(Paket p)\n    {\n        // Implementation\n    }\n}",
                    DiagramPath = "img\\sec2\\lvl9.svg",
                    MaterialDocs = "start-hint: Verkettete Listen\n" +
                                   "Verkettete Listen sind ein Klassiker im Abitur!\n\n" +
                                   ":end-hint\n" +
                                   "start-tipp: Traversierung\n" +
                                   "Traversierung (zum Ende laufen) in Java:\n" +
                                   "{|Knoten aktuell = kopf;\n" +
                                   "while(aktuell.getNachfolger() != null) {\n" +
                                   "   aktuell = aktuell.getNachfolger();\n" +
                                   "}|}\n" +
                                   ":end-hint",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Knoten {\n  + Knoten(p : Paket)\n  + getNachfolger() : Knoten\n  + setNachfolger(k : Knoten)\n}\nclass Foerderband {\n  + anhaengen(p : Paket)\n}\nFoerderband x--> \"0..1\" Knoten : -kopf\nKnoten x--> \"0..1\" Knoten : -nachfolger\nKnoten x--> \"1\" Paket : -inhalt\n@enduml",
                    AuxiliaryId = "Paket"
                },
                new Level
                {
                    Id = 10,
                    Section = "Sektion 2: Datenstrukturen & Algorithmen",
                    SkipCode = LevelCodes.CodesList[9],
                    NextLevelCode = "",
                    Title = "Die Express-Lieferung",
                    Description = "Dies ist die Abschlussprüfung für Sektion 2.\n\n" +
                                  "Aufgabe:\n" +
                                  "Implementieren Sie die Methode [GetTop3Schwere(String ort)].\n" +
                                  "Die Methode soll die **3 schwersten Pakete** für einen Zielort zurückgeben, sortiert nach Gewicht (absteigend).\n\n" +
                                  "Anforderungen:\n" +
                                  "• Filtern nach Ort\n" +
                                  "• Sortieren nach Gewicht (Absteigend)\n" +
                                  "• Maximal 3 Elemente zurückgeben.",
                    StarterCode = "public class LogistikZentrum\n{\n    private List<Paket> allePakete = new List<Paket>();\n\n    public List<Paket> GetTop3Schwere(string ort)\n    {\n        // Viel Erfolg!\n        return null;\n    }\n}",
                    DiagramPath = "img\\sec2\\lvl10.svg",
                    MaterialDocs = "start-tipp: Transferleistung\n" +
                                   "Sie können Ihre Bubble-Sort Logik wiederverwenden/anpassen oder (da dies eine Transferleistung ist) Hilfslisten nutzen.\n" +
                                   ":end-hint\n" +
                                   "start-hint: Unzureichend Pakete\n" +
                                   "Beachten Sie: Wenn weniger als 3 Pakete existieren, geben Sie einfach alle gefundenen zurück.\n" +
                                   ":end-hint",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass LogistikZentrum {\n  + getTop3Schwere(ort : String) : List<Paket>\n}\nLogistikZentrum x--> \"*\" Paket : -allePakete\n@enduml",
                    AuxiliaryId = "Paket"
                }
            };
        }
    }
}