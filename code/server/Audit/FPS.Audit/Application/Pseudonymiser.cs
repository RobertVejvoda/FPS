using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FPS.Audit.Application;

public static class Pseudonymiser
{
    // Known fields whose values are raw user IDs — renamed to *Hash and hashed.
    private static readonly HashSet<string> UserIdFields = new(StringComparer.OrdinalIgnoreCase)
        { "actorId", "requestorId", "userId", "recipientId" };

    // Known list fields whose elements are raw user IDs — renamed to *Hashes and each element hashed.
    private static readonly HashSet<string> UserIdListFields = new(StringComparer.OrdinalIgnoreCase)
        { "affectedRecipientIds", "recipientIds", "userIds" };

    private static readonly JsonSerializerOptions SerialiserOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string? Hash(string? value) =>
        string.IsNullOrEmpty(value)
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    // Serialises the full payload (including AdditionalData extension fields),
    // then replaces known user-ID fields with their SHA-256 hashes.
    // All other fields — including unknown additive fields — are preserved as-is.
    public static JsonObject SanitisePayload(BookingEventPayload payload)
    {
        var node = JsonSerializer.SerializeToNode(payload, SerialiserOptions)!.AsObject();
        return SanitiseObject(node);
    }

    private static JsonObject SanitiseObject(JsonObject source)
    {
        var result = new JsonObject();
        foreach (var (key, value) in source)
        {
            if (UserIdFields.Contains(key))
            {
                var hashKey = key[..^2] + "Hash"; // requestorId → requestorHash
                result[hashKey] = Hash(value?.GetValue<string>());
            }
            else if (UserIdListFields.Contains(key) && value is JsonArray arr)
            {
                var hashKey = key[..^3] + "Hashes"; // affectedRecipientIds → affectedRecipientHashes
                var hashes = new JsonArray();
                foreach (var item in arr)
                    hashes.Add(Hash(item?.GetValue<string>()));
                result[hashKey] = hashes;
            }
            else
            {
                result[key] = value?.DeepClone();
            }
        }
        return result;
    }
}
