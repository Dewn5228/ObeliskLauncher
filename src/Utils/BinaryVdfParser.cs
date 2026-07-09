using System.Runtime.InteropServices;
using System.Text;

namespace ObeliskLauncher.Utils;

static unsafe class BinaryVdfParser
{
    public static string DumpHex(byte* data, int size, int maxBytes = 256)
    {
        int len = Math.Min(size, maxBytes);
        var sb = new StringBuilder(len * 3 + 10);
        sb.Append($"RawBytes({size}): ");
        for (int i = 0; i < len; i++)
            sb.Append($"{data[i]:X2} ");
        if (size > maxBytes)
            sb.Append($"... (+{size - maxBytes} more)");
        return sb.ToString();
    }

    public static string DumpTree(VdfNode node, string indent = "")
    {
        var sb = new StringBuilder();
        foreach ((string key, VdfNode child) in node.Children)
        {
            sb.Append($"{indent}{key}");
            if (child.Value is not null)
                sb.Append($" = \"{child.Value}\"");
            sb.AppendLine();
            if (child.Children.Count > 0)
                sb.Append(DumpTree(child, indent + "  "));
        }
        return sb.ToString();
    }

    public static VdfNode Parse(byte* data, int size)
    {
        var root = new VdfNode();
        int offset = 0;
        while (offset < size)
        {
            if (!TryParseNode(data, size, ref offset, out string? key, out VdfNode? node))
                break;
            if (key is not null && node is not null)
                root.Children[key] = node;
        }
        return root;
    }

    static bool TryParseNode(byte* data, int size, ref int offset, out string? key, out VdfNode? node)
    {
        key = null;
        node = null;

        if (offset >= size)
            return false;

        byte type = data[offset++];

        if (type == 0x06)
            return false;

        if (!TryReadString(data, size, ref offset, out key))
            return false;

        switch (type)
        {
            case 0x00:
            {
                node = new VdfNode();
                while (offset < size)
                {
                    if (!TryParseNode(data, size, ref offset, out string? childKey, out VdfNode? childNode))
                        break;
                    if (childKey is not null && childNode is not null)
                        node.Children[childKey] = childNode;
                }
                return true;
            }
            case 0x01:
            {
                if (TryReadString(data, size, ref offset, out string? value))
                {
                    node = new VdfNode { Value = value };
                    return true;
                }
                return false;
            }
            case 0x02:
            {
                if (offset + 4 <= size)
                {
                    int val = *(int*)(data + offset);
                    offset += 4;
                    node = new VdfNode { Value = val.ToString() };
                    return true;
                }
                return false;
            }
            case 0x05:
            {
                if (offset + 8 <= size)
                {
                    long val = *(long*)(data + offset);
                    offset += 8;
                    node = new VdfNode { Value = val.ToString() };
                    return true;
                }
                return false;
            }
            case 0x07:
            case 0x08:
            {
                if (offset + 1 <= size)
                {
                    node = new VdfNode { Value = data[offset].ToString() };
                    offset += 1;
                    return true;
                }
                return false;
            }
            default:
                return false;
        }
    }

    static bool TryReadString(byte* data, int size, ref int offset, out string? value)
    {
        int start = offset;
        while (offset < size && data[offset] != 0)
            offset++;

        if (offset >= size)
        {
            value = null;
            return false;
        }

        value = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data + start, offset - start));
        offset++;
        return true;
    }
}
