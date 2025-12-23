using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ClickBand.Api.Services;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connection;

    public RedisHealthCheck(IConnectionMultiplexer connection)
    {
        _connection = connection;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _connection.GetDatabase();
            var pong = await db.PingAsync();
            return pong < TimeSpan.FromMilliseconds(500)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Redis latency {pong.TotalMilliseconds:0}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis ping failed", ex);
        }
    }
}
