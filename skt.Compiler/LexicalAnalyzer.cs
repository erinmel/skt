using System.Security.Cryptography;
using System.Text;
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
        if (AtEnd()) return;
        char ch = _code[_position];

        // Normalize Windows CRLF to a single newline movement
        if (ch == '\r')
        {
            if (_position + 1 < _codeLength && _code[_position + 1] == '\n')
            {
                _position += 2;
            }
            else
            {
                _position++;
            }
            _line++;
            _column = 1;
            return;
        }

        if (ch == '\n')
        {
            _position++;
            _line++;
            _column = 1;
            return;
        }

        if (ch == '\t')
        {
            _position++;
            int tabWidth = 4;
            int zeroBased = _column - 1;
            int nextStop = ((zeroBased / tabWidth) + 1) * tabWidth;
            _column = nextStop + 1;
            return;
        }

        _position++;
        _column++;
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

    private static TokenType DetermineIdentifierType(string value)
    {
        return value switch
        {
            "true" or "false" => TokenType.Boolean,
            _ when TokenConstants.Keywords.Contains(value) => TokenType.ReservedWord,
            _ => TokenType.Identifier
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
        TokenType tokenType = isReal ? TokenType.Real : TokenType.Integer;

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

        tokens.Add(new Token(TokenType.String, value, startLine, startColumn, _line, _column));
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
        tokens.Add(new Token(TokenType.Comment, value, startLine, startColumn, _line, _column));
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
        tokens.Add(new Token(TokenType.Comment, value, startLine, startColumn, _line, _column));
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
            TokenType.Error,
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
        // Deterministic output: hash of full path, overwrite each run, exclude comments
        var (tokens, errors) = Tokenize(code);
        var filteredTokens = tokens.Where(t => t.Type != TokenType.Comment).ToList();
        string outputDir = Path.Combine(Path.GetTempPath(), "skt/lexical");
        EnsureDirectoryExists(outputDir);
        string hashFileName = ComputePathHash(filePath) + ".sktt";
        string outputPath = Path.Combine(outputDir, hashFileName);
        WriteBinaryTokens(outputPath, filteredTokens);
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

    private static string ComputePathHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(filePath));
        return Convert.ToHexString(hash).ToLower();
    }

    private static void WriteBinaryTokens(string filePath, List<Token> tokens)
    {
        // Ensure parent directory exists and handle potential concurrent deletions
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        int maxRetries = 3;
        int delayMs = 50;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
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

                // Success: exit method
                return;
            }
            catch (DirectoryNotFoundException)
            {
                if (attempt == maxRetries - 1) throw;
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                Thread.Sleep(delayMs);
                delayMs *= 2;
            }
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
}
