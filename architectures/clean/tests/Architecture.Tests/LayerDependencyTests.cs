using System.Reflection;
using CleanArchitecture.Application.UseCases;
using CleanArchitecture.Domain.Ports;
using CleanArchitecture.Domain.Services;
using CleanArchitecture.Infrastructure.Adapters;
using NetArchTest.Rules;
using Xunit;

namespace CleanArchitecture.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(AisSightingService).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ProcessVideoFrameUseCase).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(CsvAisReader).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_AnyOtherLayer()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "CleanArchitecture.Application",
                "CleanArchitecture.Infrastructure",
                "CleanArchitecture.Web")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Frameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "System.Data.SqlClient")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    // The rule the whole pattern rests on: the use cases reach every stage through the
    // Domain's interfaces, so they must not know any concrete implementation exists.
    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Web()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("CleanArchitecture.Infrastructure", "CleanArchitecture.Web")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Application_Or_Web()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("CleanArchitecture.Application", "CleanArchitecture.Web")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Theory]
    [InlineData(typeof(IAisReader))]
    [InlineData(typeof(ICameraParametersReader))]
    [InlineData(typeof(IVideoFrameReader))]
    [InlineData(typeof(IDetector))]
    [InlineData(typeof(ITracker))]
    [InlineData(typeof(IFusionEngine))]
    public void EveryPort_IsImplementedOnlyInInfrastructure(Type port)
    {
        var implementations = new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false } && port.IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(implementations);
        Assert.All(implementations, type => Assert.Equal(InfrastructureAssembly, type.Assembly));
    }

    private static string FormatFailures(TestResult result) =>
        result.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", result.FailingTypeNames);
}
