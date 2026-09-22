using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;

namespace InventorModel.Addin;

internal static class JsonDisplayFormatter
{
    private static readonly JavaScriptSerializer Json =
        new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    public static string Format(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "{}";

        try
        {
            object? value = Json.DeserializeObject(json);
            return FormatValue(value);
        }
        catch
        {
            return json.Trim();
        }
    }

    public static string FormatObject(object? value)
    {
        if (value == null)
            return "null";

        try
        {
            return Format(Json.Serialize(value));
        }
        catch
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }

    private static string FormatValue(object? value)
    {
        var builder = new StringBuilder();
        WriteValue(builder, value, 0);
        return builder.ToString();
    }

    private static void WriteValue(StringBuilder builder, object? value, int depth)
    {
        if (value == null)
        {
            builder.Append("null");
            return;
        }

        if (value is IDictionary<string, object> dictionary)
        {
            WriteDictionary(builder, dictionary, depth);
            return;
        }

        if (value is IDictionary nonGeneric)
        {
            var converted = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in nonGeneric)
                converted[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] =
                    entry.Value;
            WriteDictionary(builder, converted, depth);
            return;
        }

        if (value is IEnumerable sequence && !(value is string))
        {
            WriteArray(builder, sequence, depth);
            return;
        }

        if (value is string text)
        {
            builder.Append(Json.Serialize(text));
            return;
        }

        if (value is bool boolean)
        {
            builder.Append(boolean ? "true" : "false");
            return;
        }

        builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static void WriteDictionary(
        StringBuilder builder,
        IDictionary<string, object> dictionary,
        int depth)
    {
        builder.Append('{');
        if (dictionary.Count == 0)
        {
            builder.Append('}');
            return;
        }

        builder.AppendLine();
        int index = 0;
        foreach (KeyValuePair<string, object> pair in dictionary)
        {
            Indent(builder, depth + 1);
            builder.Append(Json.Serialize(pair.Key));
            builder.Append(": ");
            WriteValue(builder, pair.Value, depth + 1);

            if (++index < dictionary.Count)
                builder.Append(',');
            builder.AppendLine();
        }

        Indent(builder, depth);
        builder.Append('}');
    }

    private static void WriteArray(StringBuilder builder, IEnumerable sequence, int depth)
    {
        var items = new List<object>();
        foreach (object item in sequence)
            items.Add(item);

        builder.Append('[');
        if (items.Count == 0)
        {
            builder.Append(']');
            return;
        }

        builder.AppendLine();
        for (int i = 0; i < items.Count; i++)
        {
            Indent(builder, depth + 1);
            WriteValue(builder, items[i], depth + 1);
            if (i + 1 < items.Count)
                builder.Append(',');
            builder.AppendLine();
        }

        Indent(builder, depth);
        builder.Append(']');
    }

    private static void Indent(StringBuilder builder, int depth) =>
        builder.Append(' ', depth * 2);
}
