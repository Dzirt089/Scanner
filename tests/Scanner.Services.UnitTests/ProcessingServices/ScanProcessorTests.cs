using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Scanner.Abstractions.Contracts;
using Scanner.Abstractions.Messages;
using Scanner.Abstractions.Models;
using Scanner.Services.ProcessingServices;

namespace Scanner.Services.UnitTests.ProcessingServices
{
	[TestClass]
	public class ScanProcessorTests
	{
		/// <summary>
		/// Проверяет, что конструктор успешно создает экземпляр, когда предоставлены все зависимости.
		/// что он обращается к IOptions.Value ровно один раз и что к другим зависимостям не выполняется никаких неожиданных вызовов.
		/// Условия:
		/// — ILogger, IOptions (с ненулевым значением), IScan Event Sink и IPort Complite Repository предоставляются через макеты.
		/// Ожидаемый результат:
		/// - Экземпляр, реализующий процессор IScan, создается без броска.
		/// - Метод получения IOptions.Value вызывается ровно один раз во время построения.
		/// - Никаких других вызовов в регистраторе, сканировании Event Sink и комплировании репозитория не производится.
		/// </summary>
		[TestMethod]
		public void Constructor_WithValidDependencies_CreatesInstanceAndUsesOptionsValue()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<ScanProcessor>>(MockBehavior.Strict);
			var scanEventSinkMock = new Mock<IScanEventSink>(MockBehavior.Strict);
			var repoMock = new Mock<IPortCompliteRepository>(MockBehavior.Strict);

			var scannerOptions = new ScannerOptions
			{
				KnownPrefixes = new[] { "Body", "Mounting" },
				PortScanIntervalSeconds = 5,
				PortStaleSeconds = 10
			};

			var optionsMock = new Mock<IOptions<ScannerOptions>>(MockBehavior.Strict);
			optionsMock.Setup(o => o.Value).Returns(scannerOptions);

			// Act
			ScanProcessor? instance = null;
			try
			{
				instance = new ScanProcessor(
					loggerMock.Object,
					optionsMock.Object,
					scanEventSinkMock.Object,
					repoMock.Object);
			}
			catch (Exception ex)
			{
				Assert.Fail($"Constructor threw an unexpected exception: {ex}");
			}

			// Assert
			Assert.IsNotNull(instance, "Instance should not be null.");
			Assert.IsInstanceOfType(instance, typeof(IScanProcessor), "Instance should implement IScanProcessor.");

			optionsMock.Verify(o => o.Value, Times.Once(), "IOptions.Value should be accessed exactly once during construction.");

			// No interactions expected with other dependencies during construction
			loggerMock.VerifyNoOtherCalls();
			scanEventSinkMock.VerifyNoOtherCalls();
			repoMock.VerifyNoOtherCalls();
		}

		/// <summary>
		/// Verifies that the constructor tolerates a null IOptions.Value (i.e., the options.Value getter returns null)
		/// and still constructs an instance without throwing.
		/// Conditions:
		/// - ILogger, IOptions (with Value == null), IScanEventSink and IPortCompliteRepository are provided via mocks.
		/// Expected result:
		/// - An instance implementing IScanProcessor is created without throwing.
		/// - IOptions.Value getter is invoked exactly once during construction.
		/// </summary>
		[TestMethod]
		public void Constructor_WithNullOptionsValue_AllowsConstructionAndAccessesValue()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<ScanProcessor>>(MockBehavior.Strict);
			var scanEventSinkMock = new Mock<IScanEventSink>(MockBehavior.Strict);
			var repoMock = new Mock<IPortCompliteRepository>(MockBehavior.Strict);

			var optionsMock = new Mock<IOptions<ScannerOptions>>(MockBehavior.Strict);
			// Return null from Value to simulate a misconfigured IOptions
			optionsMock.Setup(o => o.Value).Returns((ScannerOptions?)null);

			// Act
			ScanProcessor? instance = null;
			try
			{
				instance = new ScanProcessor(
					loggerMock.Object,
					optionsMock.Object,
					scanEventSinkMock.Object,
					repoMock.Object);
			}
			catch (Exception ex)
			{
				Assert.Fail($"Constructor threw an unexpected exception when options.Value was null: {ex}");
			}

