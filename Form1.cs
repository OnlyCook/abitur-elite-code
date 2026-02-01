using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace AbiturEliteCode
{
    public partial class Form1 : Form
    {
        private SplitContainer splitContainerMain;
        private TabControl leftTabControl;
        private RichTextBox editorBox;
        private System.Windows.Forms.TextBox consoleBox;
        private System.Windows.Forms.Button runButton;
        private System.Windows.Forms.Button nextButton;
        private System.Windows.Forms.TextBox skipCodeBox;

        private Level currentLevel;
        private List<Level> levels;

        // dark mode
        private Color bgDark = Color.FromArgb(20, 20, 20);
        private Color bgPanel = Color.FromArgb(35, 35, 38);
        private Color fgWhite = Color.FromArgb(220, 220, 220);
        private Color accentGreen = Color.FromArgb(45, 137, 74);
        private Color accentGray = Color.FromArgb(60, 60, 60);

        public Form1()
        {
            InitializeComponent();
            levels = Curriculum.GetLevels();
            InitializeCustomUI();
            LoadLevel(levels[0]);
        }

        private void Form1_Load(object sender, EventArgs e) { }

        private void InitializeCustomUI()
        {
            this.Text = "Abitur Elite Code";
            this.Size = new Size(1250, 850);
            this.BackColor = bgDark;
            this.ForeColor = fgWhite;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            // top nav bar
            Panel navBar = new Panel
            { 
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(15, 15, 15),
                Padding = new Padding(10)
            };

            Label titleLabel = new Label
            {
                Text = "ABITUR ELITE CODE",
                Font = new Font("Segoe UI", 12, FontStyle.Bold), 
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(15, 12)
            };

            Label skipLabel = new Label
            {
                Text = "Level Code:",
                ForeColor = Color.Gray,
                Location = new Point(900, 15),
                AutoSize = true
            };
            skipCodeBox = new System.Windows.Forms.TextBox
            {
                Width = 60,
                Location = new Point(980, 12), 
                MaxLength = 3,
                BackColor = bgPanel,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center
            };
            skipCodeBox.TextChanged += (s, e) => {
                if (skipCodeBox.Text.Length == 3) CheckLevelCode(skipCodeBox.Text);
            };

            nextButton = new System.Windows.Forms.Button
            {
                Text = "NEXT LEVEL →",
                Width = 120, Height = 30,
                Location = new Point(1060, 8),
                Visible = false,
                BackColor = Color.RoyalBlue,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White
            };
            nextButton.Click += (s, e) => LoadLevel(levels[levels.IndexOf(currentLevel) + 1]);

            navBar.Controls.Add(titleLabel);
            navBar.Controls.Add(skipLabel);
            navBar.Controls.Add(skipCodeBox);
            navBar.Controls.Add(nextButton);
            this.Controls.Add(navBar);

            // main layout
            splitContainerMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 450,
                SplitterWidth = 4,
                BackColor = Color.Black
            };
            this.Controls.Add(splitContainerMain);
            splitContainerMain.BringToFront();

            // left side (materials)
            leftTabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };
            leftTabControl.TabPages.Add(CreateTabPage("Aufgabe", "taskDesc"));
            leftTabControl.TabPages.Add(CreateTabPage("UML", "umlBox"));
            leftTabControl.TabPages.Add(CreateTabPage("Materialien", "matBox"));
            splitContainerMain.Panel1.Controls.Add(leftTabControl);

            // right side (c# editor)
            Panel rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = bgDark
            };

            runButton = new System.Windows.Forms.Button
            {
                Text = "COMPILE & RUN",
                Dock = DockStyle.Top,
                Height = 45, BackColor = accentGreen,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            runButton.Click += (s, e) => CompileAndRun();

            editorBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 13),
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.FromArgb(214, 214, 214),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true
            };
            editorBox.KeyDown += EditorBox_KeyDown;
            editorBox.KeyPress += EditorBox_KeyPress;

            consoleBox = new System.Windows.Forms.TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 180,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10)
            };

            rightPanel.Controls.Add(editorBox);
            rightPanel.Controls.Add(consoleBox);
            rightPanel.Controls.Add(runButton);
            splitContainerMain.Panel2.Controls.Add(rightPanel);
        }

        private TabPage CreateTabPage(string title, string controlName)
        {
            TabPage tp = new TabPage(title)
            {
                BackColor = bgPanel
            };
            RichTextBox rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Name = controlName,
                BackColor = bgPanel,
                ForeColor = fgWhite,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11),
                Padding = new Padding(10)
            };
            tp.Controls.Add(rtb);
            return tp;
        }

        private void CheckLevelCode(string code)
        {
            var foundLevel = levels.FirstOrDefault(l => l.SkipCode.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (foundLevel != null)
            {
                LoadLevel(foundLevel);
                skipCodeBox.Clear();
                consoleBox.Text = $"Level {foundLevel.Id} unlocked!";
            }
        }

        private void LoadLevel(Level level)
        {
            currentLevel = level;
            nextButton.Visible = false;

            Control[] taskControls = this.Controls.Find("taskDesc", true);
            if (taskControls.Length > 0) ((RichTextBox)taskControls[0]).Text = $"[LEVEL {level.Id}]\n\n{level.Description}";

            Control[] matControls = this.Controls.Find("matBox", true);
            if (matControls.Length > 0) ((RichTextBox)matControls[0]).Text = level.MaterialDocs;

            editorBox.Text = level.StarterCode;
            consoleBox.Text = "System Ready. Follow the taks and implement the logic.";
        }

        private void EditorBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                editorBox.SelectedText = "    ";
            }
            else if (e.KeyCode == Keys.Back)
            {
                int pos = editorBox.SelectionStart;
                if (pos > 0 && pos < editorBox.Text.Length)
                {
                    char left = editorBox.Text[pos - 1];
                    char right = editorBox.Text[pos];
                    if (IsPair(left, right))
                    {
                        editorBox.SelectionStart = pos;
                        editorBox.SelectionLength = 1;
                        editorBox.SelectedText = "";
                        editorBox.SelectionStart = pos;
                    }
                }
            }
        }

        private void EditorBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            char pair = GetPair(e.KeyChar);
            if (pair != '\0')
            {
                int pos = editorBox.SelectionStart;
                editorBox.SelectedText = e.KeyChar.ToString() + pair;
                editorBox.SelectionStart = pos + 1;
                e.Handled = true;
                return;
            }

            if (e.KeyChar == (char)Keys.Enter)
            {
                int lineIndex = editorBox.GetLineFromCharIndex(editorBox.SelectionStart);
                if (lineIndex > 0)
                {
                    string prevLine = editorBox.Lines[lineIndex - 1];
                    string indent = GetLeadingWhitespace(prevLine);

                    if (prevLine.Trim() == "{") indent += "    ";

                    editorBox.SelectedText = indent;
                }
            }
        }

        private bool IsPair(char l, char r)
        {
            return (l == '(' && r == ')') || (l == '{' && r == '}') ||
                   (l == '[' && r == ']') || (l == '"' && r == '"') || 
                   (l == '\'' && r == '\'');
        }

        private char GetPair(char c)
        {
            if (c == '(') return ')';
            if (c == '{') return '}';
            if (c == '[') return ']';
            if (c == '"') return '"';
            if (c == '\'') return '\'';
            return '\0';
        }

        private string GetLeadingWhitespace(string line)
        {
            string ws = "";
            foreach (char c in line)
            {
                if (c == ' ' || c == '\t') ws += c;
                else break;
            }
            return ws;
        }

        private void CompileAndRun()
        {
            consoleBox.Text = "Compiling...";
            consoleBox.ForeColor = Color.Yellow;

            // wrap user code
            string sourceCode = "";
            if (currentLevel.Id == 1) // class creation task
            {
                sourceCode = "using System; using System.Collections.Generic; " + editorBox.Text;
            }
            else // method implementation task
            {
                sourceCode = @"
                    using System;
                    using System.Collections.Generic;
                    
                    public class UserSubmission
                    {
                        " + editorBox.Text + @"
                    }
                ";
            }

            // parse
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            // references
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"))
            };

            // compile
            CSharpCompilation compilation = CSharpCompilation.Create(
                "UserAssembly",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    consoleBox.ForeColor = Color.Red;
                    consoleBox.Text = "COMPILATION ERRORS:\r\n-------------------\r\n";
                    foreach (Diagnostic diagnostic in result.Diagnostics)
                    {
                        consoleBox.AppendText($"Line {diagnostic.Location.GetLineSpan().StartLinePosition.Line}: {diagnostic.GetMessage()}\r\n");
                    }
                }
                else
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());
                    RunTests(assembly);
                }
            }
        }

        private void RunTests(Assembly assembly)
        {
            try
            {
                string feedback = "";

                if (currentLevel.Id == 1)
                {
                    // level 1 test
                    Type type = assembly.GetType("Patient");
                    if (type == null) throw new Exception("Class 'Patient' was not found. Did you name it correctly?");

                    // try to create instance
                    object instance = Activator.CreateInstance(type, new object[] { "TestName", 123 }) ?? throw new Exception("Constructor failed.");
                    feedback = "SUCCESS: Level 1 Complete! Class Patient created correctly.";
                }
                else if (currentLevel.Id == 2)
                {
                    // level 2 test
                    Type type = assembly.GetType("UserSubmission");
                    object instance = Activator.CreateInstance(type);
                    MethodInfo method = type.GetMethod("IsCritical");
                    if (method == null) throw new Exception("Method 'IsCritical' not found.");

                    bool res1 = (bool)method.Invoke(instance, [6]);
                    if (!res1) throw new Exception("Failed Test 1: Priority 6 IS critical (should return true).");

                    feedback = "SUCCESS: Level 2 Complete! Logic is perfect.";
                }

                consoleBox.ForeColor = Color.LightGreen;
                consoleBox.Text = feedback;
            }
            catch (Exception ex)
            {
                consoleBox.ForeColor = Color.Orange;
                consoleBox.Text = $"LOGIC ERROR:\r\n{ex.InnerException?.Message ?? ex.Message}";
            }
        }
    }
}