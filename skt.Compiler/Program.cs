using skt.Compiler;
using skt.Shared;

if (args.Length == 0)
{
    Console.WriteLine("Usage: skt.Compiler <source-file> [options]");
    Console.WriteLine("Options:");
    Console.WriteLine("  --tokenize    Generate token file (.sktt)");
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
bool verbose = args.Contains("--verbose");

string source = File.ReadAllText(filePath);

if (verbose)
{
    Console.WriteLine($"Processing file: {filePath}");
    Console.WriteLine($"File size: {source.Length} characters");
    Console.WriteLine();
}

var analyzer = new LexicalAnalyzer();

if (tokenizeOnly)
{
    // Generate token file
    var (tokens, errors) = analyzer.TokenizeToFile(source, filePath);
    
    Console.WriteLine($"Tokenization complete:");
    Console.WriteLine($"  Tokens: {tokens.Count}");
    Console.WriteLine($"  Errors: {errors.Count}");
    
    if (errors.Count > 0)
    {
        Console.WriteLine("\nErrors found:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  Line {error.Line}, Column {error.Column}: {error.Expected} - '{error.Value}'");
        }
    }
}
else
{
    // Full compilation pipeline (placeholder for now)
    var (tokens, errors) = analyzer.Tokenize(source);
    
    if (verbose)
    {
        Console.WriteLine("=== TOKENS ===");
        foreach (var token in tokens.Take(20)) // Show first 20 tokens
        {
            Console.WriteLine($"{token.Type,-20} | {token.Value,-15} | Line {token.Line}, Col {token.Column}");
        }
        if (tokens.Count > 20)
            Console.WriteLine($"... and {tokens.Count - 20} more tokens");
        Console.WriteLine();
    }
    
    Console.WriteLine($"Lexical analysis complete:");
    Console.WriteLine($"  Tokens: {tokens.Count}");
    Console.WriteLine($"  Errors: {errors.Count}");
    
    if (errors.Count > 0)
    {
        Console.WriteLine("\nErrors found:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  Line {error.Line}, Column {error.Column}: {error.Expected} - '{error.Value}'");
        }
        return;
    }
    
    Console.WriteLine("\nNext: Add syntax analysis (parser)");
}