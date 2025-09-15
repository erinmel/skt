using skt.Shared;

namespace skt.Compiler;

internal static class Program
{
    private sealed record Options(string FilePath, bool TokenizeOnly, bool ParseOnly, bool Verbose);

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var options = ParseOptions(args);
        if (!File.Exists(options.FilePath))
        {
            Console.WriteLine($"File not found: {options.FilePath}");
            return 1;
        }

        string source = File.ReadAllText(options.FilePath);
        PrintIntro(options, source);

        var analyzer = new LexicalAnalyzer();

        if (options.TokenizeOnly)
        {
            RunTokenizeOnly(analyzer, source, options);
            return 0;
        }

        if (options.ParseOnly)
        {
            // Ensure tokens exist (tokenize quietly) and then parse/print AST
            _ = analyzer.TokenizeToFile(source, options.FilePath);
            RunParse(options);
            return 0;
        }

        RunFullCompilation(analyzer, source, options);
        return 0;
    }

    private static Options ParseOptions(string[] args)
    {
        var filePath = args[0];
        bool tokenizeOnly = args.Contains("--tokenize");
        bool parseOnly = args.Contains("--parse");
        bool verbose = args.Contains("--verbose");
        return new Options(filePath, tokenizeOnly, parseOnly, verbose);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: skt.Compiler <source-file> [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --tokenize    Generate token file (.sktt) only");
        Console.WriteLine("  --parse       Parse and show AST");
        Console.WriteLine("  --verbose     Show detailed output");
    }

    private static void PrintIntro(Options options, string source)
    {
        if (options.Verbose)
        {
            Console.WriteLine($"Processing file: {options.FilePath}");
            Console.WriteLine($"File size: {source.Length} characters");
            Console.WriteLine();
        }

        Console.WriteLine("Source Code:");
        Console.WriteLine("=" + new string('=', 50));
        Console.WriteLine(source);
        Console.WriteLine();
    }

    private static void RunTokenizeOnly(LexicalAnalyzer analyzer, string source, Options options)
    {
        var (tokens, errors) = analyzer.TokenizeToFile(source, options.FilePath);

        Console.WriteLine("Tokenization complete:");
        Console.WriteLine($"  Tokens: {tokens.Count}");
        Console.WriteLine($"  Errors: {errors.Count}");

        if (errors.Count > 0)
        {
            PrintLexicalErrors(errors);
        }
    }

    private static void RunFullCompilation(LexicalAnalyzer analyzer, string source, Options options)
    {
        Console.WriteLine("Step 1: Lexical Analysis");
        Console.WriteLine("-" + new string('-', 30));

        var (tokens, lexErrors) = analyzer.TokenizeToFile(source, options.FilePath);
        Console.WriteLine($"Tokens generated: {tokens.Count}");

        if (lexErrors.Count > 0)
        {
            Console.WriteLine("Lexical Errors:");
            PrintLexicalErrors(lexErrors);
            Console.WriteLine();
        }

        if (options.Verbose)
        {
            PrintSampleTokens(tokens);
        }

        Console.WriteLine("Step 2: Syntax Analysis");
        Console.WriteLine("-" + new string('-', 30));

        RunParse(options);
    }

    private static void RunParse(Options options)
    {
        var syntaxAnalyzer = new SyntaxAnalyzer();
        var (ast, parseErrors) = syntaxAnalyzer.Parse(options.FilePath);

        if (parseErrors.Count > 0)
        {
            Console.WriteLine("Parse Errors:");
            PrintParseErrors(parseErrors);
            Console.WriteLine();
        }

        if (ast != null)
        {
            Console.WriteLine("Abstract Syntax Tree:");
            Console.WriteLine("-" + new string('-', 30));
            PrintAst(ast, 0);
            Console.WriteLine();
            Console.WriteLine("Parsing completed successfully!");
        }
        else
        {
            Console.WriteLine("Failed to generate AST due to errors.");
        }
    }

    private static void PrintLexicalErrors(List<ErrorToken> errors)
    {
        Console.WriteLine("\nTokenization Errors:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  Line {error.Line}, Column {error.Column}: {error.Expected} - '{error.Value}'");
        }
    }

    private static void PrintParseErrors(List<ParseError> parseErrors)
    {
        foreach (var error in parseErrors)
        {
            Console.WriteLine($"  Line {error.Line}, Column {error.Column}: {error.Message}");
            if (!string.IsNullOrEmpty(error.FoundToken))
            {
                Console.WriteLine($"    Found: '{error.FoundToken}'");
            }
        }
    }

    private static void PrintSampleTokens(List<Token> tokens)
    {
        Console.WriteLine("\nTokens:");
        foreach (var token in tokens.Take(20))
        {
            Console.WriteLine($"  {token.Type}: '{token.Value}' (Line {token.Line}, Col {token.Column})");
        }
        if (tokens.Count > 20)
        {
            Console.WriteLine($"  ... and {tokens.Count - 20} more tokens");
        }
        Console.WriteLine();
    }

    private static void PrintAst(AstNode node, int depth)
    {
        string indent = new string(' ', depth * 2);

        Console.WriteLine(node.Token != null
            ? $"{indent}├─ {node.Rule}: '{node.Token.Value}'"
            : $"{indent}├─ {node.Rule}");

        foreach (var child in node.Children)
        {
            PrintAst(child, depth + 1);
        }
    }
}