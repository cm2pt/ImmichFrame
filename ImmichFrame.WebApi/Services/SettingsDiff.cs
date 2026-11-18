using System.Text.Json;
using System.Text.Json.Nodes;

namespace ImmichFrame.WebApi.Services;

/// <summary>
/// Describes what changed between two raw settings documents in short, human lines for the
/// admin's history ("Layout: single → splitview", "Account 2 added"). Secret values never
/// appear: a changed secret is reported as "ApiKey changed".
/// </summary>
public static class SettingsDiff
{
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApiKey", "AuthenticationSecret", "AdminPassword", "WeatherApiKey"
    };

    private const int MaxValueLength = 40;

    public static List<string> Describe(string? previousJson, string currentJson)
    {
        var changes = new List<string>();
        var current = Parse(currentJson);
        var previous = Parse(previousJson);

        DescribeObject(previous?["General"] as JsonObject, current?["General"] as JsonObject, prefix: "", changes);

        var oldAccounts = previous?["Accounts"] as JsonArray ?? new JsonArray();
        var newAccounts = current?["Accounts"] as JsonArray ?? new JsonArray();
        var common = Math.Min(oldAccounts.Count, newAccounts.Count);
        for (var i = 0; i < common; i++)
        {
            DescribeObject(oldAccounts[i] as JsonObject, newAccounts[i] as JsonObject, $"Account {i + 1}: ", changes);
        }

        for (var i = common; i < newAccounts.Count; i++)
        {
            changes.Add($"Account {i + 1} added");
        }

        for (var i = common; i < oldAccounts.Count; i++)
        {
            changes.Add($"Account {i + 1} removed");
        }

        return changes;
    }

    private static void DescribeObject(JsonObject? before, JsonObject? after, string prefix, List<string> changes)
    {
        var keys = new List<string>();
        foreach (var (key, _) in after ?? new JsonObject()) keys.Add(key);
        foreach (var (key, _) in before ?? new JsonObject())
        {
            if (!keys.Contains(key)) keys.Add(key);
        }

        foreach (var key in keys)
        {
            var oldValue = before?[key];
            var newValue = after?[key];
            if (JsonNode.DeepEquals(oldValue, newValue) || (IsEmpty(oldValue) && IsEmpty(newValue)))
            {
                continue;
            }

            if (SecretKeys.Contains(key))
            {
                changes.Add($"{prefix}{key} {(IsEmpty(newValue) ? "removed" : IsEmpty(oldValue) ? "set" : "changed")}");
            }
            else if (oldValue is JsonArray || newValue is JsonArray || oldValue is JsonObject || newValue is JsonObject)
            {
                changes.Add($"{prefix}{key} changed");
            }
            else
            {
                changes.Add($"{prefix}{key}: {Format(oldValue)} → {Format(newValue)}");
            }
        }
    }

    private static bool IsEmpty(JsonNode? node) =>
        node == null
        || (node is JsonValue v && v.TryGetValue<string>(out var s) && string.IsNullOrEmpty(s))
        || (node is JsonArray a && a.Count == 0);

    private static string Format(JsonNode? node)
    {
        if (IsEmpty(node)) return "(empty)";
        var text = node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node!.ToJsonString();
        return text.Length > MaxValueLength ? text[..(MaxValueLength - 1)] + "…" : text;
    }

    private static JsonNode? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
