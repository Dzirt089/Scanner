using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Moq;

using Scanner.Abstractions.Channels;
using Scanner.Abstractions.Contracts;
using Scanner.Services.ScannerBackgroundServices;

namespace Scanner.Services.UnitTests.ScannerBackgroundServices
{
	[TestClass]
	public sealed class ScanProcessingHostedServiceTests
	{
		/// <summary>
		/// Verifies that the constructor successfully creates an instance when all dependencies are provided
		/// and that no unexpected calls are made on the provided dependencies during construction.
		/// Conditions:
		/// - A real ScanChannel instance is used (ScanChannel is sealed and not mockable).
		/// - IServiceScopeFactory, ILogger, IScannerRuntimeState and IErrorReporter are provided as strict mocks.
		/// Expected result:
		/// - An instance of ScanProcessingHostedService is created without throwing.
		/// - No calls are made to any of the mocked dependencies during construction.
		/// </summary>
		[TestMethod]
		public void Constructor_WithValidDependencies_CreatesInstanceAndDoesNotCallDependencies()
		{
			// Arrange
			var channel = new ScanChannel();

			var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
			var loggerMock = new Mock<ILogger<ScanProcessingHostedService>>(MockBehavior.Strict);
			var runtimeStateMock = new Mock<IScannerRuntimeState>(MockBehavior.Strict);
			var reporterMock = new Mock<IErrorReporter>(MockBehavior.Strict);

			// Act
			ScanProcessingHostedService? instance = null;
			try
			{
				instance = new ScanProcessingHostedService(
					channel,
					scopeFactoryMock.Object,
					loggerMock.Object,
					runtimeStateMock.Object,
					reporterMock.Object);
			}
			catch (Exception ex)
			{
				Assert.Fail($"Constructor threw an unexpected exception: {ex}");
			}

			// Assert
			Assert.IsNotNull(instance, "Instance should not be null after construction.");
			Assert.IsInstanceOfType(instance, typeof(BackgroundService), "Instance should inherit from BackgroundService.");

			// Ensure constructor did not invoke any methods on the mocks
			scopeFactoryMock.VerifyNoOtherCalls();
			loggerMock.VerifyNoOtherCalls();
			runtimeStateMock.VerifyNoOtherCalls();
			reporterMock.VerifyNoOtherCalls();
		}
	}
}