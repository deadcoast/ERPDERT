namespace Dert.Core;

/// <summary>Lossless punctuation candidates; note roles are resolved by the parser.</summary>
public static class DertLexer
{
    public static IReadOnlyList<LexToken> Lex(string text)
    {
        var tokens = new List<LexToken>();
        for (var i = 0; i < text.Length;)
        {
            var start = i;
            var kind = KindAt(text, i);
            i++;
            switch (kind)
            {
                case TokenKind.NewLine:
                    if (text[start] == '\r' && i < text.Length && text[i] == '\n') i++;
                    break;
                case TokenKind.Slashes:
                case TokenKind.Line:
                    i++;
                    break;
                case TokenKind.Text:
                case TokenKind.Whitespace:
                    while (i < text.Length && KindAt(text, i) == kind) i++;
                    break;
            }
            tokens.Add(new(kind, new(start, i - start)));
        }
        return tokens.ToArray();
    }

    private static TokenKind KindAt(string text, int i) => text[i] switch
    {
        '\uFEFF' => TokenKind.Bom,
        '\r' or '\n' => TokenKind.NewLine,
        ';' when i + 1 < text.Length && text[i + 1] == ';' => TokenKind.Line,
        ';' => TokenKind.Hook,
        '/' when i + 1 < text.Length && text[i + 1] == '/' => TokenKind.Slashes,
        ',' => TokenKind.Comma,
        _ when char.IsWhiteSpace(text[i]) => TokenKind.Whitespace,
        _ => TokenKind.Text
    };
}
