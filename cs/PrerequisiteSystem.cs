using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AbiturEliteCode.cs
{
    internal static class PrerequisiteSystem
    {
        // PREREQUISITES SECTIONS (Rows in AllTopics):
        // > Hello World
        // > Basic Types
        // > Operators
        // > Control Flow
        // > Loops
        // > Methods
        // > Arrays
        // > Collections
        // > Classes and Objects
        // > Object-Oriented Programming
        // > Structs, Records, and Enums
        // > Exception Handling
        // > Generics
        // > Delegates and Lambdas
        // > LINQ Basics
        // > Async Programming
        // > String Operations
        // > Date and Time
        // > Nullable Types
        // > Pattern Matching
        // > Advanced Types
        // > Modern C# Features
        // > Type Conversions
        // > File I/O
        // > HTTP Requests
        // > C# 13 and C# 14 Features

        public class LessonData
        {
            public string Title { get; set; }
            public string DometrainUrl { get; set; }
            public string DocsUrl { get; set; }
        }

        private static Dictionary<string, LessonData> _database = new();

        public static readonly List<string> AllTopics = new List<string>
        {
            "Console printing", "Console.Write", "Console.ReadLine", "Single line comments", "Multi line comments", "Variables", "Constants", "The var keyword",
            "Integers", "Doubles", "Decimals", "Strings", "Escape Sequences", "Verbatim Strings", "String concatenation", "String interpolation", "Chars", "Booleans", "Floats",
            "Addition", "Subtraction", "Multiplication", "Division", "The Modulo operator", "Order of operations", "Compound assignment operators", "Increment and Decrement", "Comparison operators", "Logical AND", "Logical OR", "Logical NOT",
            "If statements", "If-Else statements", "Else-If chains", "Logical patterns", "Switch statements", "Switch expressions", "The Ternary operator",
            "While Loops", "Avoiding Infinite Loops", "Do-While Loops", "For Loops", "For Loop Counting Down", "For-Each Loops", "The break statement", "The continue statement", "Nested Loops",
            "Defining void methods", "Method parameters", "Return values", "Using return values", "Method overloading", "Optional parameters", "Named arguments", "Params arguments", "ref Parameters", "out Parameters", "in Parameters",
            "Creating Arrays", "Array Initializer Syntax", "Modifying Array Elements", "Looping Arrays with for", "Looping Arrays with foreach", "Multi-Dimensional Arrays", "Jagged Arrays", "Ranges and Indices",
            "Creating Lists", "Adding to Lists", "Accessing List Elements", "Removing from Lists", "Checking List Contents", "Sorting Lists", "Creating Dictionaries", "Adding and Accessing Dictionary Items", "Checking Dictionary Keys", "Looping Through Dictionaries",
            "Defining a Class", "Fields", "Default Constructors", "Parameterized Constructors", "Constructor Overloading", "Properties", "Auto-Properties", "Read-Only Properties", "Private Set Properties", "The this Keyword", "Public Access Modifier", "Private Access Modifier",
            "The static keyword", "Static Fields", "Static Methods", "Inheritance Basics", "The base Keyword", "Virtual Methods", "Method Overriding", "Abstract Classes", "Abstract Methods", "Defining Interfaces", "Implementing Interfaces", "Multiple Interfaces", "Default interface methods",
            "Defining Structs", "Value Type Behavior", "Defining Enums", "Enum Values", "Enums in Switch", "Defining Records", "Record With Expressions",
            "Try-Catch Blocks", "Exception Messages", "Multiple Catch Blocks", "The Finally Block", "Throwing Exceptions", "Custom Exception Messages",
            "Generic Classes", "Using Generic Classes", "Generic Methods", "Generic Constraints",
            "Defining Delegates", "Using Delegates", "Action Delegates", "Func Delegates", "Lambda Expression Basics", "Lambda with Multiple Statements", "Events", "Subscribing to Events",
            "Introduction to LINQ", "LINQ Query Syntax", "Where for Filtering", "Select for Transforming", "OrderBy for Sorting", "ThenBy for Secondary Sorting", "Count and Sum", "Average, Min, and Max", "First and FirstOrDefault", "Single and SingleOrDefault", "Any and All",
            "Async and Await", "Returning Values from Async", "Task.WhenAll", "Task.WhenAny",
            "ToUpper and ToLower", "Trim", "Substring", "Replace", "Split", "Join", "Contains and IndexOf", "StartsWith and EndsWith", "String Comparisons", "Format Specifiers", "Interpolation Format", "StringBuilder",
            "DateTime Basics", "Creating DateTime Values", "Formatting Dates", "Parsing Dates", "Date Arithmetic", "TimeSpan Basics", "Comparing Dates", "DateOnly Basics", "TimeOnly Basics",
            "Nullable Value Types", "The Null-Coalescing Operator", "Nullable Reference Types", "The Null-Conditional Operator", "The Null-Forgiving Operator",
            "Type Checking with is", "Type Patterns with Variables", "Switch with Type Patterns", "Property Patterns", "Relational Patterns", "Switch Expressions", "When Guards",
            "Creating Tuples", "Named Tuple Elements", "Returning Tuples from Methods", "Tuple Deconstruction", "Anonymous Types", "Extension Methods", "Understanding Attributes", "Creating Custom Attributes", "The using Statement",
            "Init-Only Properties", "Required Properties", "Raw String Literals", "Collection Expression Syntax", "Spread Operator in Collections", "Primary Constructors",
            "Implicit Conversion", "Explicit Conversion (Casting)", "The Convert Class", "Parse Methods", "TryParse Methods", "Checked Arithmetic", "Unchecked Arithmetic",
            "Writing Text Files", "Reading Text Files", "File Lines", "Checking File Existence", "Path Manipulation", "JSON Serialization", "JSON Deserialization",
            "Introduction to HttpClient", "Making GET Requests", "HttpResponseMessage", "Checking Status Codes", "Using BaseAddress", "Making POST Requests", "Sending JSON with POST", "Deserializing JSON Responses", "Setting Request Headers", "Handling HTTP Errors", "PUT and DELETE Requests", "Async HTTP Operations",
            "Params Collections", "The Lock Type", "Partial Properties", "The field Keyword"
        };

        public static void Initialize()
        {
            try
            {
                var uri = new Uri("avares://AbiturEliteCode/assets/prerequisites.txt");
                if (AssetLoader.Exists(uri))
                {
                    using (var stream = AssetLoader.Open(uri))
                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(">")) continue;

                            var parts = line.Split('|');
                            if (parts.Length >= 3)
                            {
                                string title = parts[0].Trim();
                                string dtRaw = parts[1].Replace("dometrain:", "").Trim();
                                string docRaw = parts[2].Replace("docs:", "").Trim();

                                if (!dtRaw.StartsWith("https://")) dtRaw = "https://dometrain.com" + dtRaw;

                                _database[title] = new LessonData
                                {
                                    Title = title,
                                    DometrainUrl = dtRaw,
                                    DocsUrl = docRaw
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load prerequisites: {ex.Message}");
            }
        }

        public static LessonData GetLesson(string title)
        {
            return _database.TryGetValue(title, out var data) ? data : null;
        }

        public static void OpenUrl(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
            }
            catch { }
        }
    }
}
