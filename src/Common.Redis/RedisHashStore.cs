using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Common.Redis;

public class RedisHashStore
{
    private static readonly Lazy<ConnectionMultiplexer> _conn =
        new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(_config.ToString()));
    
    private static ConfigurationOptions _config;

    private static ConnectionMultiplexer Connection => _conn.Value;
    
    private readonly ILogger<RedisHashStore> _logger;

    internal IDatabase _db;
    public RedisHashStore(ILogger<RedisHashStore> logger, IOptions<ConfigurationOptions> options)
    {
        _logger = logger;
        _config = options.Value;
        
        Connection.ConnectionFailed += (sender, args) =>
        {
            _logger.LogError($"Redis connection failed: {args.Exception.Message}");
        };
        Connection.ConnectionRestored += (sender, args) => { _logger.LogInformation("Redis connection restored"); };
        Connection.ErrorMessage += (sender, args) => { _logger.LogError($"Redis error: {args.Message}"); };

        _db = Connection.GetDatabase();
    }

    public void SetValues(string redisKey, Dictionary<string, string> values)
    {
        try
        {
            _db.HashSet(redisKey, values.ToRedisHashSetEntries());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting value for redisKey {redisKey}");
            throw;
        }
    }

    public Dictionary<string, string> GetValues(string redisKey)
    {
        try
        {
            var entries = _db.HashGetAll(redisKey);
            var result = new Dictionary<string, string>();
            foreach (var entry in entries)
            {
                result[entry.Name] = entry.Value;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting value for redisKey {redisKey}");
            throw;
        }
    }

    public void AddOrUpdateKey(string redisKey, string key, string value)
    {
        try
        {
            _db.HashSet(redisKey, key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding or updating key for redisKey {redisKey}, key {key}");
            throw;
        }
    }

    public bool KeyExists(string redisKey, string key)
    {
        try
        {
            return _db.HashExists(redisKey, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking key existence for redisKey {redisKey}, key {key}");
            throw;
        }
    }
}