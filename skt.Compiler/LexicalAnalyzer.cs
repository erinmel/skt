using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using skt.Shared;

namespace skt.Compiler;

public class LexicalAnalyzer
{
    private string _code = string.Empty;
    private int _position;
    private int _line;
    private int _column;
    private int _codeLength;

    public (List<Token> tokens, List<ErrorToken> errors) Tokenize(string code)
    {
        _code = code;
        _position = 0;
        _line = 1;
        _column = 1;
        _codeLength = code.Length;

        var tokens = new List<Token>();
        var errors = new List<ErrorToken>();

        while (!AtEnd())
        {
            char ch = CurrentChar();

            // Skip whitespace
            if (char.IsWhiteSpace(ch))
            {
                HandleWhitespace();
                continue;
            }

            // Try to tokenize different types
            if (TryTokenizeIdentifierOrKeyword(tokens)) continue;
            if (TryTokenizeNumber(tokens)) continue;
            if (TryTokenizeString(tokens, errors)) continue;
            if (TryTokenizeComment(tokens)) continue;
            if (TryTokenizeOperator(tokens)) continue;

            // Invalid character - add error and advance
            AddError(errors, "valid token", ch.ToString());
            Advance();
        }

        return (tokens, errors);
    }

    private bool AtEnd() => _position >= _codeLength;
    private char CurrentChar() => AtEnd() ? '\0' : _code[_position];
    private char PeekChar(int offset = 1) => _position + offset >= _codeLength ? '\0' : _code[_position + offset];

    private void Advance()
    {
        if (!AtEnd())
        {
            if (CurrentChar() == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            _position++;
        }
    }

    private void HandleWhitespace()
    {
        while (!AtEnd() && char.IsWhiteSpace(CurrentChar()))
        {
            Advance();
        }
    }

    private bool TryTokenizeIdentifierOrKeyword(List<Token> tokens)
    {
        char ch = CurrentChar();
        if (!char.IsLetter(ch) && ch != '_') return false;

        var (startLine, startColumn) = (_line, _column);
        int startPosition = _position;

        // Consume identifier characters
        while (!AtEnd() && (char.IsLetterOrDigit(CurrentChar()) || CurrentChar() == '_'))
        {
            Advance();
        }

        string value = _code[startPosition.._position];
        TokenType tokenType = DetermineIdentifierType(value);

        tokens.Add(new Token(tokenType, value, startLine, startColumn, _line, _column));
        return true;
    }

    private TokenType DetermineIdentifierType(string value)
    {
        return value switch
        {
            "true" or "false" => TokenType.BOOLEAN,
            _ when TokenConstants.Keywords.Contains(value) => TokenType.RESERVED_WORD,
            _ => TokenType.IDENTIFIER
        };
    }

    private bool TryTokenizeNumber(List<Token> tokens)
    {
        if (!char.IsDigit(CurrentChar())) return false;

        var (startLine, startColumn) = (_line, _column);
        int startPosition = _position;

        // Consume integer part
        while (!AtEnd() && char.IsDigit(CurrentChar()))
        {
            Advance();
        }

        // Check for decimal point followed by digits
        bool isReal = false;
        if (!AtEnd() && CurrentChar() == '.' && char.IsDigit(PeekChar()))
        {
            isReal = true;
            Advance(); // Skip the dot

            while (!AtEnd() && char.IsDigit(CurrentChar()))
            {
                Advance();
            }
        }

        string value = _code[startPosition.._position];
        TokenType tokenType = isReal ? TokenType.REAL : TokenType.INTEGER;

        tokens.Add(new Token(tokenType, value, startLine, startColumn, _line, _column));
        return true;
    }

    private bool TryTokenizeString(List<Token> tokens, List<ErrorToken> errors)
    {
        if (CurrentChar() != '"') return false;

        var (startLine, startColumn) = (_line, _column);
        int startPosition = _position;

        Advance(); // Skip opening quote

        while (!AtEnd() && CurrentChar() != '"')
        {
            if (CurrentChar() == '\\' && !AtEnd())
            {
                Advance(); // Skip escape character
                if (!AtEnd()) Advance(); // Skip escaped character
            }
            else
            {
                Advance();
            }
        }

        if (AtEnd())
        {
            // Unclosed string
            string unfinishedString = _code[startPosition.._position];
            AddError(errors, "unclosed string", unfinishedString);
            return true;
        }

        Advance(); // Skip closing quote
        string value = _code[startPosition.._position];

        tokens.Add(new Token(TokenType.STRING, value, startLine, startColumn, _line, _column));
        return true;
    }

    private bool TryTokenizeComment(List<Token> tokens)
    {
        if (CurrentChar() != '/' || AtEnd()) return false;

        char nextChar = PeekChar();
        if (nextChar == '/')
        {
            return TokenizeSingleLineComment(tokens);
        }
        else if (nextChar == '*')
        {
            return TokenizeMultiLineComment(tokens);
        }

        return false;
    }

    private bool TokenizeSingleLineComment(List<Token> tokens)
    {
        var (startLine, startColumn) = (_line, _column);
        int startPosition = _position;

        Advance(); // Skip first /
        Advance(); // Skip second /

        // Consume until end of line or end of file
        while (!AtEnd() && CurrentChar() != '\n')
        {
            Advance();
        }

        string value = _code[startPosition.._position];
        tokens.Add(new Token(TokenType.COMMENT, value, startLine, startColumn, _line, _column));
        return true;
    }

    private bool TokenizeMultiLineComment(List<Token> tokens)
    {
        var (startLine, startColumn) = (_line, _column);
        int startPosition = _position;

        Advance(); // Skip first /
        Advance(); // Skip *

        while (!AtEnd())
        {
            if (CurrentChar() == '*' && PeekChar() == '/')
            {
                Advance(); // Skip *
                Advance(); // Skip /
                break;
            }
            Advance();
        }

        string value = _code[startPosition.._position];
        tokens.Add(new Token(TokenType.COMMENT, value, startLine, startColumn, _line, _column));
        return true;
    }

    private bool TryTokenizeOperator(List<Token> tokens)
    {
        char ch = CurrentChar();

        // Try multi-character operators first
        if (TokenConstants.MultiCharFirst.Contains(ch) && !AtEnd())
        {
            string twoChar = ch.ToString() + PeekChar();
            if (TokenConstants.MultiCharOperators.TryGetValue(twoChar, out TokenType multiCharType))
            {
                var (startLine, startColumn) = (_line, _column);
                Advance();
                Advance();
                tokens.Add(new Token(multiCharType, twoChar, startLine, startColumn, _line, _column));
                return true;
            }
        }

        // Try single character operators
        if (TokenConstants.OperatorsMap.TryGetValue(ch.ToString(), out TokenType singleCharType))
        {
            var (startLine, startColumn) = (_line, _column);
            Advance();
            tokens.Add(new Token(singleCharType, ch.ToString(), startLine, startColumn, _line, _column));
            return true;
        }

        return false;
    }

    private void AddError(List<ErrorToken> errors, string expected, string found)
    {
        errors.Add(new ErrorToken(
            TokenType.ERROR,
            expected,
            found,
            _line,
            _column,
            _line,
            _column + found.Length
        ));
    }

    public (List<Token> tokens, List<ErrorToken> errors) TokenizeToFile(string code, string filePath)
    {
        var (tokens, errors) = Tokenize(code);
        
        // Exclude comments from the output file
        var filteredTokens = tokens.Where(t => t.Type != TokenType.COMMENT).ToList();

        // Create better filename with timestamp to avoid collisions
        string fileName = CreateUniqueFileName(filePath) + ".sktt";

        // Create output directory if it doesn't exist - ensure it's fully created
        string outputDir = "lexical_output";
        EnsureDirectoryExists(outputDir);

        string outputPath = Path.Combine(outputDir, fileName);
        
        // Use custom binary serialization for maximum performance
        WriteBinaryTokens(outputPath, filteredTokens);

        Console.WriteLine($"Tokens written to: {outputPath}");
        return (tokens, errors);
    }

    private static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);

