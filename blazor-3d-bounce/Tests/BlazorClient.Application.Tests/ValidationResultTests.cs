using BlazorClient.Application.Validation;
using NUnit.Framework;

namespace BlazorClient.Application.Tests;

/// <summary>
/// Unit tests for the <see cref="ValidationResult"/> class.
/// </summary>
/// <remarks>
/// Tests cover creation of success, warning-only, and error validation results.
/// </remarks>
[TestFixture]
public class ValidationResultTests
{
    /// <summary>
    /// Verifies that Success creates a valid result with no warnings or errors.
    /// </summary>
    [Test]
    public void Success_ShouldCreateValidResult()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Is.Empty);
        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>
    /// Verifies that WithWarnings creates a valid result with warning messages.
    /// </summary>
    [Test]
    public void WithWarnings_ShouldCreateValidResultWithWarnings()
    {
        // Act
        var result = ValidationResult.WithWarnings("Warning 1", "Warning 2");

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Count.EqualTo(2));
        Assert.That(result.Warnings, Contains.Item("Warning 1"));
        Assert.That(result.Warnings, Contains.Item("Warning 2"));
        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>
    /// Verifies that WithErrors creates an invalid result with error messages.
    /// </summary>
    [Test]
    public void WithErrors_ShouldCreateInvalidResultWithErrors()
    {
        // Act
        var result = ValidationResult.WithErrors("Error 1", "Error 2");

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Errors, Contains.Item("Error 1"));
        Assert.That(result.Errors, Contains.Item("Error 2"));
        Assert.That(result.Warnings, Is.Empty);
    }

    /// <summary>
    /// Verifies that Create with only warnings (no errors) produces a valid result.
    /// </summary>
    [Test]
    public void Create_WithNoErrors_ShouldBeValid()
    {
        // Act
        var result = ValidationResult.Create(
            new[] { "Warning" },
            Array.Empty<string>()
        );

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Count.EqualTo(1));
        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>
    /// Verifies that Create with errors produces an invalid result.
    /// </summary>
    [Test]
    public void Create_WithErrors_ShouldBeInvalid()
    {
        // Act
        var result = ValidationResult.Create(
            new[] { "Warning" },
            new[] { "Error" }
        );

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Warnings, Has.Count.EqualTo(1));
        Assert.That(result.Errors, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Verifies that Create with empty collections produces a valid result.
    /// </summary>
    [Test]
    public void Create_WithEmptyCollections_ShouldBeValid()
    {
        // Act
        var result = ValidationResult.Create(
            Enumerable.Empty<string>(),
            Enumerable.Empty<string>()
        );

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Is.Empty);
        Assert.That(result.Errors, Is.Empty);
    }
}
