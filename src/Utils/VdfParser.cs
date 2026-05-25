namespace ObeliskLauncher.Utils;

class VdfNode
{
    public string? Value { get; set; }
    public Dictionary<string, VdfNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public VdfNode? this[string key] => Children.GetValueOrDefault(key);
}

class VdfScanner(string text)
{
    int _index;

    public string? NextToken()
    {
        while (_index < text.Length)
        {
            char c = text[_index];
            if (char.IsWhiteSpace(c)) { _index++; continue; }
            if (c == '/' && _index + 1 < text.Length && text[_index + 1] == '/')
            {
                _index += 2;
                while (_index < text.Length && text[_index] != '\n' && text[_index] != '\r') _index++;
                continue;
            }
            if (c == '{' || c == '}') { _index++; return c.ToString(); }
            if (c == '"')
            {
                _index++;
                int start = _index;
                while (_index < text.Length)
                {
                    if (text[_index] == '\\' && _index + 1 < text.Length && text[_index + 1] == '"') { _index += 2; }
                    else if (text[_index] == '"') { int end = _index++; return text.Substring(start, end - start).Replace("\\\"", "\""); }
                    else { _index++; }
                }
                return text.Substring(start);
            }
            int us = _index;
            while (_index < text.Length && !char.IsWhiteSpace(text[_index]) && text[_index] != '{' && text[_index] != '}' && text[_index] != '"') _index++;
            return text.Substring(us, _index - us);
        }
        return null;
    }
}

static class VdfParser
{
    public static VdfNode Parse(string text)
    {
        var scanner = new VdfScanner(text);
        var root = new VdfNode();
        string? token;
        while ((token = scanner.NextToken()) != null)
        {
            string key = token;
            string? next = scanner.NextToken();
            if (next == "{") root.Children[key] = ParseNode(scanner);
            else if (next != null) root.Children[key] = new VdfNode { Value = next };
        }
        return root;
    }

    static VdfNode ParseNode(VdfScanner scanner)
    {
        var node = new VdfNode();
        string? token;
        while ((token = scanner.NextToken()) != null)
        {
            if (token == "}") break;
            string key = token;
            string? next = scanner.NextToken();
            if (next == "{") node.Children[key] = ParseNode(scanner);
            else if (next != null) node.Children[key] = new VdfNode { Value = next };
        }
        return node;
    }
}
