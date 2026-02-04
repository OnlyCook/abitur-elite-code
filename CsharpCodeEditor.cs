using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using System.IO;
using System.Xml;

namespace AbiturEliteCode
{
    internal class CsharpCodeEditor
    {
        public static IHighlightingDefinition GetDarkCsharpHighlighting()
        {
            string xshd =
                @"
<SyntaxDefinition name=""C# Dark"" extensions="".cs"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
	<Color name=""Comment"" foreground=""#6A9955"" exampleText=""// comment"" />
	<Color name=""String"" foreground=""#CE9178"" exampleText=""string text = &quot;Hello&quot;"" />
	<Color name=""Char"" foreground=""#D7BA7D"" exampleText=""char linefeed = '\n';"" />
	<Color name=""Preprocessor"" foreground=""#9B9B9B"" exampleText=""#region Title"" />
	<Color name=""Punctuation"" foreground=""#D4D4D4"" exampleText=""a(b.c);"" />
	<Color name=""ValueTypeKeywords"" foreground=""#569CD6"" exampleText=""bool b = true;"" />
	<Color name=""ReferenceTypeKeywords"" foreground=""#569CD6"" exampleText=""object o;"" />
	<Color name=""MethodCall"" foreground=""#DCDCAA"" exampleText=""o.ToString();""/>
	<Color name=""NumberLiteral"" foreground=""#B5CEA8"" exampleText=""3.1415f""/>
	<Color name=""ThisOrBaseReference"" foreground=""#569CD6"" exampleText=""this.Do(); base.Do();""/>
	<Color name=""NullOrValueKeywords"" foreground=""#569CD6"" exampleText=""if (value == null)""/>
	<Color name=""Keywords"" foreground=""#C586C0"" exampleText=""if (a) {} else {}""/>
	<Color name=""GotoKeywords"" foreground=""#C586C0"" exampleText=""continue; return;""/>
	<Color name=""ContextKeywords"" foreground=""#569CD6"" exampleText=""var a = from x in y select z;""/>
	<Color name=""ExceptionKeywords"" foreground=""#C586C0"" exampleText=""try {} catch {} finally {}""/>
	<Color name=""CheckedKeyword"" foreground=""#569CD6"" exampleText=""checked {}""/>
	<Color name=""UnsafeKeywords"" foreground=""#569CD6"" exampleText=""unsafe { fixed (..) {} }"" />
	<Color name=""OperatorKeywords"" foreground=""#569CD6"" exampleText=""public static implicit operator..."" />
	<Color name=""ParameterModifiers"" foreground=""#569CD6"" exampleText=""(ref int a, params int[] b)"" />
	<Color name=""Modifiers"" foreground=""#569CD6"" exampleText=""public static override"" />
	<Color name=""Visibility"" foreground=""#569CD6"" exampleText=""public internal"" />
	<Color name=""NamespaceKeywords"" foreground=""#569CD6"" exampleText=""namespace A.B { using System; }"" />
	<Color name=""GetSetAddRemove"" foreground=""#569CD6"" exampleText=""int Prop { get; set; }"" />
	<Color name=""TrueFalse"" foreground=""#569CD6"" exampleText=""b = false; a = true;"" />
	<Color name=""TypeKeywords"" foreground=""#569CD6"" exampleText=""if (x is int) { a = x as int; type = typeof(int); size = sizeof(int); c = new object(); }"" />
    <Color name=""SemanticType"" foreground=""#4EC9B0"" exampleText=""List&lt;int&gt; list;"" />

	<RuleSet name=""CommentMarkerSet"">
		<Keywords fontWeight=""bold"" foreground=""#969696"">
			<Word>TODO</Word>
			<Word>FIXME</Word>
		</Keywords>
		<Keywords fontWeight=""bold"" foreground=""#969696"">
			<Word>HACK</Word>
			<Word>UNDONE</Word>
		</Keywords>
	</RuleSet>

	<RuleSet>
		<Span color=""Comment"">
			<Begin>//</Begin>
			<RuleSet>
				<Import ruleSet=""CommentMarkerSet""/>
			</RuleSet>
		</Span>
		<Span color=""Comment"" multiline=""true"">
			<Begin>/\*</Begin>
			<End>\*/</End>
			<RuleSet>
				<Import ruleSet=""CommentMarkerSet""/>
			</RuleSet>
		</Span>
		<Span color=""String"">
			<Begin>""</Begin>
			<End>""</End>
			<RuleSet>
				<Span begin=""\\"" end="".""/>
			</RuleSet>
		</Span>
		<Span color=""Char"">
			<Begin>'</Begin>
			<End>'</End>
			<RuleSet>
				<Span begin=""\\"" end="".""/>
			</RuleSet>
		</Span>
		<Span color=""Preprocessor"">
			<Begin>\#</Begin>
			<RuleSet name=""PreprocessorSet"">
				<Span> <!-- preprocessor directives that allow comments -->
					<Begin fontWeight=""bold"">region</Begin>
					<RuleSet>
						<Span color=""Comment"">
							<Begin>//</Begin>
							<RuleSet>
								<Import ruleSet=""CommentMarkerSet""/>
							</RuleSet>
						</Span>
						<Span color=""Comment"" multiline=""true"">
							<Begin>/\*</Begin>
							<End>\*/</End>
							<RuleSet>
								<Import ruleSet=""CommentMarkerSet""/>
							</RuleSet>
						</Span>
					</RuleSet>
				</Span>
			</RuleSet>
		</Span>
		<Keywords color=""TrueFalse"">
			<Word>true</Word>
			<Word>false</Word>
		</Keywords>
		<Keywords color=""Keywords"">
			<Word>else</Word>
			<Word>if</Word>
			<Word>switch</Word>
			<Word>case</Word>
			<Word>default</Word>
			<Word>do</Word>
			<Word>for</Word>
			<Word>foreach</Word>
			<Word>in</Word>
			<Word>while</Word>
			<Word>lock</Word>
		</Keywords>
		<Keywords color=""GotoKeywords"">
			<Word>break</Word>
			<Word>continue</Word>
			<Word>goto</Word>
			<Word>return</Word>
		</Keywords>
		<Keywords color=""ContextKeywords"">
			<Word>yield</Word>
			<Word>partial</Word>
			<Word>global</Word>
			<Word>where</Word>
			<Word>select</Word>
			<Word>group</Word>
			<Word>by</Word>
			<Word>into</Word>
			<Word>from</Word>
			<Word>ascending</Word>
			<Word>descending</Word>
			<Word>orderby</Word>
			<Word>let</Word>
			<Word>join</Word>
			<Word>on</Word>
			<Word>equals</Word>
		</Keywords>
		<Keywords color=""ExceptionKeywords"">
			<Word>try</Word>
			<Word>throw</Word>
			<Word>catch</Word>
			<Word>finally</Word>
		</Keywords>
		<Keywords color=""CheckedKeyword"">
			<Word>checked</Word>
			<Word>unchecked</Word>
		</Keywords>
		<Keywords color=""UnsafeKeywords"">
			<Word>fixed</Word>
			<Word>unsafe</Word>
		</Keywords>
		<Keywords color=""ValueTypeKeywords"">
			<Word>bool</Word>
			<Word>byte</Word>
			<Word>char</Word>
			<Word>decimal</Word>
			<Word>double</Word>
			<Word>enum</Word>
			<Word>float</Word>
			<Word>int</Word>
			<Word>long</Word>
			<Word>sbyte</Word>
			<Word>short</Word>
			<Word>struct</Word>
			<Word>uint</Word>
			<Word>ushort</Word>
			<Word>ulong</Word>
		</Keywords>
		<Keywords color=""ReferenceTypeKeywords"">
			<Word>class</Word>
			<Word>interface</Word>
			<Word>delegate</Word>
			<Word>object</Word>
			<Word>string</Word>
			<Word>void</Word>
		</Keywords>
		<Keywords color=""OperatorKeywords"">
			<Word>explicit</Word>
			<Word>implicit</Word>
			<Word>operator</Word>
		</Keywords>
		<Keywords color=""ParameterModifiers"">
			<Word>params</Word>
			<Word>ref</Word>
			<Word>out</Word>
		</Keywords>
		<Keywords color=""Modifiers"">
			<Word>abstract</Word>
			<Word>const</Word>
			<Word>event</Word>
			<Word>extern</Word>
			<Word>override</Word>
			<Word>readonly</Word>
			<Word>sealed</Word>
			<Word>static</Word>
			<Word>virtual</Word>
			<Word>volatile</Word>
			<Word>async</Word>
		</Keywords>
		<Keywords color=""Visibility"">
			<Word>public</Word>
			<Word>protected</Word>
			<Word>private</Word>
			<Word>internal</Word>
		</Keywords>
		<Keywords color=""NamespaceKeywords"">
			<Word>namespace</Word>
			<Word>using</Word>
		</Keywords>
		<Keywords color=""GetSetAddRemove"">
			<Word>get</Word>
			<Word>set</Word>
			<Word>add</Word>
			<Word>remove</Word>
		</Keywords>
		<Keywords color=""NullOrValueKeywords"">
			<Word>null</Word>
			<Word>value</Word>
		</Keywords>
		<Keywords color=""TypeKeywords"">
			<Word>as</Word>
			<Word>is</Word>
			<Word>new</Word>
			<Word>sizeof</Word>
			<Word>typeof</Word>
			<Word>stackalloc</Word>
		</Keywords>
		<Keywords color=""ThisOrBaseReference"">
			<Word>this</Word>
			<Word>base</Word>
		</Keywords>
        <!-- Fallback for standard types often found in Abitur code -->
        <Keywords color=""SemanticType"">
             <Word>List</Word>
             <Word>Dictionary</Word>
             <Word>Console</Word>
             <Word>Math</Word>
             <Word>Convert</Word>
             <Word>Array</Word>
        </Keywords>
		<Rule color=""MethodCall"">
			\b
			[\d\w_]+  # an identifier
			(?=\s*\() # followed by (
		</Rule>
		<Rule color=""NumberLiteral"">
			\b0[xX][0-9a-fA-F]+  # hex number
		|	
			(	\b\d+(\.[0-9]+)?   #number with optional floating point
			|	\.[0-9]+           #or just starting with floating point
			)
			([eE][+-]?[0-9]+)? # optional exponent
		</Rule>
	</RuleSet>
</SyntaxDefinition>";

            using (var reader = XmlReader.Create(new StringReader(xshd)))
            {
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }
    }
}
