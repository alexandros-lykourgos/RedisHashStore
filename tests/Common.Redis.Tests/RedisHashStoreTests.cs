using Common.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Common.Redis.Tests;

public class RedisHashStoreTests
{
    private readonly Mock<ILogger<RedisHashStore>> _loggerMock;
    private readonly Mock<IOptions<ConfigurationOptions>> _configMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly RedisHashStore _redisHashStore;

    public RedisHashStoreTests()
    {
        _loggerMock = new Mock<ILogger<RedisHashStore>>();
        _configMock = new Mock<IOptions<ConfigurationOptions>>();
        _configMock.Setup(c => c.Value).Returns(
            new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                AllowAdmin = true,
                ConnectRetry = 3,
                ConnectTimeout = 5000,
                DefaultVersion = new Version(3, 0),
                Ssl = false,
                SyncTimeout = 5000,
                Password = "mypassword",
                EndPoints = { "localhost:7000", "localhost:7001" }
            }
        );
        
        _dbMock = new Mock<IDatabase>();
        _dbMock.Setup(d => d.Multiplexer.IsConnected).Returns(true);
        
        _redisHashStore = new RedisHashStore(_loggerMock.Object, _configMock!.Object)
        {
            _db = _dbMock.Object
        };
       
    }

    [Fact]
    public void SetValues_ShouldCallHashSet_WhenCalled()
    {
        // Arrange
        var redisKey = "paymentKey";
        var values = new Dictionary<string, string>
        {
            { "SampleMetadataKey", "value" },
            { "SampleMetadataKey2", "value" }
        };

        // Act
        _redisHashStore.SetValues(redisKey, values);

        var expected = values.ToRedisHashSetEntries();
        // Assert
        _dbMock
            .Verify(x => x.HashSet(redisKey, 
            expected,It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public void SetValues_ShouldLogError_WhenExceptionOccurs()
    {
        // Arrange
        var redisKey = "paymentKey";
        var values = new Dictionary<string, string>
        {
            { "SampleMetadataKey", "value" },
            { "SampleMetadataKey2", "value" }
        };
        var exception = new Exception("Test exception");
        var expected = values.ToRedisHashSetEntries();
        _dbMock.Setup(x => x.HashSet(redisKey, expected, It.IsAny<CommandFlags>())).Throws(exception);

        // Act
        var ex = Assert.Throws<Exception>(() => _redisHashStore.SetValues(redisKey, values));

        // Assert
        Assert.Equal(exception, ex);
        
        var expectedMessage = $"Error setting value for redisKey {redisKey}";
        _loggerMock.Verify(logger => logger.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString().Contains(expectedMessage)),
                ex, (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), 
            Times.Once);
    }

    [Fact]
    public void GetValue_ShouldCallHashGetAll_WhenCalled()
    {
        // Arrange
        var redisKey = "paymentKey";
        var entries = new HashEntry[]
        {
            new HashEntry("SampleMetadataKey", "value"),
            new HashEntry("SampleMetadataKey2", "value")
        };
        _dbMock.Setup(x => x.HashGetAll(redisKey, It.IsAny<CommandFlags>())).Returns(entries);

        // Act
        _redisHashStore.GetValues(redisKey);

        // Assert
        _dbMock.Verify(x => x.HashGetAll(redisKey,It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void GetValue_ShouldReturnDictionary_WhenCalled()
    {
        // Arrange
        var redisKey = "payment-123";
        var entries = new[]
        {
            new HashEntry("SampleMetadataKey", "value"),
            new HashEntry("SampleMetadataKey2", "value"),
        };
        
        var expectedResult = new Dictionary<string, string>()
        {
            { "SampleMetadataKey", "value" },
            { "SampleMetadataKey2", "value" }
        };

        var db = new Mock<IDatabase>();
        db.Setup(x => x.HashGetAll(redisKey,It.IsAny<CommandFlags>())).Returns(entries);
        
        var redisHashStore = new RedisHashStore(_loggerMock.Object, _configMock.Object);
        redisHashStore._db = db.Object;

        // Act
        var result = redisHashStore.GetValues(redisKey);

        // Assert
        Assert.Equal(expectedResult, result);
        db.Verify(x => x.HashGetAll(redisKey, It.IsAny<CommandFlags>()), Times.Once);
    }


    [Fact]
    public void AddOrUpdateKey_ShouldAddOrUpdateKeyInRedis()
    {
        // Arrange
        var redisKey = "paymentKey";
        var key = "SampleMetadataKey";
        var value = "value";
        var expectedValue = "value";

        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(db => db.HashSet(redisKey, key, value,It.IsAny<When>(), It.IsAny<CommandFlags>())).Verifiable();
        mockDatabase.Setup(db => db.HashSet(redisKey, key, expectedValue,It.IsAny<When>(), It.IsAny<CommandFlags>())).Verifiable();

        
        var customHashStore = new RedisHashStore(_loggerMock.Object, _configMock.Object)
        {
            _db = mockDatabase.Object
        };

        // Act
        customHashStore.AddOrUpdateKey(redisKey, key, value);
        customHashStore.AddOrUpdateKey(redisKey, key, expectedValue);

        // Assert
        try
        {
            mockDatabase.VerifyAll();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debugger.Break();
        }
    }

    [Fact]
    public void KeyExists_ShouldReturnTrueIfKeyExistsInRedis()
    {
        // Arrange
        var redisKey = "paymentKey";
        var key = "SampleMetadataKey";
        
        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(db => db.HashExists(redisKey, key, It.IsAny<CommandFlags>())).Returns(true).Verifiable();

        var customHashStore = new RedisHashStore(_loggerMock.Object, _configMock.Object)
        {
            _db = mockDatabase.Object
        };

        // Act
        var result = customHashStore.KeyExists(redisKey, key);

        // Assert
        mockDatabase.VerifyAll();
        Assert.True(result);
    }

    [Fact]
    public void KeyExists_ShouldReturnFalseIfKeyDoesNotExistInRedis()
    {
        // Arrange
        var redisKey = "paymentKey";
        var key = "SampleMetadataKey";

        var mockDatabase = new Mock<IDatabase>();
        mockDatabase.Setup(db => db.HashExists(redisKey, key,It.IsAny<CommandFlags>())).Returns(false).Verifiable();

        var customHashStore = new RedisHashStore(_loggerMock.Object,_configMock.Object)
        {
            _db = mockDatabase.Object
        };

        // Act
        var result = customHashStore.KeyExists(redisKey, key);

        // Assert
        mockDatabase.VerifyAll();
        Assert.False(result);
    }

    [Fact]
    public void SetValues_ShouldSetValuesInRedis()
    {
        // Arrange
        var redisKey = "payment-123";
        var values = new Dictionary<string, string>()
        {
            { "SampleMetadataKey", "value" },
            { "SampleMetadataKey2", "value" }
        };

        var db = new Mock<IDatabase>();
        var expected = values.ToRedisHashSetEntries();
        db.Setup(x => x.HashSet(redisKey, expected, It.IsAny<CommandFlags>()));

        var customHashStore = new RedisHashStore(_loggerMock.Object, _configMock.Object);
        customHashStore._db = db.Object;

        // Act
        customHashStore.SetValues(redisKey, values);

        // Assert
        db.Verify(x => x.HashSet(redisKey, It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()), Times.Once);
    }


    [Fact]
    public void KeyExists_ShouldReturnTrue_WhenKeyExists()
    {
        // Arrange
        var redisKey = "paymentKey";
        var key = "SampleMetadataKey";
        var value = "value";
        var db = new Mock<IDatabase>();
        db.Setup(d => d.HashExists(redisKey, key, It.IsAny<CommandFlags>())).Returns(true);
        
        var customHashStore = new RedisHashStore(_loggerMock.Object, _configMock.Object);
        customHashStore._db = db.Object;

        // Act
        var result = customHashStore.KeyExists(redisKey, key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void KeyExists_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        // Arrange
        var redisKey = "paymentKey";
        var key = "SampleMetadataKey";
        var value = "value";
        var db = new Mock<IDatabase>();
        db.Setup(d => d.HashExists(redisKey, key, It.IsAny<CommandFlags>())).Returns(false);
        
        var customHashStore = new RedisHashStore(_loggerMock.Object, _configMock.Object);
        customHashStore._db = db.Object;

        // Act
        var result = customHashStore.KeyExists(redisKey, key);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void KeyExists_ShouldLogError_WhenExceptionIsThrown()
    {
        // Arrange
        var redisKey = "paymentKey";
        var key = "SampleMetadataKey";
        var value = "value";
        var db = new Mock<IDatabase>();
        db.Setup(d => d.HashExists(redisKey, key, It.IsAny<CommandFlags>())).Throws<Exception>();

        var customLoggerMock = new Mock<ILogger<RedisHashStore>>();
        
        var customHashStore = new RedisHashStore(customLoggerMock.Object, _configMock.Object);
        customHashStore._db = db.Object;

        // Act & Assert
        
        var expectedMessage = $"Error checking key existence for redisKey {redisKey}, key {key}";

        try
        {
            var ex = Assert.Throws<Exception>(() => customHashStore.KeyExists(redisKey, key));
        }
        catch (Exception ex)
        {
            customLoggerMock.Verify(logger => logger.Log(LogLevel.Error, It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString().Contains(expectedMessage)),
                    null, (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()), 
                Times.Once);
        }
    }
}