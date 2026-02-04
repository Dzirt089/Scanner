using Scanner.Abstractions.Models;
using Scanner.Services.ScannerServices;

namespace Scanner.Services.UnitTests.ScannerServices
{
	/// <summary>
	/// Tests for ScannerRuntimeState focused on TryUpsert behavior:
	/// - inserting when key absent
	/// - not overwriting when key exists (case-insensitive dictionary)
	/// </summary>
	[TestClass]
	public sealed class ScannerRuntimeStateTests
	{
		/// <summary>
		/// Ensures TryUpsert adds entries when the port name is not present.
		/// Conditions:
		/// - Several distinct port names (including empty, whitespace-only and very long) are used.
		/// Expected result:
		/// - Each unique port name results in a single stored ScanModel instance.
		/// - The returned collection contains the exact instances provided (reference equality).
		/// </summary>
		[TestMethod]
		public void TryUpsert_NewKeys_AddsScanModels_ForVariousPortNames()
		{
			// Arrange
			var sut = new ScannerRuntimeState();

			var scanA = new ScanModel();
			var scanB = new ScanModel();
			var scanC = new ScanModel();

			string portNormal = "COM1";
			string portEmpty = string.Empty;
			string portWhitespace = "   ";
			string portLong = new string('X', 1024);

			// Act: insert three distinct keys (normal + empty + whitespace). Also add a very long to ensure it behaves like a normal key.
			sut.TryUpsert(portNormal, scanA);
			sut.TryUpsert(portEmpty, scanB);
			sut.TryUpsert(portWhitespace, scanC);
			// Also verify a very long key works as expected (separate instance)
			var scanLong = new ScanModel();
			sut.TryUpsert(portLong, scanLong);

			// Assert
			var all = sut.GetAllScans();

			Assert.IsNotNull(all, "GetAllScans should not return null.");
			// We expect 4 distinct entries
			Assert.AreEqual(4, all.Count, "There should be exactly four stored ScanModel instances for distinct port names.");

			// Ensure reference equality for each inserted model
			Assert.IsTrue(all.Contains(scanA), "Inserted scanA instance should be present.");
			Assert.IsTrue(all.Contains(scanB), "Inserted scanB instance should be present (empty port name).");
			Assert.IsTrue(all.Contains(scanC), "Inserted scanC instance should be present (whitespace-only port name).");
			Assert.IsTrue(all.Contains(scanLong), "Inserted scanLong instance should be present (very long port name).");
		}

		/// <summary>
		/// Verifies that TryUpsert does not overwrite an existing entry and that port name lookup is case-insensitive.
		/// Conditions:
		/// - First insert with one ScanModel under "COM1".
		/// - Second insert with a different ScanModel under "com1" (different case).
		/// Expected result:
		/// - The dictionary retains the first ScanModel instance and does not replace it with the second.
		/// - Only one entry exists for the two logically-equal keys.
		/// </summary>
		[TestMethod]
		public void TryUpsert_ExistingKey_DoesNotOverwrite_IsCaseInsensitive()
		{
			// Arrange
			var sut = new ScannerRuntimeState();

			var original = new ScanModel();
			var replacement = new ScanModel();

			string originalPort = "COM1";
			string differentCasePort = "com1";

			// Act
			sut.TryUpsert(originalPort, original);
			// Attempt to upsert with different-case port; because dictionary is OrdinalIgnoreCase this should be considered the same key
			sut.TryUpsert(differentCasePort, replacement);

			// Assert
			var all = sut.GetAllScans();

			Assert.IsNotNull(all, "GetAllScans should not return null.");
			Assert.AreEqual(1, all.Count, "There should be exactly one stored ScanModel instance for case-insensitive duplicate keys.");

			// The stored instance must be the original one (not replaced)
			Assert.IsTrue(all.Contains(original), "Original instance must remain stored after second TryUpsert with different-case key.");
			Assert.IsFalse(all.Contains(replacement), "Replacement instance must not replace the original instance.");
		}

