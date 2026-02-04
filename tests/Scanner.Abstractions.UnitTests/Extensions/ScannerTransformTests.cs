using Scanner.Abstractions.Extensions;

namespace Scanner.Abstractions.UnitTests.Extensions
{
	[TestClass]
	public class ScannerTransformTests
	{
		/// <summary>
		/// Tests that known department keys are mapped to the expected scanner department strings.
		/// Input conditions: exact lower-case keys "korpus", "montaj", "sila", "uprav", and "check".
		/// Expected result: the corresponding localized department string is returned.
		/// </summary>
		[TestMethod]
		public void ToScannerDepartment_KnownKeys_ReturnsExpected()
		{
			// Arrange
			var mappings = new Dictionary<string, string>
			{
				{ "korpus", "051 Корпус" },
				{ "montaj", "051 Монтажка" },
				{ "sila", "051 Сила" },
				{ "uprav", "051 Управление" },
				{ "check", "051 Проверка" }
			};

			// Act & Assert
			foreach (var pair in mappings)
			{
				// Act
				var result = pair.Key.ToScannerDepartment();

				// Assert
				Assert.AreEqual(pair.Value, result, $"Input '{pair.Key}' should map to '{pair.Value}'.");
			}
		}

		/// <summary>
		/// Tests that null, empty, and whitespace-only inputs return false and leave out parameters at their defaults.
		/// Inputs: null, "", "   ".
		/// Expected: method returns false; column == string.Empty; idAuto == 0.
		/// </summary>
		[TestMethod]
		public void TryParseScan_NullOrWhitespaceInputs_ReturnsFalseAndDefaults()
		{
			// Arrange
			string?[] inputs = new string?[] { null, string.Empty, "   " };

			foreach (string? input in inputs)
			{
				// Act
				bool result = input.TryParseScan(out string column, out int idAuto);

				// Assert
				Assert.IsFalse(result, $"Input: '{input ?? "null"}' should return false.");
				Assert.AreEqual(string.Empty, column, "Column should be left as empty string for invalid input.");
				Assert.AreEqual(0, idAuto, "idAuto should be 0 for invalid input.");
			}
		}

		/// <summary>
		/// Tests a variety of invalid-format inputs that should return false.
		/// Cases include: missing pipe, empty column, missing dash in right part, dash at end, non-numeric id part.
		/// Expected: method returns false, idAuto == 0, and column matches the code-path-set value (if any).
		/// </summary>
		[TestMethod]
		public void TryParseScan_InvalidFormats_ReturnsFalseAndExpectedColumn()
		{
			// Arrange
			var cases = new (string? Input, string ExpectedColumn)[]
			{
				("NoPipe", string.Empty),         // no '|' => column remains default empty
				("|abc-1", string.Empty),         // empty column before '|'
				("Col|abc", "Col"),               // no '-' in right part -> column set but returns false
				("Col|abc-", "Col"),              // dash at end -> returns false, column set
				("Col|abc-xyz", "Col"),           // non-numeric id part -> returns false, column set
			};

			foreach (var (input, expectedColumn) in cases)
			{
				// Act
				bool result = input.TryParseScan(out string column, out int idAuto);

				// Assert
				Assert.IsFalse(result, $"Input '{input}' expected to be invalid.");
				Assert.AreEqual(expectedColumn, column, "Column value differs from expected for invalid input case.");
				Assert.AreEqual(0, idAuto, "idAuto should remain 0 for invalid parse attempts.");
			}
		}

		/// <summary>
		/// Tests valid parsing scenarios including trimming and large integer parsing.
		/// Inputs exercise typical valid inputs with and without extra whitespace.
		/// Expected: method returns true and outputs correct column and idAuto values.
		/// </summary>
		[TestMethod]
		public void TryParseScan_ValidInputs_ReturnsTrueAndParsesValues()
		{
			// Arrange
			var cases = new (string Input, string ExpectedColumn, int ExpectedId)[]
			{
				("Col|X-123", "Col", 123),
				("  Col Name  |  part -  2147483647  ", "Col Name", 2147483647), // int.MaxValue boundary
			};

			foreach (var (input, expectedColumn, expectedId) in cases)
			{
				// Act
				bool result = input.TryParseScan(out string column, out int idAuto);

				// Assert
				Assert.IsTrue(result, $"Input '{input}' should parse successfully.");
				Assert.AreEqual(expectedColumn, column, "Parsed column did not match expected.");
				Assert.AreEqual(expectedId, idAuto, "Parsed idAuto did not match expected.");
			}
		}

		/// <summary>
		/// Tests edge cases related to dash positions which reveal specific behavior of the implementation:
		/// - When the right part begins with '-' (e.g., 'C|-5'), the first '-' is treated as separator and the id parsed becomes positive.
		/// - When there are two dashes and the substring after the first dash starts with '-', the resulting id may be negative (int.MinValue tested).
		/// - An id string that overflows int should lead to parse failure (method returns false).
		/// </summary>
		[TestMethod]
		public void TryParseScan_DashPositionEdgeCases_ObservedBehaviorMatchesImplementation()
		{
			// Arrange / Act / Assert - case 1: rightPart begins with '-' so idPart becomes '5' -> parsed as 5
			{
				string? input = "C|-5";
				bool result = input.TryParseScan(out string column, out int idAuto);

				Assert.IsTrue(result, "Input 'C|-5' should return true according to current implementation.");
				Assert.AreEqual("C", column);
				Assert.AreEqual(5, idAuto);
			}

			// Arrange / Act / Assert - case 2: double dash where idPart becomes negative int.MinValue
			{
				string? input = "C|a--2147483648";
				bool result = input.TryParseScan(out string column, out int idAuto);

				Assert.IsTrue(result, "Input 'C|a--2147483648' should parse to int.MinValue.");
				Assert.AreEqual("C", column);
				Assert.AreEqual(int.MinValue, idAuto);
			}

			// Arrange / Act / Assert - case 3: numeric overflow in idPart -> parse fails and idAuto stays 0
			{
				string? input = "C|a-2147483648"; // idPart becomes "2147483648" -> overflow, int.TryParse returns false
				bool result = input.TryParseScan(out string column, out int idAuto);

				Assert.IsFalse(result, "Input 'C|a-2147483648' should fail parsing due to int overflow.");
				Assert.AreEqual("C", column);
				Assert.AreEqual(0, idAuto);
			}
		}
	}
}