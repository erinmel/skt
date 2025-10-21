using skt.Shared;

namespace skt.Compiler;

internal static class Program
{
    private sealed record Options(string FilePath, bool TokenizeOnly, bool ParseOnly, bool SemanticOnly, bool Verbose);

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

        if (options.SemanticOnly)
        {
            _ = analyzer.TokenizeToFile(source, options.FilePath);
            RunSemanticAnalysis(options);
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
        bool semanticOnly = args.Contains("--semantic");
        bool verbose = args.Contains("--verbose");
        return new Options(filePath, tokenizeOnly, parseOnly, semanticOnly, verbose);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: skt.Compiler <source-file> [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --tokenize    Generate token file (.sktt) only");
        Console.WriteLine("  --parse       Parse and show AST");
        Console.WriteLine("  --semantic    Perform semantic analysis");
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

        var (ast, parseErrors) = RunParse(options);

        if (ast != null && parseErrors.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("Step 3: Semantic Analysis");
            Console.WriteLine("-" + new string('-', 30));

            RunSemanticAnalysisWithAst(ast, options);
        }
    }

    private static (AstNode? ast, List<ParseError> errors) RunParse(Options options)
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

        return (ast, parseErrors);
    }

    private static void RunSemanticAnalysis(Options options)
    {
        var syntaxAnalyzer = new SyntaxAnalyzer();
        var (ast, parseErrors) = syntaxAnalyzer.Parse(options.FilePath);

        if (parseErrors.Count > 0)
        {
            Console.WriteLine("Parse Errors Found:");
            PrintParseErrors(parseErrors);
            Console.WriteLine();
        }

        if (ast != null)
        {
            RunSemanticAnalysisWithAst(ast, options);
        }
        else
        {
            Console.WriteLine("Cannot perform semantic analysis - AST generation failed.");
        }
    }

    private static void RunSemanticAnalysisWithAst(AstNode ast, Options options)
    {
        var semanticAnalyzer = new SemanticAnalyzer();
        var (annotatedAst, symbolTable, semanticErrors) = semanticAnalyzer.Analyze(ast);

        Console.WriteLine($"Semantic Analysis Complete:");
        Console.WriteLine($"  Symbols declared: {symbolTable.Entries.Count}");
        Console.WriteLine($"  Semantic errors: {semanticErrors.Count}");
        Console.WriteLine();

        if (symbolTable.Entries.Count > 0)
        {
            Console.WriteLine("Symbol Table:");
            Console.WriteLine("-" + new string('-', 80));
            Console.WriteLine($"{"Name",-15} {"Type",-10} {"Scope",-15} {"Line",-6} {"Offset",-8}");
            Console.WriteLine(new string('-', 80));
            foreach (var entry in symbolTable.Entries)
            {
                Console.WriteLine($"{entry.Name,-15} {entry.DataType,-10} {entry.Scope,-15} {entry.DeclarationLine,-6} {entry.MemoryOffset,-8}");
            }
            Console.WriteLine();
        }

        if (semanticErrors.Count > 0)
        {
            Console.WriteLine("Semantic Errors:");
            Console.WriteLine("-" + new string('-', 80));
            PrintSemanticErrors(semanticErrors);
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("✓ No semantic errors found!");
            Console.WriteLine();
        }

        if (options.Verbose && annotatedAst != null)
        {
            Console.WriteLine("Annotated AST (with types):");
            Console.WriteLine("-" + new string('-', 30));
            PrintAnnotatedAst(annotatedAst, 0);
            Console.WriteLine();
        }
    }

    private static void PrintSemanticErrors(List<SemanticError> errors)
    {
        foreach (var error in errors)
        {
            Console.WriteLine($"  Line {error.Line}, Column {error.Column}: [{error.ErrorType}]");
            Console.WriteLine($"    {error.Message}");

            if (error.VariableName != null)
                Console.WriteLine($"    Variable: '{error.VariableName}'");

            if (error.ExpectedType != null && error.ActualType != null)
                Console.WriteLine($"    Expected type: {error.ExpectedType}, Found: {error.ActualType}");

            Console.WriteLine();
        }
    }

    private static void PrintAnnotatedAst(AnnotatedAstNode node, int depth)
    {
        string indent = new string(' ', depth * 2);
        string typeInfo = node.DataType != null ? $" : {node.DataType}" : "";

        Console.WriteLine(node.Token != null
            ? $"{indent}├─ {node.Rule}: '{node.Token.Value}'{typeInfo}"
            : $"{indent}├─ {node.Rule}{typeInfo}");

        foreach (var child in node.Children)
        {
            PrintAnnotatedAst(child, depth + 1);
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