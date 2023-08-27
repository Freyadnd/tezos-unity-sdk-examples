using System;
using NUnit.Framework;
using TezosSDKExamples.Shared.Tezos;
using UnityEngine;

namespace TezosSDKExamples.Tests
{
    /// <summary>
    /// Unit tests for <see cref="TezosExtensions.JsonHelper"/>.
    ///
    /// JsonHelper is the project's workaround for UnityEngine.JsonUtility's inability
    /// to deserialize top-level JSON arrays. It wraps the array in an object so
    /// JsonUtility can process it via the standard {"items": [...]} pattern.
    ///
    /// These tests cover:
    ///   - Empty arrays (wallet with no tokens)
    ///   - Single-item arrays (ownership check responses)
    ///   - Multi-item arrays (paginated inventory responses)
    ///   - Field mapping correctness (contract address, token ID, balance)
    ///
    /// LIMITATION — tokenMetadata is not tested here:
    ///   The TokenBalance.tokenMetadata field is typed as System.Text.Json.JsonElement,
    ///   which UnityEngine.JsonUtility cannot deserialize. In practice, this field will
    ///   always deserialize to an empty/default JsonElement via JsonUtility.
    ///   If you need to test metadata parsing, use System.Text.Json.JsonSerializer
    ///   or Newtonsoft.Json in a separate helper class that can be fully unit-tested.
    ///
    /// RUNNING THESE TESTS:
    ///   Window → General → Test Runner → EditMode → Run All
    ///   Or filter by category: [Category("JsonHelper")]
    /// </summary>
    [Category("JsonHelper")]
    public class JsonHelperTests
    {
        // A minimal serializable type used to test JsonHelper independently of
        // TokenBalance, avoiding any fields JsonUtility cannot handle (e.g. JsonElement).
        [Serializable]
        private struct TestItem
        {
            public int    value;
            public string label;
        }


        // ---------------------------------------------------------------
        //  Empty and null input
        // ---------------------------------------------------------------

        [Test]
        public void FromJson_EmptyArray_ReturnsEmptyArray()
        {
            // Arrange — TzKT returns "[]" when a wallet has no matching tokens
            string json = "[]";

            // Act
            TestItem[] result = TezosExtensions.JsonHelper.FromJson<TestItem>(json);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty,
                "An empty JSON array should deserialize to an empty C# array, not null.");
        }

        [Test]
        public void FromJson_SingleItem_ReturnsArrayWithOneElement()
        {
            // Arrange — TzKT returns a one-element array for a single token balance match
            string json = "[{\"value\":1,\"label\":\"sword\"}]";

            // Act
            TestItem[] result = TezosExtensions.JsonHelper.FromJson<TestItem>(json);

            // Assert
            Assert.That(result, Has.Length.EqualTo(1));
        }

        [Test]
        public void FromJson_MultipleItems_ReturnsCorrectCount()
        {
            // Arrange — simulates a paginated inventory response with several tokens
            string json = "[{\"value\":1,\"label\":\"a\"},{\"value\":2,\"label\":\"b\"},{\"value\":3,\"label\":\"c\"}]";

            // Act
            TestItem[] result = TezosExtensions.JsonHelper.FromJson<TestItem>(json);

            // Assert
            Assert.That(result, Has.Length.EqualTo(3));
        }


        // ---------------------------------------------------------------
        //  Field deserialization
        // ---------------------------------------------------------------

        [Test]
        public void FromJson_DeserializesIntField_Correctly()
        {
            // Arrange
            string json = "[{\"value\":42,\"label\":\"test\"}]";

            // Act
            TestItem[] result = TezosExtensions.JsonHelper.FromJson<TestItem>(json);

            // Assert
            Assert.That(result[0].value, Is.EqualTo(42));
        }

        [Test]
        public void FromJson_DeserializesStringField_Correctly()
        {
            // Arrange
            string json = "[{\"value\":0,\"label\":\"dragon_shield\"}]";

            // Act
            TestItem[] result = TezosExtensions.JsonHelper.FromJson<TestItem>(json);

            // Assert
            Assert.That(result[0].label, Is.EqualTo("dragon_shield"));
        }

