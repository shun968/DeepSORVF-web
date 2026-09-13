using System.Reflection;
using LayeredArchitecture.Domain.Services;
using LayeredArchitecture.Infrastructure.Repositories;
using NetArchTest.Rules;
using Xunit;

namespace LayeredArchitecture.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(GreetingService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(CsvAisRepository).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_OuterLayers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LayeredArchitecture.Application",
                "LayeredArchitecture.Infrastructure",
                "LayeredArchitecture.Web")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Frameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "System.Data.SqlClient")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Application_Or_Web()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LayeredArchitecture.Application",
                "LayeredArchitecture.Web")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Frameworks()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "System.Data.SqlClient")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    private static string FormatFailures(TestResult result) =>
        result.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", result.FailingTypeNames);
}
