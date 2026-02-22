using BlazorClient.Domain.Common;
using NUnit.Framework;

namespace BlazorClient.Domain.Tests;

/// <summary>
/// Unit tests for the <see cref="Result"/> and <see cref="Result{T}"/> types.
/// </summary>
/// <remarks>
/// Tests cover success/failure creation, functional operations (Map, Match, OnSuccess, OnFailure),
/// and implicit boolean conversions.
/// </remarks>
[TestFixture]
public class ResultTests
{
    #region Result<T> Tests

    /// <summary>
    /// Verifies that Success creates a successful result with the provided value.
    /// </summary>
    [Test]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result<int>.Success(42);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.IsFailure, Is.False);
        Assert.That(result.Value, Is.EqualTo(42));
        Assert.That(result.Error, Is.Null);
    }

    /// <summary>
    /// Verifies that Failure creates a failed result with the provided error message.
    /// </summary>
    [Test]
    public void Failure_ShouldCreateFailedResult()
    {
        // Act
        var result = Result<int>.Failure("Something went wrong");

        // Assert
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Something went wrong"));
    }

    /// <summary>
    /// Verifies that Match calls the onSuccess function when result is successful.
    /// </summary>
    [Test]
    public void Match_WhenSuccess_ShouldCallOnSuccess()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var onSuccessCalled = false;
        var onFailureCalled = false;

        // Act
        var matchResult = result.Match(
            onSuccess: v => { onSuccessCalled = true; return v * 2; },
            onFailure: e => { onFailureCalled = true; return -1; }
        );

        // Assert
        Assert.That(onSuccessCalled, Is.True);
        Assert.That(onFailureCalled, Is.False);
        Assert.That(matchResult, Is.EqualTo(84));
    }

    /// <summary>
    /// Verifies that Match calls the onFailure function when result is failed.
    /// </summary>
    [Test]
    public void Match_WhenFailure_ShouldCallOnFailure()
    {
        // Arrange
        var result = Result<int>.Failure("Error");

        // Act
        var matchResult = result.Match(
            onSuccess: v => v * 2,
            onFailure: e => -1
        );

        // Assert
        Assert.That(matchResult, Is.EqualTo(-1));
    }

    /// <summary>
    /// Verifies that OnSuccess executes the action when result is successful.
    /// </summary>
    [Test]
    public void OnSuccess_WhenSuccess_ShouldExecuteAction()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var actionExecuted = false;

        // Act
        result.OnSuccess(v => actionExecuted = true);

        // Assert
        Assert.That(actionExecuted, Is.True);
    }

    /// <summary>
    /// Verifies that OnSuccess does not execute the action when result is failed.
    /// </summary>
    [Test]
    public void OnSuccess_WhenFailure_ShouldNotExecuteAction()
    {
        // Arrange
        var result = Result<int>.Failure("Error");
        var actionExecuted = false;

        // Act
        result.OnSuccess(v => actionExecuted = true);

        // Assert
        Assert.That(actionExecuted, Is.False);
    }

    /// <summary>
    /// Verifies that OnFailure executes the action with error message when result is failed.
    /// </summary>
    [Test]
    public void OnFailure_WhenFailure_ShouldExecuteAction()
    {
        // Arrange
        var result = Result<int>.Failure("Error");
        var capturedError = string.Empty;

        // Act
        result.OnFailure(e => capturedError = e);

        // Assert
        Assert.That(capturedError, Is.EqualTo("Error"));
    }

    /// <summary>
    /// Verifies that OnFailure does not execute the action when result is successful.
    /// </summary>
    [Test]
    public void OnFailure_WhenSuccess_ShouldNotExecuteAction()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var actionExecuted = false;

        // Act
        result.OnFailure(e => actionExecuted = true);

        // Assert
        Assert.That(actionExecuted, Is.False);
    }

    /// <summary>
    /// Verifies that Map transforms the value when result is successful.
    /// </summary>
    [Test]
    public void Map_WhenSuccess_ShouldMapValue()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var mappedResult = result.Map(v => v.ToString());

        // Assert
        Assert.That(mappedResult.IsSuccess, Is.True);
        Assert.That(mappedResult.Value, Is.EqualTo("42"));
    }

    /// <summary>
    /// Verifies that Map propagates the error when result is failed.
    /// </summary>
    [Test]
    public void Map_WhenFailure_ShouldPropagateError()
    {
        // Arrange
        var result = Result<int>.Failure("Original error");

        // Act
        var mappedResult = result.Map(v => v.ToString());

        // Assert
        Assert.That(mappedResult.IsFailure, Is.True);
        Assert.That(mappedResult.Error, Is.EqualTo("Original error"));
    }

    /// <summary>
    /// Verifies that implicit bool conversion returns true for successful results.
    /// </summary>
    [Test]
    public void ImplicitBoolConversion_WhenSuccess_ShouldReturnTrue()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act & Assert
        Assert.That((bool)result, Is.True);
    }

    /// <summary>
    /// Verifies that implicit bool conversion returns false for failed results.
    /// </summary>
    [Test]
    public void ImplicitBoolConversion_WhenFailure_ShouldReturnFalse()
    {
        // Arrange
        var result = Result<int>.Failure("Error");

        // Act & Assert
        Assert.That((bool)result, Is.False);
    }

    #endregion

    #region Result (non-generic) Tests

    /// <summary>
    /// Verifies that non-generic Success creates a successful result.
    /// </summary>
    [Test]
    public void NonGenericSuccess_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.IsFailure, Is.False);
        Assert.That(result.Error, Is.Null);
    }

    /// <summary>
    /// Verifies that non-generic Failure creates a failed result with error message.
    /// </summary>
    [Test]
    public void NonGenericFailure_ShouldCreateFailedResult()
    {
        // Act
        var result = Result.Failure("Something went wrong");

        // Assert
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Something went wrong"));
    }

    /// <summary>
    /// Verifies that non-generic Match calls onSuccess when result is successful.
    /// </summary>
    [Test]
    public void NonGenericMatch_WhenSuccess_ShouldCallOnSuccess()
    {
        // Arrange
        var result = Result.Success();
        var onSuccessCalled = false;

        // Act
        var matchResult = result.Match(
            onSuccess: () => { onSuccessCalled = true; return "success"; },
            onFailure: e => "failure"
        );

        // Assert
        Assert.That(onSuccessCalled, Is.True);
        Assert.That(matchResult, Is.EqualTo("success"));
    }

    /// <summary>
    /// Verifies that non-generic Match calls onFailure when result is failed.
    /// </summary>
    [Test]
    public void NonGenericMatch_WhenFailure_ShouldCallOnFailure()
    {
        // Arrange
        var result = Result.Failure("Error");

        // Act
        var matchResult = result.Match(
            onSuccess: () => "success",
            onFailure: e => "failure"
        );

        // Assert
        Assert.That(matchResult, Is.EqualTo("failure"));
    }

    /// <summary>
    /// Verifies that non-generic OnSuccess executes action when successful.
    /// </summary>
    [Test]
    public void NonGenericOnSuccess_WhenSuccess_ShouldExecuteAction()
    {
        // Arrange
        var result = Result.Success();
        var actionExecuted = false;

        // Act
        result.OnSuccess(() => actionExecuted = true);

        // Assert
        Assert.That(actionExecuted, Is.True);
    }

    /// <summary>
    /// Verifies that non-generic OnFailure executes action with error when failed.
    /// </summary>
    [Test]
    public void NonGenericOnFailure_WhenFailure_ShouldExecuteAction()
    {
        // Arrange
        var result = Result.Failure("Error");
        var capturedError = string.Empty;

        // Act
        result.OnFailure(e => capturedError = e);

        // Assert
        Assert.That(capturedError, Is.EqualTo("Error"));
    }

    /// <summary>
    /// Verifies that non-generic implicit bool conversion returns true for success.
    /// </summary>
    [Test]
    public void NonGenericImplicitBoolConversion_WhenSuccess_ShouldReturnTrue()
    {
        // Arrange
        var result = Result.Success();

        // Act & Assert
        Assert.That((bool)result, Is.True);
    }

    /// <summary>
    /// Verifies that non-generic implicit bool conversion returns false for failure.
    /// </summary>
    [Test]
    public void NonGenericImplicitBoolConversion_WhenFailure_ShouldReturnFalse()
    {
        // Arrange
        var result = Result.Failure("Error");

        // Act & Assert
        Assert.That((bool)result, Is.False);
    }

    #endregion
}
