using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using System.IO;
using System.Xml;

namespace AbiturEliteCode
{
    internal class SqlCodeEditor
    {
        public static IHighlightingDefinition GetDarkSqlHighlighting()
        {
            string xshd =
                @"
<SyntaxDefinition name=""SQL Dark"" extensions="".sql"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
	<Color name=""Comment"" foreground=""#6A9955"" exampleText=""-- comment"" />
	<Color name=""String"" foreground=""#CE9178"" exampleText=""'text'"" />
	<Color name=""Number"" foreground=""#B5CEA8"" exampleText=""42"" />
	<Color name=""Punctuation"" foreground=""#D4D4D4"" exampleText=""a(b);"" />
	<Color name=""Keywords"" foreground=""#569CD6"" fontWeight=""bold"" exampleText=""SELECT FROM"" />
	<Color name=""Functions"" foreground=""#DCDCAA"" exampleText=""COUNT()"" />
	<Color name=""Variables"" foreground=""#9CDCFE"" exampleText=""@myvar"" />
    <Color name=""Types"" foreground=""#4EC9B0"" exampleText=""INT VARCHAR"" />

	<RuleSet ignoreCase=""true"">
		<Span color=""Comment"">
			<Begin>--</Begin>
		</Span>
		<Span color=""Comment"" multiline=""true"">
			<Begin>/\*</Begin>
			<End>\*/</End>
		</Span>
		<Span color=""String"">
			<Begin>'</Begin>
			<End>'</End>
            <RuleSet>
				<Span begin=""\\"" end="".""/>
			</RuleSet>
		</Span>
        <Span color=""String"">
			<Begin>""</Begin>
			<End>""</End>
            <RuleSet>
				<Span begin=""\\"" end="".""/>
			</RuleSet>
		</Span>
        <Span color=""Variables"">
            <Begin>@</Begin>
        </Span>

		<Keywords color=""Keywords"">
			<Word>SELECT</Word>
			<Word>FROM</Word>
			<Word>WHERE</Word>
			<Word>GROUP</Word>
			<Word>BY</Word>
			<Word>HAVING</Word>
			<Word>ORDER</Word>
			<Word>LIMIT</Word>
			<Word>OFFSET</Word>
			<Word>INSERT</Word>
			<Word>INTO</Word>
			<Word>VALUES</Word>
			<Word>UPDATE</Word>
			<Word>SET</Word>
			<Word>DELETE</Word>
			<Word>JOIN</Word>
			<Word>INNER</Word>
			<Word>LEFT</Word>
			<Word>RIGHT</Word>
			<Word>OUTER</Word>
			<Word>CROSS</Word>
			<Word>ON</Word>
			<Word>AS</Word>
			<Word>DISTINCT</Word>
			<Word>ALL</Word>
			<Word>UNION</Word>
			<Word>AND</Word>
			<Word>OR</Word>
			<Word>NOT</Word>
			<Word>NULL</Word>
			<Word>IS</Word>
			<Word>IN</Word>
			<Word>BETWEEN</Word>
			<Word>LIKE</Word>
			<Word>EXISTS</Word>
            <Word>CREATE</Word>
            <Word>TABLE</Word>
            <Word>DROP</Word>
            <Word>ALTER</Word>
            <Word>PRIMARY</Word>
            <Word>KEY</Word>
            <Word>FOREIGN</Word>
            <Word>REFERENCES</Word>
            <Word>DEFAULT</Word>
            <Word>AUTO_INCREMENT</Word>
		</Keywords>

        <Keywords color=""Types"">
            <Word>INT</Word>
            <Word>INTEGER</Word>
            <Word>VARCHAR</Word>
            <Word>TEXT</Word>
            <Word>CHAR</Word>
            <Word>DATE</Word>
            <Word>DATETIME</Word>
            <Word>TIMESTAMP</Word>
            <Word>FLOAT</Word>
            <Word>DOUBLE</Word>
            <Word>DECIMAL</Word>
            <Word>BOOLEAN</Word>
        </Keywords>

		<Keywords color=""Functions"">
			<Word>COUNT</Word>
			<Word>SUM</Word>
			<Word>AVG</Word>
			<Word>MIN</Word>
			<Word>MAX</Word>
			<Word>UPPER</Word>
			<Word>LOWER</Word>
			<Word>LENGTH</Word>
            <Word>CONCAT</Word>
            <Word>NOW</Word>
		</Keywords>

		<Rule color=""Number"">
			\b0[xX][0-9a-fA-F]+|(\b\d+(\.[0-9]+)?|\.[0-9]+)([eE][+-]?[0-9]+)?
		</Rule>

        <Rule color=""Punctuation"">
            [;,()]
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