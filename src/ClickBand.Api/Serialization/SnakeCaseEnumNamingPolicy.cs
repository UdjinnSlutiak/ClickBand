using System.Text.Json;
using System.Text;

namespace ClickBand.Api.Serialization;

public sealed class SnakeCaseEnumNamingPolicy : JsonNamingPolicy
{
    public static SnakeCaseEnumNamingPolicy Instance { get; } = new();

    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name.Length + 10);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
