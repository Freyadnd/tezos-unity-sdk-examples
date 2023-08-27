using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using TezosSDKExamples.Shared.Tezos;
using TezosSDKExamples.Tests.Mocks;

namespace TezosSDKExamples.Tests.Integration
{
    /// <summary>
    /// Integration test scaffolding for <see cref="TezosExtensions"/> network methods.
    ///
    /// IMPORTANT — THESE TESTS ARE IGNORED BY DEFAULT.
    ///   Each test is marked [Ignore] and will not run in automated CI or local
    ///   Test Runner runs. To run one, remove its [Ignore] attribute temporarily.
    ///   All tests require a live connection to https://api.tzkt.io.
    ///
    /// WHY INTEGRATION TESTS FOR NETWORK CALLS?
    ///   TezosExtensions.IsOwnerOfToken(), GetAllTokensForOwner(), and related methods
    ///   build URLs and make real HTTP requests using UnityWebRequest. There is no HTTP
    ///   client interface to inject, so these methods cannot be unit tested without
    ///   refactoring. The options are:
    ///
    ///   Option A — Integration tests (this file):
    ///     Call the real TzKT API with known public wallet addresses and assert on the
    ///     live response. Fast to write, always tests the real path, but:
    ///       - Requires network access (fails offline or in restricted CI environments)
    ///       - Depends on external data (test wallet holdings may change over time)
    ///       - Slower than unit tests (~200–500ms per request)
    ///
    ///   Option B — Refactor to inject an HTTP abstraction:
    ///     Extract the HTTP call in TezosExtensions into an interface (e.g. IHttpClient)
    ///     and inject a fake in tests. Enables fully offline unit tests, but requires
    ///     changing TezosExtensions' private implementation — a larger code change.
    ///     This is the recommended approach if test coverage of network logic becomes
    ///     a priority.
    ///
    ///   Option C — HTTP interception (e.g. WireMock.Net):
    ///     Run a local HTTP mock server that intercepts UnityWebRequest calls.
    ///     Requires adding a third-party library and test infrastructure setup.
    ///     Enables offline tests with controlled responses, including error scenarios.
    ///
    ///   For this example project, Option A is shown here as the lowest-friction
    ///   starting point. The tests use real public Tezos mainnet addresses and
    ///   contracts that are known to have stable state.
    ///
    /// RUNNING THESE TESTS:
    ///   1. Ensure network access to https://api.tzkt.io
    ///   2. Remove the [Ignore] attribute from the test you want to run
    ///   3. Window → General → Test Runner → EditMode → Run Selected
    ///   4. Re-add [Ignore] before committing
    ///
    /// WHY EditMode FOR NETWORK CALLS?
    ///   UnityWebRequest works in EditMode tests via Unity's player loop.
    ///   UniTask's async/await is bridged to NUnit's async Task support.
    ///   For tests that require a running scene or MonoBehaviour lifecycle,
    ///   use PlayMode tests instead.
    /// </summary>
    [Category("Integration")]
    public class TzKTApiIntegrationTests
    {
        // A public Tezos mainnet wallet known to hold NFTs on the demo contract.
        // This address is used in Example02_NFTTokenGating.cs for the same purpose.
        private const string KnownOwnerAddress   = "tz2U7C8cf4W5Qw6onYjF8QLhnh5hMRbrrDon";

        // A wallet address that is not expected to hold the demo NFT.
        private const string KnownNonOwnerAddress = "tz1TiZ74DtsT74VyWfbAuSis5KcncH1WvNB9";

        // The FA2 demo contract used across all examples in this project.
        private const string DemoNFTContract = "KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY";
        private const int    DemoTokenId     = 1;

        private MockTezosAPI _mock;

        [SetUp]
        public void SetUp()
        {
            _mock = new MockTezosAPI();
            _mock.ActiveWalletAddress = KnownOwnerAddress;
        }

        [TearDown]
        public void TearDown()
        {
            _mock.Reset();
        }


        // ---------------------------------------------------------------
        //  IsOwnerOfToken
        // ---------------------------------------------------------------

        [Test]
        [Ignore("Integration test — requires live TzKT API. Remove [Ignore] to run manually.")]
        public async Task IsOwnerOfToken_KnownOwner_ReturnsTrue()
        {
            // Arrange — address known to hold the demo token
            _mock.ActiveWalletAddress = KnownOwnerAddress;

            // Act
            // UniTask is bridged to Task via .AsTask() for NUnit async test support.
            bool result = await _mock.IsOwnerOfToken(
                KnownOwnerAddress, DemoNFTContract, DemoTokenId).AsTask();

            // Assert
            Assert.That(result, Is.True,
                $"Address {KnownOwnerAddress} is expected to own token " +
                $"{DemoNFTContract}#{DemoTokenId} on Tezos mainnet.");
        }

        [Test]
        [Ignore("Integration test — requires live TzKT API. Remove [Ignore] to run manually.")]
        public async Task IsOwnerOfToken_KnownNonOwner_ReturnsFalse()
        {
            // Act
            bool result = await _mock.IsOwnerOfToken(
                KnownNonOwnerAddress, DemoNFTContract, DemoTokenId).AsTask();

            // Assert
            Assert.That(result, Is.False,
                $"Address {KnownNonOwnerAddress} is not expected to own token " +
                $"{DemoNFTContract}#{DemoTokenId}.");
        }

