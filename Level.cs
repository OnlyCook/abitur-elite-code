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
    // uml diagrams in need of conversion into c# code in the abitur are: uml class diagrams, uml object diagrams, uml sequence diagrams and Nassi–Shneiderman diagrams
    // the further the level progression the less handholding the user will get, although if the user had to implement a class for example which has to be used exactly as is in the next level, may be already implemented to not repeat the exact same thing (if it has changed a solid amout, the user should re-implement it though)
    // getters and setters should match the abiturs scheme of "getVariable()" [so in c# "GetVariable()"], using "{ get; set; }" should be avoided

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

    public static class SharedDiagrams
    {
        public static string ListT = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass \"List<T>\" {\n  + add(item : T) : void\n  + remove(item : T) : void\n  + get(index : int) : T\n  + size() : int\n  + contains(item : T) : boolean\n}\n@enduml";
    }

    public static class Curriculum
    {
        public static List<Level> GetLevels()
        {
            string listDocsHints =
                @"Hinweis: In C# heißt die Klasse ebenfalls [List<T>]
- Java: [list.get(i)] → C#: [list|[i|]] oder [list.ElementAt(i)]
- Java: [list.size()] → C#: [list.Count]
- Java: [boolean] → C#: [bool]
- Listen müssen mit [new List<T>()] initialisiert werden";

            return new List<Level>
            {
                new Level
                {
                    Id = 1,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = "ZOO",
                    NextLevelCode = "CAP",
                    Title = "Klasse Tier Implementieren",
                    Description = "Überführen Sie die Klasse [Tier] aus dem UML-Klassendiagramm in C#-Code.\n\n" +
                                  "Das Diagramm zeigt die Java-Notation. Beachten Sie die Unterschiede zu C#.",
                    StarterCode = "public class Tier\n{\n    // Implementation hier\n}",
                    DiagramPath = "img\\sec1\\lvl1.png",
                    MaterialDocs = "Hinweis: In Java schreibt man [String], in C# [string].\nBeide Sprachen verwenden [int] für Ganzzahlen.\nPrivate Felder werden mit [-], public mit [+] markiert.",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Tier {\n  - name : String\n  - alter : int\n  + Tier(name : String, alter : int)\n}\n@enduml"
                },
                new Level
                {
                    Id = 2,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = "CAP",
                    NextLevelCode = "ABS",
                    Title = "Kapselung und Validierung",
                    Description = "Implementieren Sie Datenkapselung für das Attribut [alter].\n\n" +
                                  "Aufgaben:\n" +
                                  "1. Ergänzen Sie einen Getter [GetAlter()] und einen Setter [SetAlter(int neuesAlter)].\n" +
                                  "2. Der Setter darf das Alter nur ändern, wenn der neue Wert größer als der alte ist.\n" +
                                  "3. Der Getter gibt den aktuellen Wert des Alters zurück.",
                    StarterCode = "public class Tier\n{\n    private int alter = 5;\n    \n    // Implementation hier\n}",
                    DiagramPath = "img\\sec1\\lvl2.png",
                    MaterialDocs = "Hinweis: In Java schreibt man [getAlter()] und [setAlter()]. In C# verwenden wir die gleiche Namenskonvention, jedoch mit großem Anfangsbuchstaben: [GetAlter()] und [SetAlter()].\n\nTipp: Verwenden Sie eine [if]-Bedingung im Setter, um zu prüfen, ob [neuesAlter > alter] ist.",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Tier {\n  - alter : int\n  + setAlter(neuesAlter : int) : void\n  + getAlter() : int\n}\n@enduml"
                },
                new Level
                {
                    Id = 3,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = "ABS",
                    NextLevelCode = "LST",
                    Title = "Abstrakte Klassen und Vererbung",
                    Description = "Implementieren Sie die abstrakte Klasse [Tier] und die abgeleitete Klasse [Loewe].\n\n" +
                                  "Anforderungen:\n" +
                                  "1. [Tier] ist eine abstrakte Klasse mit einem geschützten Attribut [name].\n" +
                                  "2. [Loewe] erbt von [Tier] und implementiert die Methode [Bruellen()].\n" +
                                  "3. Der Konstruktor von [Loewe] ruft den Basis-Konstruktor auf.",
                    StarterCode = "// Implementieren Sie beide Klassen vollständig\n",
                    DiagramPath = "img\\sec1\\lvl3.png",
                    MaterialDocs = "Hinweis: In Java schreibt man [public abstract class Tier], in C# identisch.\nVererbung: Java verwendet [extends], C# verwendet [:].\n\nTipp: Der Konstruktor von Loewe sollte [base(name)] aufrufen, um den Konstruktor der Basisklasse aufzurufen.\n\nTipp: Die Methode [Bruellen()] kann einen beliebigen String zurückgeben, z.B. \"ROAR!\"",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nabstract class Tier {\n  # name : String\n  + Tier(name : String)\n}\nclass Loewe {\n  - laenge : int\n  + Loewe(name : String, laenge : int)\n  + bruellen() : String\n}\nTier <|-- Loewe\n@enduml"
                },
                new Level
                {
                    Id = 4,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = "LST",
                    NextLevelCode = "ALG",
                    Title = "Gehege-Verwaltung mit List",
                    Description = "Erstellen Sie die Klasse [Gehege], die eine Liste von Tieren verwaltet.\n\n" +
                                  "Die Klasse soll folgende Funktionalität bieten:\n" +
                                  "- Einen Konstruktor, der die Liste initialisiert\n" +
                                  "- Eine Methode [Hinzufuegen(Tier t)], die ein Tier zur Liste hinzufügt\n" +
                                  "- Eine Methode [AnzahlTiere()], die die Anzahl der Tiere zurückgibt\n\n" +
                                  "Hinweis: Verwenden Sie für dieses Level eine normale (nicht abstrakte) Klasse Tier.",
                    StarterCode = "// Erstellen Sie die Klassen Gehege und Tier vollständig selbst\n",
                    DiagramPath = "img\\sec1\\lvl4.png",
                    MaterialDocs = listDocsHints,
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Gehege {\n  - bewohner : List<Tier>\n  + Gehege()\n  + hinzufuegen(t : Tier) : void\n  + anzahlTiere() : int\n}\nclass Tier {\n  + Tier()\n}\nGehege \"1\" o-- \"*\" Tier : -verwaltet\n@enduml",
                    AuxiliaryId = "ListT"
                },
                new Level
                {
                    Id = 5,
                    Section = "Sektion 1: OOP Grundlagen",
                    SkipCode = "ALG",
                    NextLevelCode = "FIN",
                    Title = "Algorithmus: Das Älteste Tier",
                    Description = "Implementieren Sie die Methode [ErmittleAeltestes()] in der Klasse [Gehege].\n\n" +
                                  "Die Methode soll:\n" +
                                  "- Das Tier mit dem höchsten Alter zurückgeben\n" +
                                  "- Bei mehreren Tieren mit gleichem Höchstalter das erste gefundene zurückgeben\n" +
                                  "- Bei leerer Liste [null] zurückgeben\n\n" +
                                  "Dies ist das Abschluss-Level von Sektion 1. Es kombiniert alle bisherigen Konzepte.",
                    StarterCode = "public class Tier\n{\n    private int alter;\n    \n    public Tier(int a)\n    {\n        alter = a;\n    }\n    \n    public int GetAlter()\n    {\n        return alter;\n    }\n}\n\npublic class Gehege\n{\n    public List<Tier> bewohner = new List<Tier>();\n    \n    // Implementation hier\n}\n",
                    DiagramPath = "img\\sec1\\lvl5.png",
                    MaterialDocs = listDocsHints + "\n\nTipp: Verwenden Sie eine Schleife, um durch alle Tiere zu iterieren.\n\nTipp: Denken Sie daran, den Rückgabetyp [Tier] zu verwenden. Die Methode muss ein Tier-Objekt oder [null] zurückgeben.\n\nHinweis: Prüfen Sie zuerst, ob die Liste leer ist ([bewohner.Count == 0]), bevor Sie iterieren.",
                    PlantUMLSource = "@startuml\nskinparam classAttributeIconSize 0\nskinparam monochrome true\nclass Gehege {\n  - bewohner : List<Tier>\n  + ermittleAeltestes() : Tier\n}\nclass Tier {\n  - alter : int\n  + getAlter() : int\n}\nGehege \"1\" o-- \"*\" Tier\n@enduml",
                    AuxiliaryId = "ListT"
                }
            };
        }
    }
}