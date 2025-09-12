using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using skt.Shared;

namespace skt.Compiler;

public class LexicalAnalyzer
{
    public (List<Token> tokens, List<ErrorToken> errors) Tokenize(string code)
    {
        var tokens = new List<Token>();
        var errors = new List<ErrorToken>();
        int i = 0;
        int line = 1;
        int column = 1;
        int codeLen = code.Length;

        while (i < codeLen)
        {
            char ch = code[i];

            // Handle whitespace
            if (char.IsWhiteSpace(ch))
            {
                if (ch == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
                i++;
                continue;
            }

            // Identifier, keyword or boolean
            if (char.IsLetter(ch) || ch == '_')
            {
                int startI = i, startCol = column, startLine = line;
                i++;
                column++;
                
                while (i < codeLen && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                {
                    i++;
                    column++;
                }

                string val = code[startI..i];
                TokenType tokenType;
                
                if (val == "true" || val == "false")
                    tokenType = TokenType.BOOLEAN;
                else if (TokenConstants.Keywords.Contains(val))
                    tokenType = TokenType.RESERVED_WORD;
                else
                    tokenType = TokenType.IDENTIFIER;
                
                tokens.Add(new Token(tokenType, val, startLine, startCol, line, column));
                continue;
            }

            // Numbers (integers and reals)
            if (char.IsDigit(ch))
            {
                int startI = i, startCol = column, startLine = line;
                
                while (i < codeLen && char.IsDigit(code[i]))
                {
                    i++;
                    column++;
                }

                // Check for decimal point
                if (i + 1 < codeLen && code[i] == '.' && char.IsDigit(code[i + 1]))
                {
                    i++; // skip the dot
                    column++;
                    
                    while (i < codeLen && char.IsDigit(code[i]))
                    {
                        i++;
                        column++;
                    }
                    
                    tokens.Add(new Token(TokenType.REAL, code[startI..i], startLine, startCol, line, column));
                }
                else
                {
                    tokens.Add(new Token(TokenType.INTEGER, code[startI..i], startLine, startCol, line, column));
                }
                continue;
            }

            // Strings
            if (ch == '"')
            {
                int startI = i, startCol = column, startLine = line;
                i++; // Skip opening quote
                column++;

                bool escapeMode = false;

                while (i < codeLen)
                {
                    if (escapeMode)
                    {
                        escapeMode = false;
                    }
                    else if (code[i] == '\\')
                    {
                        escapeMode = true;
                    }
                    else if (code[i] == '"')
                    {
                        break;
                    }

                    if (code[i] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                    i++;
                }

                if (i < codeLen) // Found closing quote
                {
                    i++; // Include closing quote
                    column++;
                    tokens.Add(new Token(TokenType.STRING, code[startI..i], startLine, startCol, line, column));
                }
                else // Unclosed string
                {
                    errors.Add(new ErrorToken(TokenType.ERROR, "unclosed string", code[startI..i], startLine, startCol, line, column));
                }
                continue;
            }

            // Comments
            if (ch == '/' && i + 1 < codeLen)
            {
                char nextChar = code[i + 1];

                // Line comment
                if (nextChar == '/')
                {
                    int startI = i, startCol = column, startLine = line;
                    i += 2; // Skip //
                    column += 2;

                    int newlinePos = code.IndexOf('\n', i);
                    if (newlinePos == -1)
                        i = codeLen;
                    else
                        i = newlinePos;

                    tokens.Add(new Token(TokenType.COMMENT, code[startI..i], startLine, startCol, line, column + (i - startI - 2)));
                    continue;
                }

                // Multi-line comment
                if (nextChar == '*')
                {
                    int startI = i, startCol = column, startLine = line;
                    i += 2; // Skip /*
                    column += 2;

                    while (i < codeLen - 1)
                    {
                        if (code[i] == '*' && code[i + 1] == '/')
                        {
                            i += 2;
                            column += 2;
                            break;
                        }

                        if (code[i] == '\n')
                        {
                            line++;
                            column = 1;
                        }
                        else
                        {
                            column++;
                        }
                        i++;
                    }

                    tokens.Add(new Token(TokenType.COMMENT, code[startI..i], startLine, startCol, line, column));
                    continue;
                }
            }

            // Multi-character operators
            if (TokenConstants.MultiCharFirst.Contains(ch) && i + 1 < codeLen)
            {
                string twoChar = ch.ToString() + code[i + 1];
                if (TokenConstants.MultiCharOperators.TryGetValue(twoChar, out TokenType tokenType))
                {
                    tokens.Add(new Token(tokenType, twoChar, line, column, line, column + 2));
                    i += 2;
                    column += 2;
                    continue;
                }
            }

            // Single character operators
            if (TokenConstants.OperatorsMap.TryGetValue(ch.ToString(), out TokenType singleTokenType))
            {
                tokens.Add(new Token(singleTokenType, ch.ToString(), line, column, line, column + 1));
                i++;
                column++;
                continue;
            }

            // Error - invalid token
            errors.Add(new ErrorToken(TokenType.ERROR, "valid token", ch.ToString(), line, column, line, column + 1));
            i++;
            column++;
        }

        return (tokens, errors);
    }

    public (List<Token> tokens, List<ErrorToken> errors) TokenizeToFile(string code, string filePath)
    {
        var (tokens, errors) = Tokenize(code);
        
        // Exclude comments
        var filteredTokens = tokens.Where(t => t.Type != TokenType.COMMENT).ToList();

        // Create hash of file path for output filename
        string fileName = CreateHashedFileName(filePath) + ".sktt";
        
        // Create output directory if it doesn't exist
        string outputDir = "lexical_output";
        Directory.CreateDirectory(outputDir);
        
        string outputPath = Path.Combine(outputDir, fileName);
        
        // Serialize tokens to binary file using System.Text.Json
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(filteredTokens);
        File.WriteAllBytes(outputPath, jsonBytes);

        Console.WriteLine($"Tokens written to: {outputPath}");
        return (tokens, errors);
    }

    private static string CreateHashedFileName(string filePath)
    {
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(filePath));
        return Convert.ToHexString(hash).ToLower();
    }
}