			// Assert
			Assert.IsNotNull(instance, "Instance should be created even when options.Value is null.");
			Assert.IsInstanceOfType(instance, typeof(IScanProcessor), "Instance should implement IScanProcessor.");

			optionsMock.Verify(o => o.Value, Times.Once(), "IOptions.Value should be accessed exactly once during construction.");

			// No interactions expected with other dependencies during construction
			loggerMock.VerifyNoOtherCalls();
			scanEventSinkMock.VerifyNoOtherCalls();
			repoMock.VerifyNoOtherCalls();
		}

		/// <summary>
		/// Ensures that when the incoming line does not start with any known prefix the processor returns early
		/// and does not call repository or publish any events.
		/// Input: KnownPrefixes contains "known", line starts with "unknown...".
		/// Expected: Repository and event sink are never invoked.
		/// </summary>
		[TestMethod]
		public async Task ProcessAsync_PrefixNotMatched_DoesNotCallRepositoryOrPublishEvent()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<ScanProcessor>>();
			var options = Options.Create(new ScannerOptions { KnownPrefixes = new[] { "known" } });
			var sinkMock = new Mock<IScanEventSink>();
			var repoMock = new Mock<IPortCompliteRepository>();
			var processor = new ScanProcessor(loggerMock.Object, options, sinkMock.Object, repoMock.Object);

			var line = new ScanLine("port", "unknownprefix|col-1");
			var token = CancellationToken.None;

			// Act
			await processor.ProcessAsync(line, token);

			// Assert
			repoMock.Verify(r => r.SetDateDbRowsByIdAutoAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
			repoMock.Verify(r => r.GetNameAutoDbRowsByIdAutoAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
			sinkMock.Verify(s => s.PublishScanEvent(It.IsAny<ScanReceivedMessage>()), Times.Never);
		}

		/// <summary>
		/// Ensures that when the scan line cannot be parsed by TryParseScan the processor returns early
		/// and does not call repository or publish any events.
		/// Input: KnownPrefixes contains "pre", line starts with "pre" but no '|' and/or '-' present (invalid format).
		/// Expected: Repository and event sink are never invoked.
		/// </summary>
		[TestMethod]
		public async Task ProcessAsync_InvalidScanFormat_DoesNotCallRepositoryOrPublishEvent()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<ScanProcessor>>();
			var options = Options.Create(new ScannerOptions { KnownPrefixes = new[] { "pre" } });
			var sinkMock = new Mock<IScanEventSink>();
			var repoMock = new Mock<IPortCompliteRepository>();
			var processor = new ScanProcessor(loggerMock.Object, options, sinkMock.Object, repoMock.Object);

			// Line starts with prefix but is invalid for TryParseScan (no '|' separator)
			var invalidLines = new[]
			{
				"pre   ",              // whitespace after prefix
				"pre-no-sep",          // contains dash but no '|'
				"pre|no-digits-here",  // right side id part is not an integer
				"pre|-"                // dash is last char -> invalid
			};

			var token = CancellationToken.None;

			foreach (var text in invalidLines)
			{
				var line = new ScanLine("port", text);

				// Act
				await processor.ProcessAsync(line, token);
			}

			// Assert
			repoMock.Verify(r => r.SetDateDbRowsByIdAutoAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
			repoMock.Verify(r => r.GetNameAutoDbRowsByIdAutoAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
			sinkMock.Verify(s => s.PublishScanEvent(It.IsAny<ScanReceivedMessage>()), Times.Never);
		}

		/// <summary>
		/// Ensures that when SetDateDbRowsByIdAutoAsync returns false (no rows updated) the processor returns early
		/// and does not call GetNameAutoDbRowsByIdAutoAsync nor publish any events.
		/// Input: Valid parse, repository.SetDate... returns false.
		/// Expected: GetName... and PublishScanEvent are not invoked.
		/// </summary>
		[TestMethod]
		public async Task ProcessAsync_DatabaseUpdateZeroRows_SkipsGettingNameAndPublishing()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<ScanProcessor>>();
			var options = Options.Create(new ScannerOptions { KnownPrefixes = new[] { "p" } });
			var sinkMock = new Mock<IScanEventSink>();
			var repoMock = new Mock<IPortCompliteRepository>();
			var processor = new ScanProcessor(loggerMock.Object, options, sinkMock.Object, repoMock.Object);

			// Craft a line that will parse: left part "pcolumn", right part "x-42"
			var line = new ScanLine("port", "pcolumn|x-42");
			var token = CancellationToken.None;

			repoMock.Setup(r => r.SetDateDbRowsByIdAutoAsync("pcolumn", 42, It.IsAny<DateTime>(), token))
				.ReturnsAsync(false);

			// Act
			await processor.ProcessAsync(line, token);

			// Assert
			repoMock.Verify(r => r.SetDateDbRowsByIdAutoAsync("pcolumn", 42, It.IsAny<DateTime>(), token), Times.Once);
			repoMock.Verify(r => r.GetNameAutoDbRowsByIdAutoAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
			sinkMock.Verify(s => s.PublishScanEvent(It.IsAny<ScanReceivedMessage>()), Times.Never);
		}

		/// <summary>
		/// Ensures successful end-to-end flow: when parsing succeeds, DB update returns true and GetName returns information,
		/// the processor publishes a ScanReceivedMessage with the same InformationOnAutomation instance.
		/// Input: Valid parse with several id values including int.MinValue and int.MaxValue to cover numeric boundaries.
		/// Expected: Repository methods are called and the event sink receives a message containing the same InformationOnAutomation.
		/// </summary>
		[TestMethod]
		public async Task ProcessAsync_ValidFlow_PublishesEvent_ForVariousIdEdgeValues()
		{
			// Arrange
			var loggerMock = new Mock<ILogger<ScanProcessor>>();
			var options = Options.Create(new ScannerOptions { KnownPrefixes = new[] { "my" } });
			var sinkMock = new Mock<IScanEventSink>();
			var repoMock = new Mock<IPortCompliteRepository>();
			var processor = new ScanProcessor(loggerMock.Object, options, sinkMock.Object, repoMock.Object);
			var token = CancellationToken.None;

			var idValues = new[] { int.MinValue, -1, 0, 1, int.MaxValue };

			foreach (var id in idValues)
			{
				// Create line that starts with prefix "my" and has a column "mycol"
				var column = "mycol";
				var rightPart = $"x-{id}";
				var lineText = $"{column}|{rightPart}"; // starts with "mycol" which in turn starts with prefix "my"
				var line = new ScanLine("port", lineText);

				// Prepare repository behavior
				repoMock.Reset(); // reset setups and invocation counts between iterations
				sinkMock.Reset();

				repoMock.Setup(r => r.SetDateDbRowsByIdAutoAsync(column, id, It.IsAny<DateTime>(), token))
					.ReturnsAsync(true);

				var info = new InformationOnAutomation
				{
					IdAuto = id,
					ZakNm = "Z",
					Art = "A",
					Name = "N",
					NameVkc = "V",
					Planed = DateTime.UtcNow,
					ScanerTime = DateTime.UtcNow,
					Department = "D"
				};

				repoMock.Setup(r => r.GetNameAutoDbRowsByIdAutoAsync(id, column, It.IsAny<DateTime>(), token))
					.ReturnsAsync(info);

				// Act
				await processor.ProcessAsync(line, token);

				// Assert
				repoMock.Verify(r => r.SetDateDbRowsByIdAutoAsync(column, id, It.IsAny<DateTime>(), token), Times.Once);
				repoMock.Verify(r => r.GetNameAutoDbRowsByIdAutoAsync(id, column, It.IsAny<DateTime>(), token), Times.Once);
				sinkMock.Verify(s => s.PublishScanEvent(It.Is<ScanReceivedMessage>(m => ReferenceEquals(m.Value, info))), Times.Once);
			}
		}
	}
}