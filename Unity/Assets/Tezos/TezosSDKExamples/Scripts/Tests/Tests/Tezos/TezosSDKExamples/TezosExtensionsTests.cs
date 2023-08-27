using NUnit.Framework;
using TezosSDKExamples.Shared.Tezos;
using TezosSDKExamples.Tests.Mocks;

namespace TezosSDKExamples.Tests
{
    /// <summary>
    /// Unit tests for <see cref="TezosExtensions"/>.
    ///
    /// TESTING PHILOSOPHY FOR SDK EXTENSION METHODS:
    ///   TezosExtensions has two layers of functionality:
    ///
    ///   1. Pure logic (tested here with MockTezosAPI):
    ///      HasActiveWalletAddress() — wraps GetActiveWalletAddress(), no network call.
    ///      These tests are fast, deterministic, and require no network access.
    ///
    ///   2. Network logic (see Integration/TzKTApiIntegrationTests.cs):
    ///      IsOwnerOfToken(), GetAllTokensForOwner(), GetTokensForOwnerPage(),
    ///      GetAllTokensForOwnerPaginated() — all make HTTP requests to TzKT.
    ///      These cannot be unit tested without either:
    ///        a) Injecting a fake HTTP client (requires refactoring TezosExtensions), or
    ///        b) Running against the live TzKT API (integration tests).
    ///      See Integration/TzKTApiIntegrationTests.cs for the integration approach.
    ///
    /// RUNNING THESE TESTS:
    ///   Window → General → Test Runner → EditMode → Run All
    ///   Or filter by category: [Category("TezosExtensions")]
    /// </summary>
    [Category("TezosExtensions")]
    public class TezosExtensionsTests
    {
        private MockTezosAPI _mock;

        [SetUp]
        public void SetUp()
        {
            // Each test gets a fresh mock with no configured state.
            _mock = new MockTezosAPI();
        }

        [TearDown]
        public void TearDown()
        {
            // Explicit reset is redundant here since SetUp creates a new instance,
            // but is included as a pattern reminder for cases where the mock is shared.
            _mock.Reset();
        }


        // ---------------------------------------------------------------
        //  HasActiveWalletAddress
        // ---------------------------------------------------------------
        // This method is the project-wide "is the player logged in?" check.
        // It wraps ITezosAPI.GetActiveWalletAddress() with a null/empty guard.
        // Tests verify each state the SDK can return for an unauthenticated player.

        [Test]
        public void HasActiveWalletAddress_WhenAddressIsValidTz1_ReturnsTrue()
        {
            // Arrange — simulate a connected wallet with a realistic Tezos address
            _mock.ActiveWalletAddress = "tz1TiZ74DtsT74VyWfbAuSis5KcncH1WvNB9";

            // Act
            bool result = _mock.HasActiveWalletAddress();

            // Assert
            Assert.That(result, Is.True,
                "A non-empty wallet address should be treated as authenticated.");
        }

        [Test]
        public void HasActiveWalletAddress_WhenAddressIsNull_ReturnsFalse()
        {
            // Arrange — null is what the SDK returns when no wallet is connected
            _mock.ActiveWalletAddress = null;

            // Act
            bool result = _mock.HasActiveWalletAddress();

            // Assert
            Assert.That(result, Is.False,
                "A null address means no wallet is connected.");
        }

        [Test]
        public void HasActiveWalletAddress_WhenAddressIsEmpty_ReturnsFalse()
        {
            // Arrange — some SDK versions return "" instead of null on disconnect
            _mock.ActiveWalletAddress = "";

            // Act
            bool result = _mock.HasActiveWalletAddress();

            // Assert
            Assert.That(result, Is.False,
                "An empty string address should not be treated as authenticated.");
        }

