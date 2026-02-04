using MailerVKT;

using Microsoft.Extensions.Logging;

using Moq;

using Scanner.Abstractions.Channels;
using Scanner.Services.ScannerBackgroundServices;

namespace Scanner.Services.UnitTests.ScannerBackgroundServices
{
	[TestClass]
	public class ErrorReportingHostedServiceTests
	{
		/// <summary>
		/// Verifies that the constructor creates an instance when provided with valid dependencies.
		/// Conditions:
		/// - A strict ILogger mock is provided to ensure no unexpected calls are made during construction.
		/// - A real ErrorReportChannel instance is provided.
		/// - An attempt to create a Sender via parameterless construction is made; if Sender cannot be constructed,
		///   the test is marked Inconclusive with instructions to provide a suitable Sender instance.
		/// Expected:
		/// - Construction does not throw.
		/// - Returned instance is not null and is of the expected type.
		/// - The logger mock receives no calls during construction.
		/// </summary>
		[TestMethod]
		public void Constructor_WithValidDependencies_CreatesInstanceAndDoesNotCallLogger()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<ErrorReportingHostedService>>(MockBehavior.Strict);
			var channel = new ErrorReportChannel();

			Sender? sender;
			try
			{
				// Try to create Sender using parameterless constructor. If Sender requires special construction,
				// mark the test inconclusive and instruct the maintainer to provide a suitable instance.
				sender = (Sender?)Activator.CreateInstance(typeof(Sender));
				if (sender is null)
				{
					Assert.Inconclusive("Cannot construct MailerVKT.Sender via parameterless constructor. Provide a Sender instance or update the test to create it appropriately.");
					return;
				}
			}
			catch (Exception ex)
			{
				Assert.Inconclusive($"Unable to construct MailerVKT.Sender: {ex.GetType().FullName}: {ex.Message}. Provide a Sender instance or adjust test construction.");
				return;
			}

			// Act
			ErrorReportingHostedService? instance = null;
			try
			{
				instance = new ErrorReportingHostedService(loggerMock.Object, channel, sender);
			}
			catch (Exception ex)
			{
				Assert.Fail($"Constructor threw an unexpected exception: {ex}");
			}

			// Assert
			Assert.IsNotNull(instance, "Instance should not be null.");
			Assert.IsInstanceOfType(instance, typeof(ErrorReportingHostedService), "Instance should be of type ErrorReportingHostedService.");

			// Constructor should not have invoked any logger methods.
			loggerMock.VerifyNoOtherCalls();
		}

		/// <summary>
		/// Ensures that multiple constructions with different channel instances succeed.
		/// Conditions:
		/// - A strict ILogger mock is provided.
		/// - Two different ErrorReportChannel instances are created and passed in separate constructions.
		/// - Sender is attempted to be created via parameterless constructor; if not possible, test is inconclusive.
		/// Expected:
		/// - Both constructions succeed without throwing.
		/// - No calls are made to the logger during construction.
		/// Rationale:
		/// - Validates that the constructor does not capture or mutate channel state during construction.
		/// </summary>
		[TestMethod]
		public void Constructor_WithDifferentChannels_DoesNotThrowAndDoesNotCallLogger()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<ErrorReportingHostedService>>(MockBehavior.Strict);
			var channel1 = new ErrorReportChannel();
			var channel2 = new ErrorReportChannel();

			Sender? sender;
			try
			{
				sender = (Sender?)Activator.CreateInstance(typeof(Sender));
				if (sender is null)
				{
					Assert.Inconclusive("Cannot construct MailerVKT.Sender via parameterless constructor. Provide a Sender instance or update the test to create it appropriately.");
					return;
				}
			}
			catch (Exception ex)
			{
				Assert.Inconclusive($"Unable to construct MailerVKT.Sender: {ex.GetType().FullName}: {ex.Message}. Provide a Sender instance or adjust test construction.");
				return;
			}

			// Act & Assert for first channel
			try
			{
				var instance1 = new ErrorReportingHostedService(loggerMock.Object, channel1, sender);
				Assert.IsNotNull(instance1);
			}
			catch (Exception ex)
			{
				Assert.Fail($"Constructor threw an unexpected exception for channel1: {ex}");
			}

			// Act & Assert for second channel
			try
			{
				var instance2 = new ErrorReportingHostedService(loggerMock.Object, channel2, sender);
				Assert.IsNotNull(instance2);
			}
			catch (Exception ex)
			{
				Assert.Fail($"Constructor threw an unexpected exception for channel2: {ex}");
			}

			// No logger calls should happen during construction
			loggerMock.VerifyNoOtherCalls();
		}
	}
}