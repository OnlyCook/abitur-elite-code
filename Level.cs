using System.Collections.Generic;

namespace AbiturEliteCode
{
    public class Level
    {
        public int Id { get; set; }
        public string SkipCode { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string StarterCode { get; set; }
        public string DiagramPath { get; set; }
        public string MaterialDocs { get; set; }
    }

    public static class Curriculum
    {
        public static List<Level> GetLevels()
        {
            return new List<Level>
            {
                new Level
                {
                    Id = 1,
                    Title = "Klasse Patient Erstellen",
                    Description = "Basierend auf Abitur 2025:\nErstellen Sie die Klasse 'Patient' mit den privaten Attributen:\n- name (String)\n- nummer (int)\n\nErstellen Sie einen Konstruktor, der beide Werte initialisiert.",
                    StarterCode = "public class Patient\n{\n    \n}",
                    MaterialDocs = "Abitur 2025 - Anlage 1:\nKlasse Patient hält Stammdaten..."
                },
                new Level
                {
                    Id = 2,
                    Title = "Priorisierte Operationen",
                    Description = "Implementieren Sie die Methode 'IsCritical'.\nSie soll true zurückgeben, wenn die Priorität größer als 5 ist, sonst false.",
                    StarterCode = "public bool IsCritical(int prioritaet) {\n    \n}",
                    MaterialDocs = "Abitur 2022 - Material 5:\nList<T> Dokumentation..."
                }
            };
        }
    }
}