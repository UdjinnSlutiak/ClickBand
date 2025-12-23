using System.Collections.Generic;
using System.Linq;
using ClickBand.Api.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ClickBand.Api.Services;

public interface IRoomLinkBuilder
{
    string BuildRoomUrl(string roomId, IReadOnlyDictionary<string, string>? query = null);
}

public sealed class RoomLinkBuilder : IRoomLinkBuilder
{
    private readonly IOptions<TelegramOptions> _options;

    public RoomLinkBuilder(IOptions<TelegramOptions> options)
    {
        _options = options;
    }

    public string BuildRoomUrl(string roomId, IReadOnlyDictionary<string, string>? query = null)
    {
        var settings = _options.Value;
        if (string.IsNullOrWhiteSpace(settings.BasePublicUrl))
        {
            return roomId;
        }

        var trimmedBase = settings.BasePublicUrl.TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(settings.RoomPath) ? "/rooms" : settings.RoomPath;
        var baseUrl = $"{trimmedBase}{path.TrimEnd('/')}";

        var mergedQuery = new Dictionary<string, string>
        {
            ["roomId"] = roomId
        };

        if (query is { Count: > 0 })
        {
            foreach (var kvp in query)
            {
                mergedQuery[kvp.Key] = kvp.Value;
            }
        }

        return QueryHelpers.AddQueryString(
            baseUrl,
            mergedQuery.Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value)));
    }
}