            // Ensure the directory is actually created before proceeding
            int maxRetries = 10;
            int retryDelay = 10; // milliseconds

            for (int i = 0; i < maxRetries; i++)
            {
                if (Directory.Exists(directoryPath))
                    return;

                Thread.Sleep(retryDelay);
            }

            // If we get here, throw an exception
            throw new IOException($"Failed to create directory: {directoryPath}");
        }
    }

    private static string CreateUniqueFileName(string filePath)
    {
        // Combine file path hash + timestamp for uniqueness
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(filePath));
        string hashHex = Convert.ToHexString(hash).ToLower(); // Use full 64 chars for collision resistance
        // Ensure the directory exists before trying to create the file
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }


        // Add timestamp for collision avoidance
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");

        return $"{hashHex}_{timestamp}";
    }

    private static void WriteBinaryTokens(string filePath, List<Token> tokens)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        // Write magic header and version for file format validation
        writer.Write("SKTT".ToCharArray()); // Magic number
        writer.Write((byte)1); // Version

        // Write token count
        writer.Write(tokens.Count);

        // Write each token in binary format
        foreach (var token in tokens)
        {
            writer.Write((byte)token.Type);
            writer.Write(token.Value);
            writer.Write(token.Line);
            writer.Write(token.Column);
            writer.Write(token.EndLine);
            writer.Write(token.EndColumn);
        }
    }

    public static List<Token> ReadBinaryTokens(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Token file not found: {filePath}");

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        // Validate magic header
        var magic = new string(reader.ReadChars(4));
        if (magic != "SKTT")
            throw new InvalidDataException("Invalid token file format");

        // Read version
        byte version = reader.ReadByte();
        if (version != 1)
            throw new InvalidDataException($"Unsupported token file version: {version}");

        // Read token count
        int tokenCount = reader.ReadInt32();
        var tokens = new List<Token>(tokenCount);

        // Read each token
        for (int i = 0; i < tokenCount; i++)
        {
            var type = (TokenType)reader.ReadByte();
            var value = reader.ReadString();
            var line = reader.ReadInt32();
            var column = reader.ReadInt32();
            var endLine = reader.ReadInt32();
            var endColumn = reader.ReadInt32();

            tokens.Add(new Token(type, value, line, column, endLine, endColumn));
        }

        return tokens;
    }

    // Alternative: MessagePack serialization (if you want to add the NuGet package)
    private static void WriteMessagePackTokens(string filePath, List<Token> tokens)
    {
        // Uncomment if you add MessagePack NuGet package
        // var bytes = MessagePackSerializer.Serialize(tokens);
        // File.WriteAllBytes(filePath, bytes);
    }

    // Fallback: Optimized JSON with compression
    private static void WriteCompressedJsonTokens(string filePath, List<Token> tokens)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
        };

        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(tokens, options);

        // Compress with GZip for smaller files
        using var fileStream = new FileStream(filePath, FileMode.Create);
        using var gzipStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Compress);
        gzipStream.Write(jsonBytes);
    }
}
