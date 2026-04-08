using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AbiturEliteCode.cs;

public class SqlLevelDraft
{
    public string Name { get; set; } = "Neues SQL Level";
    public string Author { get; set; } = "Unbekannt";
    public string Description { get; set; } = "Beschreibe hier die Aufgabe...";

    public string Materials { get; set; } =
        "Hier können Dokumentation, Tipps oder Hinweise stehen...\n\nstart-hint: Ein Hinweis\nVersteckter Text\n:end-hint";

    public List<string> Prerequisites { get; set; } = new();

    // sql specific fields
    public string SetupScript { get; set; } =
        "CREATE TABLE MeineTabelle (id INTEGER PRIMARY KEY, name TEXT);\nINSERT INTO MeineTabelle VALUES (1, 'Test');";

    public bool IsDmlMode { get; set; }
    public string VerificationQuery { get; set; } = "";
    public string SampleSolution { get; set; } = "SELECT * FROM MeineTabelle;";

    public List<SqlExpectedColumn> ExpectedSchema { get; set; } = new();
    public List<string[]> ExpectedResult { get; set; } = new();

    public bool IsRelationalModelReadOnly { get; set; }
    public List<RTable> InitialRelationalModel { get; set; } = new();

    // single main diagram only (no material auxiliary diagrams)
    public string PlantUmlSource { get; set; } =
        "@startchen\nentity MeineTabelle {\n    id <<key>>\n    name\n}\n@endchen";

    public string PlantUmlSvgContent { get; set; } = "";

    public bool QuickGenerate { get; set; }
}

public static class SqlLevelDesigner
{
    public static SqlLevelDraft LoadDraft(string path)
    {
        if (!File.Exists(path)) return new SqlLevelDraft();
        try
        {
            string json = File.ReadAllText(path);
            var draft = JsonSerializer.Deserialize<SqlLevelDraft>(json);

            if (draft != null)
            {
                if (string.IsNullOrEmpty(draft.PlantUmlSource))
                    draft.PlantUmlSource = "@startchen\nentity MeineTabelle {\n    id <<key>>\n    name\n}\n@endchen";

                // replace commas with periods
                if (draft.ExpectedResult != null)
                    foreach (var row in draft.ExpectedResult)
                        for (int i = 0; i < row.Length; i++)
                            if (row[i] != null)
                                row[i] = row[i].Replace(",", ".");
            }

            return draft ?? new SqlLevelDraft();
        }
        catch
        {
            return new SqlLevelDraft();
        }
    }

    public static async Task SaveDraftAsync(string path, SqlLevelDraft draft)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(draft, options);
        await File.WriteAllTextAsync(path, json);
    }

    public static void ExportLevel(string draftPath, SqlLevelDraft draft, List<SqlExpectedColumn> expectedSchema,
        List<string[]> expectedResult)
    {
        string dir = Path.GetDirectoryName(draftPath);
        string filename = Path.GetFileNameWithoutExtension(draftPath);
        string targetPath = Path.Combine(dir, filename + ".eliteslvl");

        var exportData = new
        {
            Title = draft.Name,
            draft.Author,
            draft.Description,
            MaterialDocs = draft.Materials,
            draft.Prerequisites,
            draft.SetupScript,
            VerificationQuery = draft.IsDmlMode ? draft.VerificationQuery : "",
            ExpectedSchema = expectedSchema,
            ExpectedResult = expectedResult,
            PlantUMLSources = new List<string> { draft.PlantUmlSource },
            DiagramPaths = new List<string> { draft.PlantUmlSvgContent },
            draft.IsRelationalModelReadOnly,
            InitialRelationalModel = draft.IsRelationalModelReadOnly ? draft.InitialRelationalModel : new List<RTable>()
        };

        var options = new JsonSerializerOptions { WriteIndented = false };
        string json = JsonSerializer.Serialize(exportData, options);

        string encryptedJson = LevelEncryption.Encrypt(json);
        File.WriteAllText(targetPath, encryptedJson);
    }
}