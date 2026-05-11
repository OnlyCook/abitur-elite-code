namespace AbiturEliteCode;

public partial class MainWindow
{
    private class CustomLevelInfo
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public string FilePath { get; set; }
        public string Section { get; set; }
        public bool IsDraft { get; set; } // .elitelvldraft = true; .elitelvl = false
        public bool QuickGenerate { get; set; }
    }
}