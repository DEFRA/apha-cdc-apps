using System.Globalization;

namespace CDC.Auth.Cidm.Claims;

/// <summary>Parses CIDM's colon-delimited "relationships"/"roles" claim values into structured records.</summary>
public static class CidmClaimsMapper
{
    /// <summary>Parses every raw relationship claim value, silently skipping malformed entries.</summary>
    /// <param name="rawRelationships">Raw colon-delimited claim values.</param>
    /// <returns>The successfully parsed relationships.</returns>
    public static IReadOnlyList<RelationshipInfo> ParseRelationships(IEnumerable<string> rawRelationships)
    {
        var results = new List<RelationshipInfo>();
        foreach (var raw in rawRelationships)
        {
            if (TryParseRelationship(raw, out var relationship))
            {
                results.Add(relationship);
            }
        }

        return results;
    }

    /// <summary>Attempts to parse a single colon-delimited relationship claim value.</summary>
    /// <param name="raw">The raw claim value.</param>
    /// <param name="relationship">The parsed relationship, if parsing succeeded.</param>
    /// <returns><c>true</c> if <paramref name="raw"/> was well-formed.</returns>
    public static bool TryParseRelationship(string raw, out RelationshipInfo relationship)
    {
        var parts = raw.Split(':');
        if (parts.Length == 6 &&
            int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var organisationLoa) &&
            int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var relationshipLoa))
        {
            relationship = new RelationshipInfo(parts[0], parts[1], parts[2], organisationLoa, parts[4], relationshipLoa);
            return true;
        }

        relationship = null!;
        return false;
    }

    /// <summary>Parses every raw role claim value, silently skipping malformed entries.</summary>
    /// <param name="rawRoles">Raw colon-delimited claim values.</param>
    /// <returns>The successfully parsed roles.</returns>
    public static IReadOnlyList<RoleInfo> ParseRoles(IEnumerable<string> rawRoles)
    {
        var results = new List<RoleInfo>();
        foreach (var raw in rawRoles)
        {
            if (TryParseRole(raw, out var role))
            {
                results.Add(role);
            }
        }

        return results;
    }

    /// <summary>Attempts to parse a single colon-delimited role claim value.</summary>
    /// <param name="raw">The raw claim value.</param>
    /// <param name="role">The parsed role, if parsing succeeded.</param>
    /// <returns><c>true</c> if <paramref name="raw"/> was well-formed.</returns>
    public static bool TryParseRole(string raw, out RoleInfo role)
    {
        var parts = raw.Split(':');
        if (parts.Length == 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var status))
        {
            role = new RoleInfo(parts[0], parts[1], status);
            return true;
        }

        role = null!;
        return false;
    }
}
