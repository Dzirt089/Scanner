using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Scanner.Abstractions.Channels;
using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Models;
using Scanner.Services.ScannerBackgroundServices;

namespace Scanner.Services.UnitTests.ScannerBackgroundServices
{
	[TestClass]
	public class SerialScannerHostedServiceTests
	{
		/// <summary>
		/// Verifies that the constructor successfully creates an instance when IOptions.Value is non-null.
		/// Conditions:
		/// - A real ScanChannel and strict mocks for ILogger, IOptions (Value != null), IScannerRuntimeState and IErrorReporter are provided.
		/// Expected:
		/// - Instance is created without throwing.
		/// - IOptions.Value is accessed exactly once.
		/// - No other calls are made on logger, scannerRuntimeState, or reporter during construction.
		/// </summary>
		[TestMethod]
		public void Constructor_WithValidOptionsValue_CreatesInstanceAndUsesOptionsValue()
		{
			// Arrange
			var channel = new ScanChannel();

			var loggerMock = new Mock<ILogger<SerialScannerHostedService>>(MockBehavior.Strict);
			var scannerRuntimeStateMock = new Mock<IScannerRuntimeState>(MockBehavior.Strict);
			var reporterMock = new Mock<IErrorReporter>(MockBehavior.Strict);

			var scannerOptions = new ScannerOptions
			{
				KnownPrefixes = new[] { "A" },
				PortScanIntervalSeconds = 5,
				PortStaleSeconds = 10
			};

			var optionsMock = new Mock<IOptions<ScannerOptions>>(MockBehavior.Strict);
			optionsMock.Setup(o => o.Value).Returns(scannerOptions);

			// Act
			SerialScannerHostedService? instance = null;
			try
			{
				instance = new SerialScannerHostedService(
					channel,
					loggerMock.Object,
					optionsMock.Object,
					scannerRuntimeStateMock.Object,
					reporterMock.Object);
			}
			catch (Exception ex)
			{
				Assert.Fail($"Constructor threw an unexpected exception: {ex}");
			}

			// Assert
			Assert.IsNotNull(instance, "Instance should not be null.");
			Assert.IsInstanceOfType(instance, typeof(SerialScannerHostedService), "Instance should be of the expected type.");

			optionsMock.Verify(o => o.Value, Times.Once(), "IOptions.Value should be accessed exactly once during construction.");

			// No interactions expected with other dependencies during construction
			loggerMock.VerifyNoOtherCalls();
			scannerRuntimeStateMock.VerifyNoOtherCalls();
			reporterMock.VerifyNoOtherCalls();
		}

		/// <summary>
		/// Verifies that the constructor tolerates a null IOptions.Value (options.Value returns null)
		/// and still constructs an instance without throwing.
		/// Conditions:
		/// - A real ScanChannel and strict mocks for ILogger, IOptions (Value == null), IScannerRuntimeState and IErrorReporter are provided.
		/// Expected:
		/// - Instance is created without throwing even when options.Value is null.
		/// - IOptions.Value getter is invoked exactly once during construction.
		/// - No other calls are made on logger, scannerRuntimeState, or reporter during construction.
		/// </summary>
		[TestMethod]
		public void Constructor_WithNullOptionsValue_AllowsConstructionAndAccessesValue()
		{
			// Arrange
			var channel = new ScanChannel();

			var loggerMock = new Mock<ILogger<SerialScannerHostedService>>(MockBehavior.Strict);
			var scannerRuntimeStateMock = new Mock<IScannerRuntimeState>(MockBehavior.Strict);
			var reporterMock = new Mock<IErrorReporter>(MockBehavior.Strict);

			var optionsMock = new Mock<IOptions<ScannerOptions>>(MockBehavior.Strict);
			// Simulate misconfigured IOptions where Value is null
			optionsMock.Setup(o => o.Value).Returns((ScannerOptions?)null);

			// Act
			SerialScannerHostedService? instance = null;
			try
			{
				instance = new SerialScannerHostedService(
					channel,
					loggerMock.Object,
					optionsMock.Object,
					scannerRuntimeStateMock.Object,
					reporterMock.Object);
			}
			catch (Exception ex)
			{
				Assert.Fail($"Constructor threw an unexpected exception when options.Value was null: {ex}");
			}

			// Assert
			Assert.IsNotNull(instance, "Instance should be created even when options.Value is null.");
			Assert.IsInstanceOfType(instance, typeof(SerialScannerHostedService), "Instance should be of the expected type.");

			optionsMock.Verify(o => o.Value, Times.Once(), "IOptions.Value should be accessed exactly once during construction.");

			// No interactions expected with other dependencies during construction
			loggerMock.VerifyNoOtherCalls();
			scannerRuntimeStateMock.VerifyNoOtherCalls();
			reporterMock.VerifyNoOtherCalls();
		}

		/// <summary>
		/// Ensures that StopAsync completes successfully when there are no listeners.
		/// Conditions:
		/// - A SerialScannerHostedService is constructed with mocked dependencies.
		/// - The internal listeners collection is empty (default state).
		/// Expected:
		/// - StopAsync completes without throwing and returns a completed task.
		/// </summary>
		[TestMethod]
		public async Task StopAsync_NoListeners_CompletesWithoutThrowing()
		{
			// Arrange
			var channel = new ScanChannel();
			var loggerMock = new Mock<ILogger<SerialScannerHostedService>>(MockBehavior.Strict);
			var optionsMock = new Mock<IOptions<ScannerOptions>>(MockBehavior.Strict);
			optionsMock.Setup(o => o.Value).Returns(new ScannerOptions { PortScanIntervalSeconds = 1, PortStaleSeconds = 10 });
			var runtimeStateMock = new Mock<IScannerRuntimeState>(MockBehavior.Strict);
			var reporterMock = new Mock<IErrorReporter>(MockBehavior.Strict);

			var service = new SerialScannerHostedService(
				channel,
				loggerMock.Object,
				optionsMock.Object,
				runtimeStateMock.Object,
				reporterMock.Object);

			var cts = new CancellationTokenSource();

			// Act
			Task stopTask = null;
			Exception? thrown = null;
			try
			{
				stopTask = service.StopAsync(cts.Token);
				await stopTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				thrown = ex;
			}

			// Assert
			Assert.IsNull(thrown, "StopAsync should not throw when there are no listeners.");
			Assert.IsNotNull(stopTask, "StopAsync should return a Task.");
			Assert.IsTrue(stopTask.IsCompleted, "StopAsync returned task should be completed.");
		}

	}
}