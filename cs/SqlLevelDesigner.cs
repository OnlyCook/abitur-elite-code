using AbiturEliteCode.cs;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public class SqlLevelDraft
{
    public string Name { get; set; } = "Neues SQL Level";
    public string Author { get; set; } = "Unbekannt";
    public string Description { get; set; } = "Beschreibe hier die Aufgabe...";
    public string Materials { get; set; } = "Hier können Dokumentation, Tipps oder Hinweise stehen...\n\nstart-hint: Ein Hinweis\nVersteckter Text\n:end-hint";
    public List<string> Prerequisites { get; set; } = new List<string>();

    // sql specific fields
    public string SetupScript { get; set; } = "CREATE TABLE MeineTabelle (id INTEGER PRIMARY KEY, name TEXT);\nINSERT INTO MeineTabelle VALUES (1, 'Test');";
    public bool IsDmlMode { get; set; } = false;
    public string VerificationQuery { get; set; } = "";
    public string SampleSolution { get; set; } = "SELECT * FROM MeineTabelle;";

    public List<SqlExpectedColumn> ExpectedSchema { get; set; } = new List<SqlExpectedColumn>();
    public List<string[]> ExpectedResult { get; set; } = new List<string[]>();

    public bool IsRelationalModelReadOnly { get; set; } = false;
    public List<RTable> InitialRelationalModel { get; set; } = new List<RTable>();

    // single main diagram only (no material auxiliary diagrams)
    public string PlantUmlSource { get; set; } = "@startchen\nentity MeineTabelle {\n    id <<key>>\n    name\n}\n@endchen";
    public string PlantUmlSvgContent { get; set; } = "";

    public bool QuickGenerate { get; set; } = false;
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

            if (draft != null && string.IsNullOrEmpty(draft.PlantUmlSource))
            {
                draft.PlantUmlSource = "@startchen\nentity MeineTabelle {\n    id <<key>>\n    name\n}\n@endchen";
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

    public static void ExportLevel(string draftPath, SqlLevelDraft draft, List<SqlExpectedColumn> expectedSchema, List<string[]> expectedResult)
    {
        string dir = Path.GetDirectoryName(draftPath);
        string filename = Path.GetFileNameWithoutExtension(draftPath);
        string targetPath = Path.Combine(dir, filename + ".eliteslvl");

        var exportData = new
        {
            Title = draft.Name,
            Author = draft.Author,
            Description = draft.Description,
            MaterialDocs = draft.Materials,
            Prerequisites = draft.Prerequisites,
            SetupScript = draft.SetupScript,
            VerificationQuery = draft.IsDmlMode ? draft.VerificationQuery : "",
            ExpectedSchema = expectedSchema,
            ExpectedResult = expectedResult,
            PlantUMLSources = new List<string> { draft.PlantUmlSource },
            DiagramPaths = new List<string> { draft.PlantUmlSvgContent },
            IsRelationalModelReadOnly = draft.IsRelationalModelReadOnly,
            InitialRelationalModel = draft.InitialRelationalModel
        };

        var options = new JsonSerializerOptions { WriteIndented = false };
        string json = JsonSerializer.Serialize(exportData, options);
        File.WriteAllText(targetPath, json);
    }
}