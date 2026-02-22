using BlazorClient.Application.Events;
using BlazorClient.Infrastructure.Events;
using NSubstitute;
using NUnit.Framework;

namespace BlazorClient.Infrastructure.Tests;

/// <summary>
/// Test event class for EventAggregator tests.
/// </summary>
/// <param name="Message">The test message payload.</param>
public record TestEvent(string Message) : Event;

/// <summary>
/// Secondary test event class to verify event type discrimination.
/// </summary>
/// <param name="Value">The test integer payload.</param>
public record AnotherTestEvent(int Value) : Event;

/// <summary>
/// Unit tests for the <see cref="EventAggregator"/> class.
/// </summary>
/// <remarks>
/// Tests cover subscription, unsubscription, event publishing, error handling, and thread safety.
/// </remarks>
[TestFixture]
public class EventAggregatorTests
{
    private EventAggregator _eventAggregator = null!;

    /// <summary>
    /// Initializes a fresh EventAggregator instance before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _eventAggregator = new EventAggregator();
    }

    #region Subscribe Tests

    /// <summary>
    /// Verifies that IEventHandler subscribers receive published events.
    /// </summary>
    [Test]
    public async Task Subscribe_IEventHandler_ShouldReceiveEvents()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        var testEvent = new TestEvent("Hello");

        _eventAggregator.Subscribe(handler);

        // Act
        await _eventAggregator.PublishAsync(testEvent);

        // Assert
        await handler.Received(1).HandleAsync(testEvent);
    }

    /// <summary>
    /// Verifies that Func delegate subscribers receive published events.
    /// </summary>
    [Test]
    public async Task Subscribe_FuncHandler_ShouldReceiveEvents()
    {
        // Arrange
        var receivedMessage = string.Empty;
        Func<TestEvent, Task> handler = async e =>
        {
            receivedMessage = e.Message;
            await Task.CompletedTask;
        };

        _eventAggregator.Subscribe(handler);

        // Act
        await _eventAggregator.PublishAsync(new TestEvent("Test Message"));

        // Assert
        Assert.That(receivedMessage, Is.EqualTo("Test Message"));
    }

    /// <summary>
    /// Verifies that multiple handlers all receive the same published event.
    /// </summary>
    [Test]
    public async Task Subscribe_MultipleHandlers_ShouldAllReceiveEvents()
    {
        // Arrange
        var handler1 = Substitute.For<IEventHandler<TestEvent>>();
        var handler2 = Substitute.For<IEventHandler<TestEvent>>();
        var testEvent = new TestEvent("Hello");

        _eventAggregator.Subscribe(handler1);
        _eventAggregator.Subscribe(handler2);

        // Act
        await _eventAggregator.PublishAsync(testEvent);

        // Assert
        await handler1.Received(1).HandleAsync(testEvent);
        await handler2.Received(1).HandleAsync(testEvent);
    }

    /// <summary>
    /// Verifies that subscribing the same handler twice only results in one notification.
    /// </summary>
    [Test]
    public async Task Subscribe_SameHandlerTwice_ShouldOnlyReceiveOnce()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        var testEvent = new TestEvent("Hello");

        _eventAggregator.Subscribe(handler);
        _eventAggregator.Subscribe(handler); // Subscribe again

        // Act
        await _eventAggregator.PublishAsync(testEvent);

        // Assert
        await handler.Received(1).HandleAsync(testEvent);
    }

    #endregion

    #region Unsubscribe Tests

    /// <summary>
    /// Verifies that unsubscribed handlers no longer receive events.
    /// </summary>
    [Test]
    public async Task Unsubscribe_ShouldStopReceivingEvents()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        var testEvent = new TestEvent("Hello");

        _eventAggregator.Subscribe(handler);
        _eventAggregator.Unsubscribe(handler);

        // Act
        await _eventAggregator.PublishAsync(testEvent);

        // Assert
        await handler.DidNotReceive().HandleAsync(Arg.Any<TestEvent>());
    }

    /// <summary>
    /// Verifies that unsubscribing a non-subscribed handler does not throw.
    /// </summary>
    [Test]
    public async Task Unsubscribe_WhenNotSubscribed_ShouldNotThrow()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();

        // Act & Assert - should not throw
        Assert.DoesNotThrow(() => _eventAggregator.Unsubscribe(handler));
    }

    #endregion

    #region PublishAsync Tests

    /// <summary>
    /// Verifies that publishing with no subscribers does not throw.
    /// </summary>
    [Test]
    public async Task PublishAsync_WithNoSubscribers_ShouldNotThrow()
    {
        // Act & Assert - should not throw
        await _eventAggregator.PublishAsync(new TestEvent("No subscribers"));
    }

    /// <summary>
    /// Verifies that handlers only receive events of their subscribed type.
    /// </summary>
    [Test]
    public async Task PublishAsync_DifferentEventTypes_ShouldOnlyNotifyCorrectSubscribers()
    {
        // Arrange
        var testEventHandler = Substitute.For<IEventHandler<TestEvent>>();
        var anotherEventHandler = Substitute.For<IEventHandler<AnotherTestEvent>>();

        _eventAggregator.Subscribe(testEventHandler);
        _eventAggregator.Subscribe(anotherEventHandler);

        // Act
        await _eventAggregator.PublishAsync(new TestEvent("Test"));

        // Assert
        await testEventHandler.Received(1).HandleAsync(Arg.Any<TestEvent>());
        await anotherEventHandler.DidNotReceive().HandleAsync(Arg.Any<AnotherTestEvent>());
    }

    /// <summary>
    /// Verifies that a throwing handler does not prevent other handlers from executing.
    /// </summary>
    [Test]
    public async Task PublishAsync_WhenHandlerThrows_ShouldContinueWithOtherHandlers()
    {
        // Arrange
        var failingHandler = Substitute.For<IEventHandler<TestEvent>>();
        failingHandler.HandleAsync(Arg.Any<TestEvent>())
            .Returns(x => throw new InvalidOperationException("Handler failed"));

        var successHandler = Substitute.For<IEventHandler<TestEvent>>();
        successHandler.HandleAsync(Arg.Any<TestEvent>()).Returns(Task.CompletedTask);

        _eventAggregator.Subscribe(failingHandler);
        _eventAggregator.Subscribe(successHandler);

        // Act - should not throw
        await _eventAggregator.PublishAsync(new TestEvent("Test"));

        // Assert - second handler should still be called
        await successHandler.Received(1).HandleAsync(Arg.Any<TestEvent>());
    }

    #endregion

    #region Publish (Fire-and-Forget) Tests

    /// <summary>
    /// Verifies that fire-and-forget Publish returns immediately without blocking.
    /// </summary>
    [Test]
    public void Publish_ShouldNotBlock()
    {
        // Arrange
        var handler = Substitute.For<IEventHandler<TestEvent>>();
        handler.HandleAsync(Arg.Any<TestEvent>()).Returns(async _ =>
        {
            await Task.Delay(100);
        });

        _eventAggregator.Subscribe(handler);

        // Act - should return immediately
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _eventAggregator.Publish(new TestEvent("Test"));
        sw.Stop();

        // Assert - should not wait for handler
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(50));
    }

    #endregion

    #region Thread Safety Tests

    /// <summary>
    /// Verifies that concurrent subscribe and publish operations are thread-safe.
    /// </summary>
    [Test]
    public async Task ConcurrentSubscribeAndPublish_ShouldNotThrow()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - subscribe and publish concurrently
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var handler = Substitute.For<IEventHandler<TestEvent>>();
                _eventAggregator.Subscribe(handler);
            }));

            tasks.Add(Task.Run(async () =>
            {
                await _eventAggregator.PublishAsync(new TestEvent($"Message {i}"));
            }));
        }

        // Assert - should complete without throwing
        await Task.WhenAll(tasks);
    }

    #endregion
}
