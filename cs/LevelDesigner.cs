using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AbiturEliteCode.cs
{
    public class LevelDraft
    {
        public class DiagramData
        {
            public string Name { get; set; } = "Material Diagramm";
            public string PlantUmlSource { get; set; } = "@startuml\nclass NewMaterial {\n}\n@enduml";
            public string PlantUmlSvgContent { get; set; } = "";
        }

        public string Name { get; set; } = "Neues Level";
        public string Author { get; set; } = "Unbekannt";
        public string Description { get; set; } = "Beschreibe hier die Aufgabe...";
        public string Materials { get; set; } = "Hier können Dokumentation, Tipps oder Hinweise stehen...\n\nstart-hint: Ein Hinweis\nVersteckter Text\n:end-hint";
        public List<string> Prerequisites { get; set; } = new List<string>();
        public string StarterCode { get; set; } = "public class MyClass \n{\n    // Code hier\n}";
        public string ValidationCode { get; set; } = "private static bool ValidateLevel(Assembly assembly, out string feedback)\n{\n    feedback = \"Gut gemacht!\";\n    return true;\n}";
        public string TestCode { get; set; } = "";
        public string PlantUmlSource { get; set; } = "@startuml\nAlice -> Bob: Hello\n@enduml";
        public string PlantUmlSvgContent { get; set; } = "";
        public List<DiagramData> MaterialDiagrams { get; set; } = new List<DiagramData>();
        public bool QuickGenerate { get; set; } = false;
    }

    public static class LevelDesigner
    {
        public static LevelDraft LoadDraft(string path)
        {
            if (!File.Exists(path)) return new LevelDraft();
            try
            {
                string json = File.ReadAllText(path);
                var draft = JsonSerializer.Deserialize<LevelDraft>(json);
                return draft ?? new LevelDraft();
            }
            catch
            {
                return new LevelDraft();
            }
        }

        public static async Task SaveDraftAsync(string path, LevelDraft draft)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(draft, options);
            await File.WriteAllTextAsync(path, json);
        }

        public static void ExportLevel(string draftPath, LevelDraft draft)
        {
            string dir = Path.GetDirectoryName(draftPath);
            string filename = Path.GetFileNameWithoutExtension(draftPath);
            string targetPath = Path.Combine(dir, filename + ".elitelvl");

            var exportData = new
            {
                Name = draft.Name,
                Author = draft.Author,
                Description = draft.Description,
                MaterialDocs = draft.Materials,
                Prerequisites = draft.Prerequisites,
                StarterCode = draft.StarterCode,
                ValidationCode = draft.ValidationCode,
                PlantUmlSvg = draft.PlantUmlSvgContent,
                MaterialDiagramSvgs = draft.MaterialDiagrams.Select(d => d.PlantUmlSvgContent).ToList()
            };

            var options = new JsonSerializerOptions { WriteIndented = false };
            string json = JsonSerializer.Serialize(exportData, options);
            File.WriteAllText(targetPath, json);
        }
    }

    public static class PlantUmlHelper
    {
        private static readonly string _plantUmlAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

        public static async Task<string> GenerateSvgFromCodeAsync(string plantUmlCode)
        {
            string encoded = EncodePlantUml(plantUmlCode);
            string url = $"http://www.plantuml.com/plantuml/dsvg/{encoded}";

            using (var client = new HttpClient())
            {
                return await client.GetStringAsync(url);
            }
        }

        private static string EncodePlantUml(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            using (var output = new MemoryStream())
            {
                using (var compressor = new DeflateStream(output, CompressionLevel.Optimal))
                {
                    compressor.Write(bytes, 0, bytes.Length);
                }
                bytes = output.ToArray();
            }

            return Encode64(bytes);
        }

        private static string Encode64(byte[] data)
        {
            StringBuilder r = new StringBuilder();
            int i = 0;
            while (i < data.Length)
            {
                byte b1 = data[i++];
                byte b2 = (i < data.Length) ? data[i++] : (byte)0;
                byte b3 = (i < data.Length) ? data[i++] : (byte)0;

                r.Append(Append3Bytes(b1, b2, b3));
            }
            return r.ToString();
        }

        private static string Append3Bytes(byte b1, byte b2, byte b3)
        {
            int c1 = b1 >> 2;
            int c2 = ((b1 & 0x3) << 4) | (b2 >> 4);
            int c3 = ((b2 & 0xF) << 2) | (b3 >> 6);
            int c4 = b3 & 0x3F;

            return "" + _plantUmlAlphabet[c1 & 0x3F] +
                        _plantUmlAlphabet[c2 & 0x3F] +
                        _plantUmlAlphabet[c3 & 0x3F] +
                        _plantUmlAlphabet[c4 & 0x3F];
        }
    }
}