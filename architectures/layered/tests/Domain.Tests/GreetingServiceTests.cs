using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;
using LayeredArchitecture.Domain.Services;
using Moq;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class GreetingServiceTests
{
    [Fact]
    public void GetMessage_ReturnsMessageFromRepository()
    {
        var repositoryMock = new Mock<IGreetingRepository>();
        repositoryMock
            .Setup(repository => repository.GetGreeting())
            .Returns(new Greeting("Hello, World!"));

        var greetingService = new GreetingService(repositoryMock.Object);

        var message = greetingService.GetMessage();

        Assert.Equal("Hello, World!", message);
    }
}