        [Test]
        public void FromJson_DeserializesAllItems_WithCorrectFieldValues()
        {
            // Arrange — verifies each item maps to the right array index
            string json = "[{\"value\":10,\"label\":\"first\"},{\"value\":20,\"label\":\"second\"}]";

            // Act
            TestItem[] result = TezosExtensions.JsonHelper.FromJson<TestItem>(json);

            // Assert
            Assert.That(result[0].value, Is.EqualTo(10));
            Assert.That(result[0].label, Is.EqualTo("first"));
            Assert.That(result[1].value, Is.EqualTo(20));
            Assert.That(result[1].label, Is.EqualTo("second"));
        }


        // ---------------------------------------------------------------
        //  TokenBalance field deserialization
        // ---------------------------------------------------------------
        // These tests use the real TokenBalance type to verify that the fields
        // the project actually queries (tokenId, balance, tokenContract) deserialize
        // correctly from representative TzKT API response fragments.

        [Test]
        public void FromJson_TokenBalance_DeserializesTokenId()
        {
            // Arrange — representative TzKT response fragment for one token balance
            string json =
                "[{" +
                "\"id\":1001," +
                "\"owner\":\"tz1TiZ74DtsT74VyWfbAuSis5KcncH1WvNB9\"," +
                "\"balance\":\"1\"," +
                "\"tokenContract\":\"KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY\"," +
                "\"tokenId\":\"7\"," +
                "\"lastTime\":\"2024-01-15T12:00:00Z\"" +
                "}]";

            // Act
            TokenBalance[] result = TezosExtensions.JsonHelper.FromJson<TokenBalance>(json);

            // Assert
            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0].tokenId, Is.EqualTo("7"),
                "tokenId should deserialize as a string matching the JSON value.");
        }

        [Test]
        public void FromJson_TokenBalance_DeserializesBalance()
        {
            string json =
                "[{" +
                "\"id\":1002," +
                "\"owner\":\"tz1abc\"," +
                "\"balance\":\"5\"," +
                "\"tokenContract\":\"KT1xxx\"," +
                "\"tokenId\":\"3\"," +
                "\"lastTime\":\"2024-01-15T12:00:00Z\"" +
                "}]";

            TokenBalance[] result = TezosExtensions.JsonHelper.FromJson<TokenBalance>(json);

            Assert.That(result[0].balance, Is.EqualTo("5"),
                "balance is stored as a raw string (not divided by decimals). " +
                "Parse to long or decimal before arithmetic.");
        }

        [Test]
        public void FromJson_TokenBalance_DeserializesContractAddress()
        {
            string json =
                "[{" +
                "\"id\":1003," +
                "\"owner\":\"tz1abc\"," +
                "\"balance\":\"1\"," +
                "\"tokenContract\":\"KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY\"," +
                "\"tokenId\":\"0\"," +
                "\"lastTime\":\"2024-01-15T12:00:00Z\"" +
                "}]";

            TokenBalance[] result = TezosExtensions.JsonHelper.FromJson<TokenBalance>(json);

            Assert.That(result[0].tokenContract, Is.EqualTo("KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY"));
        }

        [Test]
        public void FromJson_TokenBalance_MultipleEntries_PreservesOrder()
        {
            // Verifies that paginated results maintain the sort order returned by TzKT.
            // TezosExtensions requests &sort.asc=id, so id values should be ascending.
            string json =
                "[" +
                "{\"id\":100,\"owner\":\"tz1a\",\"balance\":\"1\",\"tokenContract\":\"KT1x\",\"tokenId\":\"0\",\"lastTime\":\"\"}," +
                "{\"id\":200,\"owner\":\"tz1b\",\"balance\":\"1\",\"tokenContract\":\"KT1y\",\"tokenId\":\"1\",\"lastTime\":\"\"}," +
                "{\"id\":300,\"owner\":\"tz1c\",\"balance\":\"1\",\"tokenContract\":\"KT1z\",\"tokenId\":\"2\",\"lastTime\":\"\"}" +
                "]";

            TokenBalance[] result = TezosExtensions.JsonHelper.FromJson<TokenBalance>(json);

            Assert.That(result[0].id, Is.EqualTo(100));
            Assert.That(result[1].id, Is.EqualTo(200));
            Assert.That(result[2].id, Is.EqualTo(300));
        }
    }
}
