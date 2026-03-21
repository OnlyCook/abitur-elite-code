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
    public string VerificationQuery { get; set; } = ""; // for update/insert/delete validation
    public string ExpectedQuery { get; set; } = "SELECT * FROM MeineTabelle;"; // used to generate expectedschema and expectedresult during verification

    // single main diagram only (no material auxiliary diagrams)
    public string PlantUmlSource { get; set; } = "@startchen\nentity MeineTabelle {\n    id <<key>>\n    name\n}\n@endchen";
    public string PlantUmlSvgContent { get; set; } = "";
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
        string targetPath = Path.Combine(dir, filename + ".eliteslvl"); // exported sql level

        var exportData = new
        {
            Title = draft.Name,
            Description = $"Autor: {draft.Author}\n\n{draft.Description}",
            MaterialDocs = draft.Materials,
            Prerequisites = draft.Prerequisites,
            SetupScript = draft.SetupScript,
            VerificationQuery = draft.VerificationQuery,
            ExpectedSchema = expectedSchema,
            ExpectedResult = expectedResult,
            PlantUMLSources = new List<string> { draft.PlantUmlSource },
            DiagramPaths = new List<string> { draft.PlantUmlSvgContent },
            IsRelationalModelReadOnly = false,
            InitialRelationalModel = new List<RTable>()
        };

        var options = new JsonSerializerOptions { WriteIndented = false };
        string json = JsonSerializer.Serialize(exportData, options);
        File.WriteAllText(targetPath, json);
    }
}