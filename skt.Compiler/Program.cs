using skt.Compiler;
using skt.Shared;

if (args.Length == 0)
{
    Console.WriteLine("Usage: skt.Compiler <source-file> [options]");
    Console.WriteLine("Options:");
    Console.WriteLine("  --tokenize    Generate token file (.sktt) only");
    Console.WriteLine("  --parse       Parse and show AST");
    Console.WriteLine("  --verbose     Show detailed output");
    return;
}

var filePath = args[0];
if (!File.Exists(filePath))
{
    Console.WriteLine($"File not found: {filePath}");
    return;
}

bool tokenizeOnly = args.Contains("--tokenize");
bool parseOnly = args.Contains("--parse");
bool verbose = args.Contains("--verbose");

string source = File.ReadAllText(filePath);

if (verbose)
{
    Console.WriteLine($"Processing file: {filePath}");
    Console.WriteLine($"File size: {source.Length} characters");
    Console.WriteLine();
}

Console.WriteLine("Source Code:");
Console.WriteLine("=" + new string('=', 50));
Console.WriteLine(source);
Console.WriteLine();

var analyzer = new LexicalAnalyzer();

if (tokenizeOnly)
{
    // Generate token file only
    var (tokens, errors) = analyzer.TokenizeToFile(source, filePath);
    
    Console.WriteLine($"Tokenization complete:");
    Console.WriteLine($"  Tokens: {tokens.Count}");
    Console.WriteLine($"  Errors: {errors.Count}");
    
    if (errors.Count > 0)
    {
        Console.WriteLine("\nTokenization Errors:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  Line {error.Line}, Column {error.Column}: {error.Expected} - '{error.Value}'");
        }
    }
}
else
{
    // Full compilation: tokenize and parse
    Console.WriteLine("Step 1: Lexical Analysis");
    Console.WriteLine("-" + new string('-', 30));

    var (tokens, lexErrors) = analyzer.TokenizeToFile(source, filePath);
    Console.WriteLine($"Tokens generated: {tokens.Count}");

    if (lexErrors.Count > 0)
    {
        Console.WriteLine("Lexical Errors:");
        foreach (var error in lexErrors)
        {
            Console.WriteLine($"  Line {error.Line}, Column {error.Column}: {error.Expected} - '{error.Value}'");
        }
        Console.WriteLine();
    }

    if (verbose)
    {
        Console.WriteLine("\nTokens:");
        foreach (var token in tokens.Take(20)) // Show first 20 tokens
        {
            Console.WriteLine($"  {token.Type}: '{token.Value}' (Line {token.Line}, Col {token.Column})");
        }
        if (tokens.Count > 20)
        {
            Console.WriteLine($"  ... and {tokens.Count - 20} more tokens");
        }
        Console.WriteLine();
    }
    
    Console.WriteLine("Step 2: Syntax Analysis");
    Console.WriteLine("-" + new string('-', 30));

    var syntaxAnalyzer = new SyntaxAnalyzer();
    var (ast, parseErrors) = syntaxAnalyzer.Parse(filePath);

    if (parseErrors.Count > 0)
    {
        Console.WriteLine("Parse Errors:");
        foreach (var error in parseErrors)
        {
            Console.WriteLine($"  Line {error.Line}, Column {error.Column}: {error.Message}");
            if (!string.IsNullOrEmpty(error.FoundToken))
            {
                Console.WriteLine($"    Found: '{error.FoundToken}'");
            }
        }
        Console.WriteLine();
    }

    if (ast != null)
    {
        Console.WriteLine("Abstract Syntax Tree:");
        Console.WriteLine("-" + new string('-', 30));
        PrintAST(ast, 0);
        Console.WriteLine();
        Console.WriteLine("Parsing completed successfully!");
    }
    else
    {
        Console.WriteLine("Failed to generate AST due to errors.");
    }
}

static void PrintAST(AstNode node, int depth)
{
    string indent = new string(' ', depth * 2);

    if (node.Token != null)
    {
        // Terminal node
        Console.WriteLine($"{indent}├─ {node.Rule}: '{node.Token.Value}'");
    }
    else
    {
        // Non-terminal node
        Console.WriteLine($"{indent}├─ {node.Rule}");
    }

    for (int i = 0; i < node.Children.Count; i++)
    {
        PrintAST(node.Children[i], depth + 1);
    }
}