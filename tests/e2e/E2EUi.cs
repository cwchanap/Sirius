using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GodotE2E;

internal readonly record struct E2EVector2I(int X, int Y);

internal static class E2EUi
{
    internal static async Task<IReadOnlyList<string>> FindNodesAsync(
        E2EGame game, string by, string value, string? type = null, string startPath = "/root")
    {
        var query = new Dictionary<string, object?>
        {
            ["by"] = by,
            ["value"] = value,
        };
        if (type is not null)
        {
            query["filters"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["by"] = "type",
                    ["value"] = type,
                }
            };
        }

        var result = await game.SendCommandAsync(
            "find_nodes",
            new Dictionary<string, JsonElement>
            {
                ["query"] = JsonSerializer.SerializeToElement(query),
                ["start_path"] = JsonSerializer.SerializeToElement(startPath),
            });
        RequireSuccess(result);

        return result.Value.GetProperty("nodes")
            .EnumerateArray()
            .Select(node => node.GetString()
                ?? throw new E2EException("find_nodes returned a node without a path"))
            .ToArray();
    }

    internal static async Task<string> FindExactlyOneAsync(
        E2EGame game, string by, string value, string? type = null, string startPath = "/root")
    {
        var nodes = await FindNodesAsync(game, by, value, type, startPath);
        return nodes.Count == 1
            ? nodes[0]
            : throw new E2EException(
                $"Expected exactly one node for {by}={value}, found {nodes.Count}");
    }

    internal static async Task<string> FindExactlyOneVisibleAsync(
        E2EGame game, string by, string value, string? type = null, string startPath = "/root")
    {
        var nodes = await FindNodesAsync(game, by, value, type, startPath);
        var visibleNodes = new List<string>();
        foreach (var node in nodes)
        {
            if (await game.CallMethodAsync<bool>(node, "is_visible_in_tree") == true)
                visibleNodes.Add(node);
        }

        return visibleNodes.Count == 1
            ? visibleNodes[0]
            : throw new E2EException(
                $"Expected exactly one visible node for {by}={value}, found {visibleNodes.Count}");
    }

    internal static async Task WaitForNodeAsync(E2EGame game, string path, double timeoutSeconds)
    {
        var result = await game.SendCommandAsync(
            "wait_for_node",
            new Dictionary<string, JsonElement>
            {
                ["path"] = JsonSerializer.SerializeToElement(path),
                ["timeout"] = JsonSerializer.SerializeToElement(timeoutSeconds),
            },
            TimeSpan.FromSeconds(timeoutSeconds + 1));
        RequireSuccess(result);
    }

    internal static async Task<E2EVector2I> GetVector2IPropertyAsync(
        E2EGame game, string path, string property)
    {
        var result = await game.SendCommandAsync(
            "get_property",
            new Dictionary<string, JsonElement>
            {
                ["path"] = JsonSerializer.SerializeToElement(path),
                ["property"] = JsonSerializer.SerializeToElement(property),
            });
        RequireSuccess(result);
        return ParseVector2I(result.Value.GetProperty("result"));
    }

    internal static async Task<E2EVector2I> CallVector2IMethodAsync(
        E2EGame game, string path, string method, params JsonElement[] args)
    {
        var result = await game.SendCommandAsync(
            "call_method",
            new Dictionary<string, JsonElement>
            {
                ["path"] = JsonSerializer.SerializeToElement(path),
                ["method"] = JsonSerializer.SerializeToElement(method),
                ["args"] = JsonSerializer.SerializeToElement(args),
            });
        RequireSuccess(result);
        return ParseVector2I(result.Value.GetProperty("result"));
    }

    internal static JsonElement Vector2IArgument(int x, int y) =>
        JsonSerializer.SerializeToElement(new { _t = "v2i", x, y });

    private static void RequireSuccess(E2EResult result)
    {
        if (!result.Success)
            throw new E2EException(result.Message);
    }

    private static E2EVector2I ParseVector2I(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 3
            || !value.TryGetProperty("_t", out var tag)
            || tag.ValueKind != JsonValueKind.String || tag.GetString() != "v2i"
            || !value.TryGetProperty("x", out var x) || !x.TryGetInt32(out var xValue)
            || !value.TryGetProperty("y", out var y) || !y.TryGetInt32(out var yValue))
            throw new E2EException(
                "Expected a Godot Vector2I value in the {_t:v2i,x:int,y:int} format");

        return new E2EVector2I(xValue, yValue);
    }
}
