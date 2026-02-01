using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AbiturEliteCode
{
    public partial class Form1 : Form
    {
        private SplitContainer splitContainerMain;
        private TabControl leftTabControl;
        private Panel editorPanel;
        private RichTextBox lineNumberBox;
        private RichTextBox editorBox;
        private TextBox consoleBox;
        private Button runButton;
        private Button nextButton;
        private TextBox skipCodeBox;
        private Button levelSelectButton;

        private PlayerData playerData;
        private Level currentLevel;
        private List<Level> levels;

        private System.Windows.Forms.Timer autoSaveTimer;

        // dark mode colors
        private Color bgDark = Color.FromArgb(18, 18, 18);
        private Color bgPanel = Color.FromArgb(32, 33, 36);
        private Color fgWhite = Color.FromArgb(230, 230, 230);
        private Color accentGreen = Color.FromArgb(50, 168, 82);
        private Color borderGray = Color.FromArgb(60, 60, 60);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private void EnableDarkTitleBar()
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                int preference = 1;
                int result = DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref preference, sizeof(int));

                if (result != 0) // fallback try older verison
                {
                    DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref preference, sizeof(int));
                }
            }
        }

        public Form1()
        {
            InitializeComponent();

            EnableDarkTitleBar();

            levels = Curriculum.GetLevels();
            playerData = SaveSystem.Load();

            // autosave init
            autoSaveTimer = new();
            autoSaveTimer.Interval = 2000; // 2 seconds
            autoSaveTimer.Tick += AutoSaveTimer_Tick;

            InitializeCustomUI();

            // load highest unlocked level (or level 1 as fallback)
            int maxId = playerData.UnlockedLevelIds.Max();
            LoadLevel(levels.FirstOrDefault(l => l.Id == maxId) ?? levels[0]);
        }

        private void Form1_Load(object sender, EventArgs e) { }

        private void InitializeCustomUI()
        {
            this.Text = "Abitur Elite Code";
            this.Size = new Size(1250, 850);
            this.BackColor = bgDark;
            this.ForeColor = fgWhite;

            // save on close
            this.FormClosing += (s, e) => SaveCurrentProgress();

            // --- TOP NAV BAR ---
            Panel navBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = bgPanel,
                Padding = new Padding(20)
            };

            Label titleLabel = new Label
            {
                Text = "ABITUR ELITE CODE",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = accentGreen,
                Location = new Point(20, 18),
                AutoSize = true,
            };

            // calculate X positions based on title
            int startX = 220;

            levelSelectButton = new Button
            {
                Text = "LEVEL WÄHLEN",
                Width = 150,
                Height = 32,
                Location = new Point(startX, 14),
                BackColor = Color.FromArgb(45, 45, 48),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            levelSelectButton.FlatAppearance.BorderSize = 0; // Cleaner look
            levelSelectButton.Click += ShowLevelSelector;
            navBar.Controls.Add(levelSelectButton);

            int codeLabelX = levelSelectButton.Location.X + levelSelectButton.Width + 30;

            Label skipLabel = new Label
            {
                Text = "Level Code:",
                ForeColor = Color.Gray,
                Location = new Point(codeLabelX, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            skipCodeBox = new TextBox
            {
                Width = 60,
                Location = new Point(codeLabelX + 85, 17),
                MaxLength = 3,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center,
                CharacterCasing = CharacterCasing.Upper,
                Font = new Font("Consolas", 11)
            };
            skipCodeBox.TextChanged += (s, e) =>
            {
                if (skipCodeBox.Text.Length == 3) CheckLevelCode(skipCodeBox.Text);
            };

            nextButton = new Button
            {
                Text = "NÄCHSTES LEVEL →",
                Width = 160,
                Height = 32,
                Location = new Point(1050, 14),
                Visible = false,
                BackColor = Color.RoyalBlue,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            nextButton.FlatAppearance.BorderSize = 0;
            nextButton.Click += (s, e) => LoadNextLevel();

            navBar.Controls.Add(titleLabel);
            navBar.Controls.Add(skipLabel);
            navBar.Controls.Add(skipCodeBox);
            navBar.Controls.Add(nextButton);
            this.Controls.Add(navBar);

            // --- MAIN SPLIT CONTAINER ---
            splitContainerMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 4,
                BackColor = bgPanel
            };
            this.Controls.Add(splitContainerMain);
            splitContainerMain.BringToFront();

            this.Load += (s, e) =>
            {
                splitContainerMain.SplitterDistance = splitContainerMain.Width / 2;
            };

            // --- LEFT SIDE (Tabs) ---
            leftTabControl = new EliteTabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                BackColor = bgPanel
            };

            // task tab
            TabPage tabTask = new TabPage("Aufgabe");
            tabTask.BackColor = bgDark;
            RichTextBox taskBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = bgDark,
                ForeColor = fgWhite,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(10),
                Text = "Wähle ein Level aus, um zu beginnen."
            };
            tabTask.Controls.Add(taskBox);

            // uml tab
            TabPage tabUml = new TabPage("UML/Diagramme");
            tabUml.BackColor = bgDark;
            Panel imgScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = bgDark
            };
            PictureBox umlBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.AutoSize
            };
            imgScrollPanel.Controls.Add(umlBox);
            tabUml.Controls.Add(imgScrollPanel);

            // materials tab
            TabPage tabMat = new TabPage("Materialien");
            tabMat.BackColor = bgDark;
            FlowLayoutPanel matFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = bgDark,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            tabMat.Controls.Add(matFlow);

            leftTabControl.TabPages.Add(tabTask);
            leftTabControl.TabPages.Add(tabUml);
            leftTabControl.TabPages.Add(tabMat);

            splitContainerMain.Panel1.Controls.Add(leftTabControl);
            ApplyDarkScrollbars(leftTabControl);

            // --- RIGHT SIDE (editor) ---
            Panel rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = bgDark
            };

            // button container
            Panel buttonContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = bgDark,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(0, 0, 0, 10)
            };

            // reset button
            Button resetButton = new Button
            {
                Text = "↺",
                Width = 50,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            resetButton.FlatAppearance.BorderSize = 0;
            resetButton.Click += (s, e) => ResetLevelCode();

            // gap
            Panel gap2 = new Panel { Width = 10, Dock = DockStyle.Right, BackColor = bgDark };

            // save button
            Button saveButton = new Button
            {
                Text = "💾",
                Width = 60,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(70, 70, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12),
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += (s, e) => ManualSave();

            // another gap
            Panel gap1 = new Panel
            {
                Width = 10,
                Dock = DockStyle.Right,
                BackColor = bgDark
            };

            // run button
            runButton = new Button
            {
                Text = "▶ AUSFÜHREN",
                Dock = DockStyle.Fill,
                BackColor = accentGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            runButton.FlatAppearance.BorderSize = 0;
            runButton.Click += (s, e) => CompileAndRun();

            buttonContainer.Controls.Add(runButton);
            buttonContainer.Controls.Add(gap1);
            buttonContainer.Controls.Add(saveButton);
            buttonContainer.Controls.Add(gap2);
            buttonContainer.Controls.Add(resetButton);

            // console box
            consoleBox = new System.Windows.Forms.TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 180,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LightGray,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10),
                ScrollBars = ScrollBars.Vertical
            };

            // editor panel
            editorPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 25)
            };

            lineNumberBox = new RichTextBox
            {
                Dock = DockStyle.Left,
                Width = 40,
                Font = new Font("Consolas", 13),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(100, 100, 100),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.None
            };

            editorBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 13),
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.FromArgb(214, 214, 214),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true,
                WordWrap = false
            };
            editorBox.KeyDown += EditorBox_KeyDown;
            editorBox.KeyPress += EditorBox_KeyPress;
            editorBox.TextChanged += (s, e) => {
                UpdateLineNumbers(s, e);
                autoSaveTimer.Stop();
                autoSaveTimer.Start();
            };
            editorBox.VScroll += SyncLineNumberScroll;

            editorPanel.Controls.Add(editorBox);
            editorPanel.Controls.Add(lineNumberBox);

            rightPanel.Controls.Add(buttonContainer);
            rightPanel.Controls.Add(consoleBox);
            rightPanel.Controls.Add(editorPanel);

            editorPanel.BringToFront();

            ApplyDarkScrollbars(editorBox);
            ApplyDarkScrollbars(lineNumberBox);
            ApplyDarkScrollbars(consoleBox);

            foreach (TabPage page in leftTabControl.TabPages)
            {
                foreach (Control c in page.Controls) ApplyDarkScrollbars(c);
            }

            splitContainerMain.Panel2.Controls.Add(rightPanel);
        }

        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            autoSaveTimer.Stop();
            SaveCurrentProgress();
        }

        private void SaveCurrentProgress()
        {
            if (currentLevel != null)
            {
                playerData.UserCode[currentLevel.Id] = editorBox.Text;
                SaveSystem.Save(playerData);
            }
        }

        private void UpdateLineNumbers(object sender, EventArgs e)
        {
            int lineCount = editorBox.Lines.Length;
            if (lineNumberBox.Lines.Length != lineCount)
            {
                string lineNumbers = "";
                for (int i = 1; i <= lineCount; i++)
                {
                    lineNumbers += i.ToString() + "\n";
                }
                lineNumberBox.Text = lineNumbers.TrimEnd('\n');
            }
        }

        private void SyncLineNumberScroll(object sender, EventArgs e)
        {
            int scrollPos = GetScrollPos(editorBox.Handle, 1);
            SendMessage(lineNumberBox.Handle, 0x115, 4 + 0x10000 * scrollPos, 0);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetScrollPos(IntPtr hWnd, int nBar);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [System.Runtime.InteropServices.DllImport("uxtheme.dll", ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        private void ApplyDarkScrollbars(Control control)
        {
            SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        }

        private string GetIndentString(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= editorBox.Lines.Length) return "";

            // get the start index of the line
            int lineStart = editorBox.GetFirstCharIndexFromLine(lineIndex);
            if (lineStart < 0) return "";

            // determine line end
            int nextLineStart = editorBox.GetFirstCharIndexFromLine(lineIndex + 1);
            if (nextLineStart == -1) nextLineStart = editorBox.TextLength;

            string lineText = editorBox.Text.Substring(lineStart, nextLineStart - lineStart);

            string indent = "";
            foreach (char c in lineText)
            {
                if (c == ' ' || c == '\t') indent += c;
                else break;
            }
            return indent;
        }

        private void EditorBox_KeyDown(object sender, KeyEventArgs e)
        {
            // --- TAB HANDLING ---
            if (e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                if (editorBox.SelectionLength > 0)
                {
                    // block indentation logic
                    int startLine = editorBox.GetLineFromCharIndex(editorBox.SelectionStart);
                    int endLine = editorBox.GetLineFromCharIndex(editorBox.SelectionStart + editorBox.SelectionLength - 1);

                    // sanity check to ensure we cover the full selection
                    if (endLine < startLine) endLine = startLine;

                    editorBox.SuspendLayout();
                    int start = editorBox.SelectionStart;
                    int length = editorBox.SelectionLength;

                    // iterate lines and add/remove indentation
                    for (int i = startLine; i <= endLine; i++)
                    {
                        int lineStart = editorBox.GetFirstCharIndexFromLine(i);
                        if (e.Shift) // un-indent
                        {
                            if (editorBox.Text.Length > lineStart + 4 && editorBox.Text.Substring(lineStart, 4) == "    ")
                            {
                                editorBox.Select(lineStart, 4);
                                editorBox.SelectedText = "";
                                length -= 4;
                            }
                            else if (editorBox.Text.Length > lineStart && editorBox.Text[lineStart] == '\t')
                            {
                                editorBox.Select(lineStart, 1);
                                editorBox.SelectedText = "";
                                length -= 1;
                            }
                        }
                        else // indent
                        {
                            editorBox.Select(lineStart, 0);
                            editorBox.SelectedText = "    ";
                            length += 4;
                        }
                    }
                    editorBox.Select(start, length);
                    editorBox.ResumeLayout();
                }
                else
                {
                    // regular tab (4 spaces)
                    int start = editorBox.SelectionStart;
                    editorBox.SelectedText = "    ";
                }
                return;
            }

            // --- ENTER HANDLING ---
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                int currentLineIndex = editorBox.GetLineFromCharIndex(editorBox.SelectionStart);
                string currentIndent = GetIndentString(currentLineIndex);

                int caretPos = editorBox.SelectionStart;

                // check for block expansion: {|
                char prevChar = caretPos > 0 ? editorBox.Text[caretPos - 1] : '\0';
                char nextChar = caretPos < editorBox.Text.Length ? editorBox.Text[caretPos] : '\0';

                if (prevChar == '{' && nextChar == '}')
                {
                    string expansion = "\n" + currentIndent + "    \n" + currentIndent;
                    editorBox.SelectedText = expansion;
                    editorBox.SelectionStart = caretPos + currentIndent.Length + 5; // position caret inside
                }
                else
                {
                    editorBox.SelectedText = "\n" + currentIndent;
                }

                editorBox.ScrollToCaret();
                return;
            }

            // --- BACKSPACE HANDLING ---
            if (e.KeyCode == Keys.Back)
            {
                int caretPos = editorBox.SelectionStart;
                if (editorBox.SelectionLength == 0 && caretPos > 0)
                {
                    // pair removal
                    char prevChar = editorBox.Text[caretPos - 1];
                    char nextChar = caretPos < editorBox.Text.Length ? editorBox.Text[caretPos] : '\0';

                    bool isPair = (prevChar == '(' && nextChar == ')') ||
                                  (prevChar == '{' && nextChar == '}') ||
                                  (prevChar == '[' && nextChar == ']') ||
                                  (prevChar == '"' && nextChar == '"') ||
                                  (prevChar == '\'' && nextChar == '\'');

                    if (isPair)
                    {
                        e.SuppressKeyPress = true;
                        editorBox.Select(caretPos - 1, 2);
                        editorBox.SelectedText = "";
                        return;
                    }

                    // whole tab deletion
                    if (caretPos >= 4)
                    {
                        string textToCheck = editorBox.Text.Substring(caretPos - 4, 4);
                        if (textToCheck == "    ")
                        {
                            e.SuppressKeyPress = true;
                            editorBox.Select(caretPos - 4, 4);
                            editorBox.SelectedText = "";
                            return;
                        }
                    }
                }
            }
        }

        private void EditorBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            var pairs = new Dictionary<char, char>
            {
                { '(', ')' },
                { '{', '}' },
                { '[', ']' },
                { '"', '"' },
                { '\'', '\'' }
            };

            if (pairs.ContainsKey(e.KeyChar))
            {
                e.Handled = true;
                char closeChar = pairs[e.KeyChar];

                // insert pair
                string pair = e.KeyChar.ToString() + closeChar.ToString();
                int currentPos = editorBox.SelectionStart;

                editorBox.SelectedText = pair;

                // nove cursor to middle
                editorBox.SelectionStart = currentPos + 1;
            }

            // handle closing brackets behavior
            if (e.KeyChar == ')' || e.KeyChar == '}' || e.KeyChar == ']' || e.KeyChar == '"' || e.KeyChar == '\'')
            {
                int caretPos = editorBox.SelectionStart;

                // check if next is same as trying to type
                if (caretPos < editorBox.TextLength && editorBox.Text[caretPos] == e.KeyChar)
                {
                    // move caret forward
                    editorBox.SelectionStart = caretPos + 1;
                    editorBox.SelectionLength = 0;

                    e.Handled = true;
                }
            }
        }

        private void ResizeMaterials(FlowLayoutPanel matFlow)
        {
            int width = matFlow.ClientSize.Width;
            if (width <= 0 && matFlow.Parent != null) width = matFlow.Parent.Width;
            if (width <= 0) return;

            int targetWidth = width - 25; // scrollbar margin

            matFlow.SuspendLayout();
            foreach (Control ctrl in matFlow.Controls)
            {
                if (ctrl is RichTextBox || ctrl is Button || ctrl is PictureBox)
                {
                    ctrl.Width = targetWidth;
                }
            }
            matFlow.ResumeLayout();
        }

        private void LoadLevel(Level level)
        {
            SaveCurrentProgress();
            currentLevel = level;
            nextButton.Visible = false;

            string rawCode = playerData.UserCode.ContainsKey(level.Id) ? playerData.UserCode[level.Id] : level.StarterCode;

            editorBox.Text = rawCode.Replace("\n", Environment.NewLine);

            UpdateLineNumbers(null, null);

            // --- TASK DESCRIPTION ---
            TabPage taskPage = leftTabControl.TabPages[0];
            taskPage.Controls.Clear(); // clear placeholder content
            Panel taskContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = bgPanel,
                Padding = new Padding(10)
            };
            taskPage.Controls.Add(taskContainer);

            RichTextBox taskBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = bgPanel,
                ForeColor = fgWhite,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Padding = new Padding(10)
            };
            taskContainer.Controls.Add(taskBox);
            ApplyDarkScrollbars(taskBox);

            taskBox.Clear();
            taskBox.SelectionFont = new Font("Segoe UI", 14, FontStyle.Bold);
            taskBox.SelectionColor = fgWhite;
            taskBox.AppendText($"{level.Id}. {level.Title}\n\n");
            AppendFormattedText(taskBox, level.Description);

            // --- MAIN UML DIAGRAM ---
            TabPage umlPage = leftTabControl.TabPages[1];
            umlPage.Controls.Clear();

            Panel imgScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = bgPanel,
                Padding = new Padding(10)
            };
            umlPage.Controls.Add(imgScrollPanel);

            if (!string.IsNullOrEmpty(level.DiagramPath) && File.Exists(level.DiagramPath))
            {
                Image originalImg = Image.FromFile(level.DiagramPath);
                PictureBox pic = new PictureBox
                {
                    Image = originalImg,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = bgPanel,
                    Location = new Point(10, 10) // offset by padding
                };
                imgScrollPanel.Controls.Add(pic);
                ResizeDiagram(pic, imgScrollPanel, originalImg);
                imgScrollPanel.Resize += (s, e) => ResizeDiagram(pic, imgScrollPanel, originalImg);
            }
            else
            {
                Label noDia = new Label
                {
                    Text = "Kein UML-Diagramm verfügbar.",
                    ForeColor = Color.Orange,
                    AutoSize = true,
                    Location = new Point(10, 10)
                };
                imgScrollPanel.Controls.Add(noDia);
            }

            // --- MATERIALS ---
            TabPage matPage = leftTabControl.TabPages[2];
            matPage.Controls.Clear();

            FlowLayoutPanel matFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = bgPanel,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10)
            };
            matPage.Controls.Add(matFlow);
            matFlow.Resize += (s, e) => ResizeMaterials(matFlow);

            if (!string.IsNullOrEmpty(level.AuxiliaryId))
            {
                string auxPath = Path.Combine(Application.StartupPath, "img", $"aux_{level.AuxiliaryId}.png");
                if (File.Exists(auxPath))
                {
                    Label auxHeader = new Label
                    {
                        Text = "Referenz-Klassen:",
                        ForeColor = accentGreen,
                        Font = new Font("Segoe UI", 10,FontStyle.Bold),
                        AutoSize = true,
                        Margin = new Padding(0, 0, 0, 5)
                    };
                    matFlow.Controls.Add(auxHeader);

                    Image auxImg = Image.FromFile(auxPath);
                    PictureBox auxPic = new PictureBox
                    {
                        Image = auxImg,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Height = 150,
                        Width = 200,
                        BackColor = Color.FromArgb(40, 40, 40),
                        BorderStyle = BorderStyle.FixedSingle,
                        Margin = new Padding(0, 0, 0, 15)
                    };
                    matFlow.Controls.Add(auxPic);
                }
            }

            // add text materials
            if (!string.IsNullOrEmpty(level.MaterialDocs))
            {
                var lines = level.MaterialDocs.Split(new[] { "\n" }, StringSplitOptions.None);
                string currentTextBuffer = "";

                void FlushTextBuffer()
                {
                    if (string.IsNullOrWhiteSpace(currentTextBuffer)) return;
                    RichTextBox rtb = CreateMaterialBox(currentTextBuffer);
                    matFlow.Controls.Add(rtb);
                    currentTextBuffer = "";
                }

                foreach (var line in lines)
                {
                    string trim = line.Trim();
                    if (trim.StartsWith("Hinweis:") || trim.StartsWith("Tipp:"))
                    {
                        FlushTextBuffer();

                        string previewText = trim.Length > 18 ? trim.Substring(0, 15) + "..." : trim;

                        Button spoilerBtn = new Button
                        {
                            Text = "▶ " + previewText,
                            ForeColor = Color.White,
                            BackColor = Color.FromArgb(60, 60, 65),
                            FlatStyle = FlatStyle.Flat,
                            TextAlign = ContentAlignment.MiddleLeft,
                            Height = 30,
                            Width = 200,
                            Margin = new Padding(0, 0, 0, 5),
                            Cursor = Cursors.Hand
                        };
                        spoilerBtn.FlatAppearance.BorderSize = 0;

                        RichTextBox contentBox = CreateMaterialBox(line);
                        contentBox.BackColor = Color.FromArgb(45, 45, 48);

                        contentBox.Margin = new Padding(0, 0, 0, 15);
                        contentBox.Visible = false;
                        contentBox.Tag = "hidden";

                        spoilerBtn.Click += (s, e) =>
                        {
                            spoilerBtn.Visible = false;
                            contentBox.Visible = true;

                            int scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
                            int targetW = matFlow.ClientSize.Width - matFlow.Padding.Horizontal - scrollBarWidth;

                            if (targetW > 50)
                            {
                                contentBox.Width = targetW;
                                // force height recalc
                                using (Graphics g = contentBox.CreateGraphics())
                                {
                                    SizeF size = g.MeasureString(contentBox.Text, contentBox.Font, contentBox.Width);
                                    contentBox.Height = (int)size.Height + 20;
                                }
                            }
                        };

                        matFlow.Controls.Add(spoilerBtn);
                        matFlow.Controls.Add(contentBox);
                    }
                    else
                    {
                        currentTextBuffer += line + "\n";
                    }
                }
                FlushTextBuffer();
            }

            ResizeMaterials(matFlow);

            consoleBox.Clear();
            consoleBox.ForeColor = Color.LightGray;
            consoleBox.Text = $"> System initialisiert.\r\n> Level {level.Id} geladen.";
        }

        private RichTextBox CreateMaterialBox(string content)
        {
            RichTextBox box = new RichTextBox
            {
                BackColor = bgPanel,
                ForeColor = fgWhite,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.None,
                Font = new Font("Segoe UI", 10),
                Width = 200,
                Margin = new Padding(0, 0, 0, 15)
            };
            box.ContentsResized += (s, e) => box.Height = e.NewRectangle.Height + 5;
            AppendFormattedText(box, content);
            return box;
        }

        private void ResizeDiagram(PictureBox pic, Panel container, Image img)
        {
            if (img == null || container.Width == 0) return;

            int padding = 20;
            int availableWidth = container.ClientSize.Width - padding;

            int targetWidth = img.Width * 2;
            int targetHeight = img.Height * 2;

            if (targetWidth > availableWidth)
            {
                float ratio = (float)availableWidth / targetWidth;
                targetWidth = availableWidth;
                targetHeight = (int)(targetHeight * ratio);
            }

            pic.Width = targetWidth;
            pic.Height = targetHeight;
        }

        private void AppendFormattedText(RichTextBox box, string text)
        {
            // for now replace escaped brackets with temp invisible tokens (as else regex creates issues)
            string safeText = text.Replace("|[", "\x01").Replace("|]", "\x02");

            // split normally
            var parts = Regex.Split(safeText, @"(\[.*?\])");

            foreach (var part in parts)
            {
                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    // its a highlighting block
                    string content = part.Substring(1, part.Length - 2);

                    // restore the tokens
                    content = content.Replace("\x01", "[").Replace("\x02", "]");

                    box.SelectionFont = new Font("Consolas", 11, FontStyle.Bold);
                    box.SelectionColor = Color.CornflowerBlue;
                    box.AppendText(content);
                }
                else // regular text
                {
                    string content = part.Replace("\x01", "[").Replace("\x02", "]");

                    box.SelectionFont = new Font("Segoe UI", 11);
                    box.SelectionColor = box.BackColor == bgPanel ? fgWhite : Color.LightGreen;
                    box.AppendText(content);
                }
            }
        }

        private void LoadNextLevel()
        {
            var nextLvl = levels.FirstOrDefault(l => l.SkipCode == currentLevel.NextLevelCode);

            if (nextLvl != null)
            {
                LoadLevel(nextLvl);
            }
            else
            {
                ShowCourseCompletedDialog();
            }
        }

        private void ShowCourseCompletedDialog()
        {
            Form dialog = new Form
            {
                Size = new Size(450, 250),
                Text = "Kurs Abgeschlossen",
                BackColor = Color.FromArgb(32, 33, 36),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false
            };

            int preference = 1;
            DwmSetWindowAttribute(dialog.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref preference, sizeof(int));

            Label headerLabel = new Label
            {
                Text = "🎉 Herzlichen Glückwunsch! 🎉",
                Dock = DockStyle.Top,
                Height = 50,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = accentGreen,
                Padding = new Padding(10, 15, 10, 0)
            };

            Label msg = new Label
            {
                Text = "Du hast alle Levels erfolgreich abgeschlossen!\n\nDu bist nun bereit für deine Abiturprüfung in\nPraktischer Informatik. Viel Erfolg!",
                Dock = DockStyle.Top,
                Height = 100,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10),
                Padding = new Padding(10)
            };

            Button btnOk = new Button
            {
                Text = "Schließen",
                DialogResult = DialogResult.OK,
                BackColor = accentGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 150,
                Height = 35,
                Location = new Point(150, 165),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;

            dialog.Controls.AddRange(new Control[] { headerLabel, msg, btnOk });
            dialog.AcceptButton = btnOk;

            dialog.ShowDialog();
        }

        private void CheckLevelCode(string code)
        {
            var lvl = levels.FirstOrDefault(l => l.SkipCode == code && playerData.UnlockedLevelIds.Contains(l.Id));
            if (lvl != null)
            {
                LoadLevel(lvl);
                skipCodeBox.Clear();
            }
        }

        private void CompileAndRun()
        {
            consoleBox.ForeColor = Color.LightGray;
            consoleBox.Clear();

            SaveCurrentProgress();

            string userCode = editorBox.Text;
            string fullCode = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n" + userCode;

            var syntaxTree = CSharpSyntaxTree.ParseText(fullCode);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location)
            };

            var compilation = CSharpCompilation.Create(
                "UserAssembly",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    consoleBox.ForeColor = Color.Red;
                    consoleBox.AppendText("KOMPILIERFEHLER:\r\n");
                    foreach (var diag in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        var lineSpan = diag.Location.GetLineSpan();
                        int userLine = lineSpan.StartLinePosition.Line - 3;
                        if (userLine < 0) userLine = 0;
                        consoleBox.AppendText($"Zeile {userLine}: {diag.GetMessage()}\r\n");
                    }
                    return;
                }

                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());
                RunTests(assembly);
            }
        }

        private void RunTests(Assembly assembly)
        {
            bool success = false;
            string feedback = "";

            try
            {
                // --- LEVEL 1 ---
                if (currentLevel.Id == 1)
                {
                    Type tierType = assembly.GetType("Tier");
                    if (tierType == null) throw new Exception("Klasse 'Tier' nicht gefunden.");

                    ConstructorInfo ctor = tierType.GetConstructor(new[] { typeof(string), typeof(int) });
                    if (ctor == null) throw new Exception("Konstruktor Tier(string, int) fehlt.");

                    object tier = ctor.Invoke(new object[] { "Löwe", 5 });
                    FieldInfo fName = tierType.GetField("name", BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo fAlter = tierType.GetField("alter", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fName == null) throw new Exception("Feld 'name' fehlt oder ist nicht private.");
                    if (fAlter == null) throw new Exception("Feld 'alter' fehlt oder ist nicht private.");

                    string actualName = (string)fName.GetValue(tier);
                    int actualAlter = (int)fAlter.GetValue(tier);

                    if (actualName == "Löwe" && actualAlter == 5)
                    {
                        success = true;
                        feedback = "Klasse Tier korrekt implementiert!";
                    }
                    else
                    {
                        throw new Exception("Konstruktor setzt die Werte nicht korrekt.");
                    }
                }
                // --- LEVEL 2 ---
                else if (currentLevel.Id == 2)
                {
                    Type t = assembly.GetType("Tier");
                    if (t == null) throw new Exception("Klasse 'Tier' nicht gefunden. Haben Sie sie gelöscht?");

                    object obj = Activator.CreateInstance(t);

                    MethodInfo mSet = t.GetMethod("SetAlter");
                    MethodInfo mGet = t.GetMethod("GetAlter");
                    FieldInfo fAlter = t.GetField("alter", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (mSet == null) throw new Exception("Methode SetAlter fehlt.");
                    if (mGet == null) throw new Exception("Methode GetAlter fehlt.");

                    // initial value check
                    fAlter.SetValue(obj, 10);

                    // test 1: invalid (lower)
                    mSet.Invoke(obj, new object[] { 5 });
                    int val1 = (int)mGet.Invoke(obj, null);
                    if (val1 != 10) throw new Exception("Fehler: Alter wurde trotz kleinerem Wert geändert! (Kapselung verletzt)");

                    // test 2: valid (higher)
                    mSet.Invoke(obj, new object[] { 12 });
                    int val2 = (int)mGet.Invoke(obj, null);
                    if (val2 != 12) throw new Exception("Fehler: Alter wurde trotz gültigem Wert nicht geändert.");

                    success = true;
                    feedback = "Kapselung und Logik erfolgreich!";
                }
                // --- LEVEL 3 ---
                else if (currentLevel.Id == 3)
                {
                    Type tTier = assembly.GetType("Tier");
                    Type tLoewe = assembly.GetType("Loewe");

                    if (tTier == null) throw new Exception("Klasse Tier fehlt.");
                    if (tLoewe == null) throw new Exception("Klasse Loewe fehlt.");

                    if (!tTier.IsAbstract) throw new Exception("Klasse Tier muss 'abstract' sein.");
                    if (!tLoewe.IsSubclassOf(tTier)) throw new Exception("Loewe erbt nicht von Tier.");

                    // check constructor chaining
                    ConstructorInfo ctor = tLoewe.GetConstructor(new[] { typeof(string), typeof(int) });
                    if (ctor == null) throw new Exception("Konstruktor Loewe(string, int) fehlt.");

                    // we cannot instantiate Tier, but we can instantiate Loewe
                    object leo = ctor.Invoke(new object[] { "Simba", 50 });

                    // check Bruellen
                    MethodInfo mB = tLoewe.GetMethod("Bruellen");
                    if (mB == null) throw new Exception("Methode Bruellen fehlt.");

                    string sound = (string)mB.Invoke(leo, null);
                    if (string.IsNullOrEmpty(sound)) throw new Exception("Bruellen gibt nichts zurück.");

                    success = true;
                    feedback = "Vererbung und Abstraktion korrekt!";
                }
                // --- LEVEL 4 ---
                else if (currentLevel.Id == 4)
                {
                    Type tG = assembly.GetType("Gehege");
                    if (tG == null) throw new Exception("Klasse Gehege fehlt.");

                    object g = Activator.CreateInstance(tG);
                    MethodInfo mAdd = tG.GetMethod("Hinzufuegen");
                    MethodInfo mCount = tG.GetMethod("AnzahlTiere");

                    // dynamic creation of Tier (since it might be abstract or concrete depending on user implementation)
                    Type tTier = assembly.GetType("Tier");
                    object animal;
                    if (tTier.IsAbstract)
                    {
                        // if user left Tier abstract from prev level, we need a concrete subclass to test list
                        throw new Exception("Für dieses Level bitte Klasse Tier wieder 'konkret' (nicht abstract) machen, oder eine konkrete Unterklasse nutzen.");
                    }
                    else
                    {
                        animal = Activator.CreateInstance(tTier);
                    }

                    mAdd.Invoke(g, new object[] { animal });
                    int count = (int)mCount.Invoke(g, null);

                    if (count == 1)
                    {
                        success = true;
                        feedback = "Tier erfolgreich zur Liste hinzugefügt.";
                    }
                    else throw new Exception("AnzahlTiere liefert falschen Wert.");
                }
                // --- LEVEL 5 ---
                else if (currentLevel.Id == 5)
                {
                    Type tG = assembly.GetType("Gehege");
                    if (tG == null) throw new Exception("Klasse 'Gehege' nicht gefunden."); // Safety check
                    object g = Activator.CreateInstance(tG);
                    Type tT = assembly.GetType("Tier");

                    object CreateTier(int age)
                    {
                        var t = Activator.CreateInstance(tT, new object[] { age });
                        return t;
                    }

                    object t1 = CreateTier(5);
                    object t2 = CreateTier(20);
                    object t3 = CreateTier(10);

                    FieldInfo fList = tG.GetField("bewohner");
                    if (fList == null) throw new Exception("Feld 'bewohner' nicht gefunden.");

                    MethodInfo mGetAlter = tT.GetMethod("GetAlter");
                    if (mGetAlter == null) throw new Exception("Methode 'GetAlter()' nicht gefunden in Klasse Tier.");

                    var listInstance = fList.GetValue(g);
                    MethodInfo listAdd = listInstance.GetType().GetMethod("Add");

                    listAdd.Invoke(listInstance, new object[] { t1 });
                    listAdd.Invoke(listInstance, new object[] { t2 });
                    listAdd.Invoke(listInstance, new object[] { t3 });

                    MethodInfo mAlgo = tG.GetMethod("ErmittleAeltestes") ?? throw new Exception("Methode 'ErmittleAeltestes' wurde nicht gefunden. Stelle sicher, dass der Name korrekt ist und die Methode 'public' ist.");
                    object result = mAlgo.Invoke(g, null);

                    int resultAge = (int)mGetAlter.Invoke(result, null);
                    int t2Age = (int)mGetAlter.Invoke(t2, null);

                    if (result == t2 && resultAge == 20)
                    {
                        success = true;
                        feedback = "Algorithmus korrekt! Das älteste Tier (20) wurde gefunden.";
                    }
                    else throw new Exception("Algorithmus liefert nicht das älteste Tier.");
                }

                if (success)
                {
                    consoleBox.ForeColor = Color.LightGreen;
                    consoleBox.Text = "✓ TEST BESTANDEN: " + feedback + "\r\n\r\n";

                    var nextLvl = levels.FirstOrDefault(l => l.SkipCode == currentLevel.NextLevelCode);
                    if (nextLvl != null && !playerData.UnlockedLevelIds.Contains(nextLvl.Id))
                    {
                        playerData.UnlockedLevelIds.Add(nextLvl.Id);
                        SaveSystem.Save(playerData);
                        consoleBox.AppendText($"🔓 Level '{nextLvl.Title}' freigeschaltet!\r\n");
                        consoleBox.AppendText($"Nächstes Level Code: {nextLvl.SkipCode}\r\n");
                        nextButton.Text = "NÄCHSTES LEVEL →";
                        nextButton.Visible = true;
                    }
                    else if (nextLvl != null)
                    {
                        consoleBox.AppendText($"\r\nNächstes Level Code: {nextLvl.SkipCode}");
                        nextButton.Text = "NÄCHSTES LEVEL →";
                        nextButton.Visible = true;
                    }
                    else // final level
                    {
                        consoleBox.AppendText("\r\n🎉 Das war das letzte Level dieser Sektion!");
                        nextButton.Text = "KURS ABSCHLIESSEN ✓";
                        nextButton.Visible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                consoleBox.ForeColor = Color.Orange;
                consoleBox.Text = "❌ LAUFZEITFEHLER / LOGIK:\r\n" + (ex.InnerException?.Message ?? ex.Message);
            }
        }

        private void ShowLevelSelector(object sender, EventArgs e)
        {
            Form selector = new Form
            {
                Text = "Level auswählen",
                Size = new Size(400, 600),
                MinimumSize = new Size(350, 400),
                BackColor = bgDark,
                ForeColor = fgWhite,
                FormBorderStyle = FormBorderStyle.Sizable,
                StartPosition = FormStartPosition.CenterParent,
                AutoScroll = true
            };

            FlowLayoutPanel panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = bgDark,
                Padding = new Padding(10)
            };

            var handle = selector.Handle;
            int preference = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref preference, sizeof(int));

            ApplyDarkScrollbars(panel);

            var groups = levels.GroupBy(l => l.Section);

            foreach (var group in groups)
            {
                Label secHeader = new Label
                {
                    Text = group.Key,
                    ForeColor = accentGreen,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    AutoSize = true,
                    Margin = new Padding(0, 10, 0, 5)
                };
                panel.Controls.Add(secHeader);
                panel.SetFlowBreak(secHeader, true);

                foreach (var lvl in group)
                {
                    bool unlocked = playerData.UnlockedLevelIds.Contains(lvl.Id);
                    Button b = new Button
                    {
                        Text = (unlocked ? "🔓 " : "🔒 ") + $"{lvl.Id}. {lvl.Title}",
                        Width = 340,
                        Height = 40,
                        BackColor = unlocked ? bgPanel : Color.FromArgb(25, 25, 25),
                        ForeColor = unlocked ? fgWhite : Color.Gray,
                        FlatStyle = FlatStyle.Flat,
                        TextAlign = ContentAlignment.MiddleLeft
                    };

                    if (unlocked)
                    {
                        b.Click += (s, ev) => { LoadLevel(lvl); selector.Close(); };
                    }
                    panel.Controls.Add(b);
                }
            }

            selector.Controls.Add(panel);
            selector.ShowDialog();
        }

        private void ResetLevelCode()
        {
            Form dialog = new Form
            {
                Size = new Size(400, 200),
                Text = "Code zurücksetzen",
                BackColor = Color.FromArgb(32, 33, 36),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false
            };

            int preference = 1;
            DwmSetWindowAttribute(dialog.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref preference, sizeof(int));

            Label msg = new Label
            {
                Text = "Möchtest du den Code wirklich auf den Anfangszustand zurücksetzen?\nAlle Änderungen gehen verloren.",
                Dock = DockStyle.Top,
                Height = 80,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10),
                Padding = new Padding(10)
            };

            Button btnYes = new Button
            {
                Text = "Ja, zurücksetzen",
                DialogResult = DialogResult.Yes,
                BackColor = Color.FromArgb(180, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 150,
                Location = new Point(30, 100)
            };
            Button btnNo = new Button
            {
                Text = "Abbrechen",
                DialogResult = DialogResult.No,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 150,
                Location = new Point(200, 100)
            };

            dialog.Controls.AddRange(new Control[] { msg, btnYes, btnNo });
            dialog.AcceptButton = btnYes;
            dialog.CancelButton = btnNo;

            if (dialog.ShowDialog() == DialogResult.Yes && currentLevel != null)
            {
                editorBox.Text = currentLevel.StarterCode.Replace("\n", Environment.NewLine);
                consoleBox.Text = "> Code auf Standard zurückgesetzt.";
                SaveCurrentProgress();
            }
        }

        private void ManualSave()
        {
            SaveCurrentProgress();

            // visual feedback in console
            consoleBox.ForeColor = Color.LightGray;
            consoleBox.AppendText("\r\n> Fortschritt manuell gespeichert.");

            Button btn = (Button)leftTabControl.FindForm().Controls.Find("saveButton", true).FirstOrDefault();
        }
    }

    public class DarkTabControl : TabControl
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x14)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
    }

    // --- CUSTOM DARK TAB CONTROL ---
    public class EliteTabControl : TabControl
    {
        private Color bgHeader = Color.FromArgb(32, 33, 36); // Background behind tabs
        private Color bgActive = Color.FromArgb(45, 45, 48); // Selected Tab
        private Color bgInactive = Color.FromArgb(32, 33, 36); // Unselected Tab
        private Color fgTextActive = Color.White;
        private Color fgTextInactive = Color.Gray;
        private Color accentColor = Color.FromArgb(50, 168, 82); // Green accent line

        public EliteTabControl()
        {
            // Take full control of painting to remove the 3D OS border
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(140, 40); // Wider, taller tabs
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 1. Paint the entire control background first
            g.Clear(bgHeader);

            // 2. Draw Tabs
            for (int i = 0; i < TabCount; i++)
            {
                Rectangle r = GetTabRect(i);
                bool isSelected = (SelectedIndex == i);

                // Tab Background
                using (SolidBrush b = new SolidBrush(isSelected ? bgActive : bgInactive))
                {
                    g.FillRectangle(b, r);
                }

                // Tab Text
                string text = TabPages[i].Text;
                using (SolidBrush b = new SolidBrush(isSelected ? fgTextActive : fgTextInactive))
                {
                    // Center text manually
                    SizeF textSize = g.MeasureString(text, Font);
                    float x = r.X + (r.Width - textSize.Width) / 2;
                    float y = r.Y + (r.Height - textSize.Height) / 2;
                    g.DrawString(text, Font, b, x, y);
                }

                // Accent Line (Green bar at the bottom of active tab)
                if (isSelected)
                {
                    using (SolidBrush barBrush = new SolidBrush(accentColor))
                    {
                        g.FillRectangle(barBrush, r.X, r.Bottom - 3, r.Width, 3);
                    }
                }
            }

            // 3. Draw border around the ENTIRE TabControl (including content area)
            using (Pen borderPen = new Pen(Color.FromArgb(60, 60, 60), 1))
            {
                // Draw the outer border of the entire control
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

                // Draw a separator line between tab headers and content
                int separatorY = ItemSize.Height;
                g.DrawLine(borderPen, 0, separatorY, Width - 1, separatorY);
            }
        }

        // Fix the "Depth" border issue: 
        // By overriding DisplayRectangle, we remove the space the OS usually reserves for the 3D frame.
        public override Rectangle DisplayRectangle
        {
            get
            {
                Rectangle rect = ClientRectangle;
                rect.Y += ItemSize.Height; // Start below the tabs
                rect.Height -= ItemSize.Height; // Reduce height
                return rect;
            }
        }
    }
}