        [Test]
        [Ignore("Integration test — requires live TzKT API. Remove [Ignore] to run manually.")]
        public async Task IsOwnerOfToken_InvalidContract_ReturnsFalse()
        {
            // Verifies graceful handling of a non-existent contract address.
            // TzKT returns an empty array for unknown contracts, not an error.
            bool result = await _mock.IsOwnerOfToken(
                KnownOwnerAddress, "KT1_NOT_A_REAL_CONTRACT_ADDRESS", 0).AsTask();

            Assert.That(result, Is.False);
        }


        // ---------------------------------------------------------------
        //  GetAllTokensForOwner
        // ---------------------------------------------------------------

        [Test]
        [Ignore("Integration test — requires live TzKT API. Remove [Ignore] to run manually.")]
        public async Task GetAllTokensForOwner_KnownWallet_ReturnsNonEmptyList()
        {
            // Act
            List<TokenBalance> tokens = await _mock
                .GetAllTokensForOwner(KnownOwnerAddress).AsTask();

            // Assert
            Assert.That(tokens, Is.Not.Null);
            Assert.That(tokens, Is.Not.Empty,
                $"Address {KnownOwnerAddress} is expected to hold at least one token.");
        }

        [Test]
        [Ignore("Integration test — requires live TzKT API. Remove [Ignore] to run manually.")]
        public async Task GetAllTokensForOwner_AllTokens_HaveNonEmptyContractAddress()
        {
            // Verifies that every returned TokenBalance has a valid contract address.
            // A missing tokenContract would indicate a broken TzKT select query.
            List<TokenBalance> tokens = await _mock
                .GetAllTokensForOwner(KnownOwnerAddress).AsTask();

            foreach (TokenBalance token in tokens)
            {
                Assert.That(token.tokenContract, Is.Not.Null.And.Not.Empty,
                    $"Token ID {token.tokenId} is missing a contract address.");
            }
        }

        [Test]
        [Ignore("Integration test — requires live TzKT API. Remove [Ignore] to run manually.")]
        public async Task GetAllTokensForOwner_AllTokens_HavePositiveBalance()
        {
            // Verifies the balance.ne=0 filter in the TzKT query is working.
            // Any result with balance "0" indicates the filter is broken.
            List<TokenBalance> tokens = await _mock
                .GetAllTokensForOwner(KnownOwnerAddress).AsTask();

            foreach (TokenBalance token in tokens)
            {
                bool parsed = long.TryParse(token.balance, out long balance);
                Assert.That(parsed, Is.True,
                    $"Token {token.tokenId} balance '{token.balance}' is not a valid integer.");
                Assert.That(balance, Is.GreaterThan(0),
                    $"Token {token.tokenId} has a zero balance. The balance.ne=0 filter may not be working.");
            }
        }


        // ---------------------------------------------------------------
        //  GetTokensForOwnerPage — pagination correctness
        // ---------------------------------------------------------------

        [Test]
        [Ignore("Integration test — requires live TzKT API. Remove [Ignore] to run manually.")]
        public async Task GetTokensForOwnerPage_PageSizeOne_ReturnsExactlyOneToken()
        {
            // Act
            List<TokenBalance> page = await _mock
                .GetTokensForOwnerPage(KnownOwnerAddress, offset: 0, limit: 1).AsTask();

            // Assert
            Assert.That(page, Has.Count.LessThanOrEqualTo(1),
                "Requesting limit=1 should return at most one token.");
        }

        [Test]
        [Ignore("Integration test — requires live TzKT API. Remove [Ignore] to run manually.")]
        public async Task GetTokensForOwnerPage_SecondPage_DoesNotDuplicateFirstPage()
        {
            // This test detects the absence of &sort.asc=id, which would cause TzKT
            // to return non-deterministic ordering and potentially duplicate tokens
            // across page boundaries.
            int pageSize = 2;

            List<TokenBalance> page1 = await _mock
                .GetTokensForOwnerPage(KnownOwnerAddress, offset: 0,        limit: pageSize).AsTask();
            List<TokenBalance> page2 = await _mock
                .GetTokensForOwnerPage(KnownOwnerAddress, offset: pageSize, limit: pageSize).AsTask();

            // Collect all IDs from both pages and check for duplicates
            var allIds = new System.Collections.Generic.HashSet<long>();
            foreach (TokenBalance token in page1) allIds.Add(token.id);
            foreach (TokenBalance token in page2)
            {
                Assert.That(allIds.Add(token.id), Is.True,
                    $"Token id={token.id} appeared on both page 1 and page 2. " +
                    "Pagination ordering may be unstable — ensure &sort.asc=id is in the query.");
            }
        }

        [Test]
        [Ignore("Integration test — requires live TzKT API. Remove [Ignore] to run manually.")]
        public async Task GetAllTokensForOwnerPaginated_SmallPageSize_AssemblesSameTotalAsFullLoad()
        {
            // Verifies that paginated assembly produces the same result as a single full load.
            // A mismatch indicates either duplicate tokens across pages or missed pages.
            int pageSize = 2;

            List<TokenBalance> paginated = await _mock.GetAllTokensForOwnerPaginated(
                KnownOwnerAddress, pageSize: pageSize).AsTask();

            List<TokenBalance> fullLoad = await _mock
                .GetAllTokensForOwner(KnownOwnerAddress).AsTask();

            Assert.That(paginated.Count, Is.EqualTo(fullLoad.Count),
                $"Paginated load (pageSize={pageSize}) returned {paginated.Count} tokens, " +
                $"but full load returned {fullLoad.Count}. " +
                "Pagination may be skipping or duplicating records.");
        }
    }
}