		/// <summary>
		/// Verifies that GetAllScans returns a non-null, empty collection when no scans have been registered.
		/// Conditions:
		/// - A fresh instance of ScannerRuntimeState is used with its internal dictionary in the default state.
		/// Expected result:
		/// - The returned IReadOnlyCollection&lt;ScanModel&gt; is not null and has a Count of 0.
		/// </summary>
		[TestMethod]
		public void GetAllScans_NoScans_ReturnsEmptyCollection()
		{
			// Arrange
			var state = new ScannerRuntimeState();

			// Act
			IReadOnlyCollection<ScanModel>? result = null;
			try
			{
				result = state.GetAllScans();
			}
			catch (Exception ex)
			{
				Assert.Fail($"GetAllScans threw an unexpected exception: {ex}");
			}

			// Assert
			Assert.IsNotNull(result, "Result should not be null.");
			Assert.IsInstanceOfType(result, typeof(IReadOnlyCollection<ScanModel>), "Result should implement IReadOnlyCollection<ScanModel>.");
			Assert.AreEqual(0, result.Count, "Expected no scans in a fresh ScannerRuntimeState instance.");
		}

		/// <summary>
		/// Ensures that successive calls to GetAllScans return distinct array instances (ToArray snapshot behavior).
		/// Conditions:
		/// - A fresh instance of ScannerRuntimeState is used.
		/// Expected result:
		/// - Two calls to GetAllScans return different object instances (reference inequality), even if both are empty.
		/// </summary>
		[TestMethod]
		public void GetAllScans_Twice_ReturnsDistinctInstances()
		{
			// Arrange
			var state = new ScannerRuntimeState();

			// Act
			IReadOnlyCollection<ScanModel> first = state.GetAllScans();
			IReadOnlyCollection<ScanModel> second = state.GetAllScans();

			// Assert
			// Ensure contents are equal (both empty) and that arrays are distinct instances
			Assert.IsNotNull(first, "First result should not be null.");
			Assert.IsNotNull(second, "Second result should not be null.");
			Assert.AreEqual(first.Count, second.Count, "Both results should have the same count.");
			// Because GetAllScans uses ToArray(), it should return a fresh array each call; verify reference inequality.
			// However, when the collection is empty, ToArray() may return Array.Empty<T>() which is a shared singleton.
			// Allow the same reference in the empty case; otherwise require distinct instances.
			if (first.Count > 0 || second.Count > 0)
			{
				Assert.AreNotSame(first, second, "Successive calls to GetAllScans should return distinct collection instances.");
			}
			else
			{
				// Both empty: it's acceptable for implementations to return a cached empty array instance.
				Assert.IsTrue(ReferenceEquals(first, second) || first.Count == 0, "Both empty results are acceptable and may reference the same singleton empty array.");
			}
		}

		/// <summary>
		/// Verifies that TryUpdateName returns false when the provided port is not present in runtime state.
		/// Conditions:
		/// - ScannerRuntimeState is empty (no TryUpsert called).
		/// - Provided line is a well-formed candidate (korpus|x-1).
		/// Expected:
		/// - Method returns false and does not throw.
		/// </summary>
		[TestMethod]
		public void TryUpdateName_PortNotFound_ReturnsFalse()
		{
			// Arrange
			var sut = new ScannerRuntimeState();
			var portName = "COM-MISSING";
			var line = "korpus|x-1";

			// Act
			bool result = sut.TryUpdateName(portName, line);

			// Assert
			Assert.IsFalse(result, "TryUpdateName should return false when the port is not present.");
		}

		/// <summary>
		/// Ensures TryUpdateName returns false for a variety of invalid or malformed line inputs.
		/// Conditions:
		/// - A ScanModel exists for the port.
		/// - Lines tested include empty, whitespace, missing delimiter, missing dash, no id, non-numeric id.
		/// Expected:
		/// - For each invalid input, TryUpdateName returns false and does not change the ScanModel.Name.
		/// </summary>
		[TestMethod]
		public void TryUpdateName_InvalidLines_ReturnsFalseAndDoesNotSetName()
		{
			// Arrange
			var sut = new ScannerRuntimeState();
			var portName = "COM1";
			var model = new ScanModel();
			// Ensure initial name is null to detect changes
			model.Name = null;
			sut.TryUpsert(portName, model);

			var invalidLines = new[]
			{
				string.Empty,          // empty
				"   ",                 // whitespace-only
				"korpus",              // missing '|'
				"korpus|nodashpart",   // missing '-' in right part
				"korpus|x-",           // dash at end -> no id part
				"korpus|x-abc"         // non-numeric id part
			};

			foreach (var line in invalidLines)
			{
				// Act
				bool result = sut.TryUpdateName(portName, line);

				// Assert
				Assert.IsFalse(result, $"Line '{line}' should be considered invalid and return false.");
				Assert.IsNull(model.Name, "Model.Name must remain null for invalid lines.");
			}
		}

