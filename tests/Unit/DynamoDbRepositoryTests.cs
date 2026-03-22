using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Unit;

public class DynamoDbRepositoryTests
{
    private readonly Mock<IAmazonDynamoDB> _dbMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly DynamoDbConversationStateRepository _repository;

    public DynamoDbRepositoryTests()
    {
        _dbMock = new Mock<IAmazonDynamoDB>();
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["DynamoDB:Table"]).Returns("test_table");
        _repository = new DynamoDbConversationStateRepository(_dbMock.Object, _configMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_UsesCorrectKeys()
    {
        // Arrange
        var convId = "123-456";
        _dbMock.Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), default))
               .ReturnsAsync(new GetItemResponse { IsItemSet = false });

        // Act
        await _repository.GetByIdAsync(convId);

        // Assert
        _dbMock.Verify(x => x.GetItemAsync(It.Is<GetItemRequest>(r => 
            r.TableName == "test_table" && 
            r.Key["PK"].S == "CONV#123-456" && 
            r.Key["SK"].S == "SUM#"), default), Times.Once);
    }

    [Fact]
    public async Task GetFactsAsync_UsesCorrectPrefixQuery()
    {
        // Arrange
        var convId = "123-456";
        _dbMock.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
               .ReturnsAsync(new QueryResponse { Items = [] });

        // Act
        await _repository.GetFactsAsync(convId);

        // Assert
        _dbMock.Verify(x => x.QueryAsync(It.Is<QueryRequest>(r => 
            r.KeyConditionExpression.Contains("PK = :pk") && 
            r.KeyConditionExpression.Contains("begins_with(SK, :sk_prefix)") && 
            r.ExpressionAttributeValues[":pk"].S == "CONV#123-456" && 
            r.ExpressionAttributeValues[":sk_prefix"].S == "FACT#"), default), Times.Once);
    }

    [Fact]
    public async Task GetTimelineAsync_UsesFilterExpressionForEvents()
    {
        // Arrange
        var convId = "123-456";
        _dbMock.Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), default))
               .ReturnsAsync(new QueryResponse { Items = [], LastEvaluatedKey = [] });

        // Act
        await _repository.GetTimelineAsync(convId, limit: 10);

        // Assert
        _dbMock.Verify(x => x.QueryAsync(It.Is<QueryRequest>(r => 
            r.KeyConditionExpression == "PK = :pk" && 
            r.FilterExpression.Contains("EVT#") && 
            !r.FilterExpression.Contains("MSG#") && 
            r.Limit == 10), default), Times.Once);
    }
}