        [Test]
        public void HasActiveWalletAddress_WhenAddressIsKt1Contract_ReturnsTrue()
        {
            // Arrange — KT1 addresses are smart contract addresses, not user wallets.
            // HasActiveWalletAddress() does not validate address format — it only checks
            // for null/empty. This test documents that limitation: if the SDK somehow
            // returns a contract address, this method will incorrectly report authenticated.
            // In production, validate that the address starts with "tz1", "tz2", or "tz3".
            _mock.ActiveWalletAddress = "KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY";

            // Act
            bool result = _mock.HasActiveWalletAddress();

            // Assert — documents current behaviour, not necessarily ideal behaviour
            Assert.That(result, Is.True,
                "HasActiveWalletAddress() does not validate address format. " +
                "A KT1 contract address passes the null/empty check. " +
                "Consider adding a tz1/tz2/tz3 prefix validation if your game " +
                "needs to distinguish user wallets from contract addresses.");
        }


        // ---------------------------------------------------------------
        //  CallContract (via MockTezosAPI call log)
        // ---------------------------------------------------------------
        // These tests verify that the MockTezosAPI correctly records CallContract
        // invocations. They also demonstrate the pattern for testing any game code
        // that calls _tezos.CallContract() — assert against ContractCallLog.

        [Test]
        public void CallContract_RecordsOneEntry_InContractCallLog()
        {
            // Arrange
            string contract  = "KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY";
            string entryPoint = "transfer";
            string input     = "{\"int\":\"1\"}";

            // Act
            _mock.CallContract(contract, entryPoint, input);

            // Assert
            Assert.That(_mock.ContractCallLog, Has.Count.EqualTo(1),
                "One CallContract() call should produce exactly one log entry.");
        }

        [Test]
        public void CallContract_RecordsCorrectParameters()
        {
            // Arrange
            string contract   = "KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY";
            string entryPoint = "submit_score";
            string input      = "{\"int\":\"42000\"}";
            ulong  amount     = 0;

            // Act
            _mock.CallContract(contract, entryPoint, input, amount);

            // Assert
            MockTezosAPI.ContractCallRecord recorded = _mock.ContractCallLog[0];
            Assert.That(recorded.ContractAddress, Is.EqualTo(contract));
            Assert.That(recorded.EntryPoint,      Is.EqualTo(entryPoint));
            Assert.That(recorded.Input,           Is.EqualTo(input));
            Assert.That(recorded.Amount,          Is.EqualTo(amount));
        }

        [Test]
        public void CallContract_RecordsMultipleCalls_InOrder()
        {
            // Demonstrates that the log preserves call order — useful for verifying
            // that a multi-step game flow (e.g. approve then transfer) calls contracts
            // in the correct sequence.

            // Act
            _mock.CallContract("KT1_contract_A", "entrypoint_1", "{}", 0);
            _mock.CallContract("KT1_contract_B", "entrypoint_2", "{}", 0);

            // Assert
            Assert.That(_mock.ContractCallLog, Has.Count.EqualTo(2));
            Assert.That(_mock.ContractCallLog[0].ContractAddress, Is.EqualTo("KT1_contract_A"),
                "First call should be recorded first.");
            Assert.That(_mock.ContractCallLog[1].ContractAddress, Is.EqualTo("KT1_contract_B"),
                "Second call should be recorded second.");
        }

        [Test]
        public void Reset_ClearsContractCallLog()
        {
            // Arrange — populate the mock with state
            _mock.ActiveWalletAddress = "tz1abc...";
            _mock.CallContract("KT1...", "entrypoint", "{}");

            // Act
            _mock.Reset();

            // Assert — all state should be cleared
            Assert.That(_mock.ContractCallLog,      Is.Empty);
            Assert.That(_mock.ActiveWalletAddress,  Is.Null);
            Assert.That(_mock.ConnectWalletCalled,  Is.False);
        }


        // ---------------------------------------------------------------
        //  Page size constants
        // ---------------------------------------------------------------

        [Test]
        public void DefaultPageSize_IsPositive()
        {
            Assert.That(TezosExtensions.DefaultPageSize, Is.GreaterThan(0));
        }

        [Test]
        public void MaxPageSize_IsGreaterThanDefaultPageSize()
        {
            Assert.That(TezosExtensions.MaxPageSize, Is.GreaterThan(TezosExtensions.DefaultPageSize),
                "MaxPageSize should be larger than DefaultPageSize so that " +
                "full-load calls fetch more tokens per round-trip than preview calls.");
        }
    }
}