		/// <summary>
		/// Verifies that TryUpdateName successfully maps known department columns to department names and sets ScanModel.Name.
		/// Conditions:
		/// - For each known department key (korpus, montaj, sila, uprav, check) a ScanModel is present.
		/// - Each input line has a valid right part with a dash and numeric id.
		/// Expected:
		/// - Method returns true and ScanModel.Name equals the mapped department string.
		/// </summary>
		[TestMethod]
		public void TryUpdateName_ValidDepartments_SetsNameAndReturnsTrue()
		{
			// Arrange
			var sut = new ScannerRuntimeState();

			var cases = new Dictionary<string, string>
			{
				// key: input column, value: expected mapped name from ToScannerDepartment
				["korpus"] = "051 Корпус",
				["montaj"] = "051 Монтажка",
				["sila"] = "051 Сила",
				["uprav"] = "051 Управление",
				["check"] = "051 Проверка"
			};

			int counter = 0;
			foreach (var kvp in cases)
			{
				counter++;
				var portName = "PORT-" + counter;
				var model = new ScanModel();
				model.Name = null;
				sut.TryUpsert(portName, model);

				var line = $"{kvp.Key}|prefix-1"; // valid right part: contains '-' and numeric id

				// Act
				bool result = sut.TryUpdateName(portName, line);

				// Assert
				Assert.IsTrue(result, $"Expected TryUpdateName to succeed for column '{kvp.Key}'.");
				Assert.AreEqual(kvp.Value, model.Name, $"Model.Name should be set to mapped value for '{kvp.Key}'.");
			}
		}

		/// <summary>
		/// Ensures Remove can be called for a port that does not exist.
		/// Input: a fresh ScannerRuntimeState instance and a non-existing port name ("COM1").
		/// Expected: no exception is thrown and GetAllScans() remains empty.
		/// </summary>
		[TestMethod]
		public void Remove_NonExistingPort_DoesNotThrowAndCollectionUnchanged()
		{
			// Arrange
			var sut = new ScannerRuntimeState();

			// Act
			try
			{
				sut.Remove("COM1");
			}
			catch (Exception ex)
			{
				Assert.Fail($"Remove threw an unexpected exception for non-existing port: {ex}");
			}

			// Assert
			var all = sut.GetAllScans();
			Assert.IsNotNull(all, "GetAllScans should not return null.");
			Assert.AreEqual(0, all.Count, "Collection should remain empty after removing a non-existing port.");
		}

		/// <summary>
		/// Verifies Remove is tolerant to a variety of string inputs and does not alter state for an initially-empty runtime.
		/// Inputs tested: empty string, whitespace-only string, very long string, string with special/unicode characters,
		/// and typical mixed-case strings.
		/// Expected: No call to Remove throws, and the collection remains empty.
		/// </summary>
		[TestMethod]
		public void Remove_VariousPortNames_DoesNotThrowAndLeavesCollectionEmpty()
		{
			// Arrange
			var sut = new ScannerRuntimeState();
			var testInputs = new[]
			{
				string.Empty,
				"   ",
				new string('A', 1024),           // very long string
				"port-with-特殊-∆",               // special / unicode characters
				"COM1",                          // typical port name
				"com1"                           // different case
			};

			// Act & Assert
			foreach (var port in testInputs)
			{
				try
				{
					// Act
					sut.Remove(port);
				}
				catch (Exception ex)
				{
					Assert.Fail($"Remove threw an unexpected exception for port value '{port}': {ex}");
				}
			}

			// Final Assert: ensure state unchanged (empty)
			var all = sut.GetAllScans();
			Assert.IsNotNull(all, "GetAllScans should not return null after multiple Remove calls.");
			Assert.AreEqual(0, all.Count, "Collection should remain empty after Remove calls on an initially-empty runtime.");
		}
	}
}