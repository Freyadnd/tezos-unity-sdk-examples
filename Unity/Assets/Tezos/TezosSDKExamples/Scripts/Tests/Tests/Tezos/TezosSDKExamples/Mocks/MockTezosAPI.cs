using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using Beacon.Sdk.Beacon.Sign;
using TezosAPI;
using UnityEngine;

namespace TezosSDKExamples.Tests.Mocks
{
    /// <summary>
    /// Hand-written test double for <see cref="ITezosAPI"/>.
    ///
    /// WHY A MANUAL MOCK INSTEAD OF A FRAMEWORK (e.g. Moq, NSubstitute)?
    ///   This project does not include a mocking library. Adding one requires importing
    ///   a DLL and updating the test assembly definition — a non-trivial project change.
    ///   A manual mock is more verbose but has no dependencies and is easier to read
    ///   for developers who are new to testing.
    ///
    ///   To replace this with Moq, install the Moq NuGet package and write:
    ///     var mock = new Mock&lt;ITezosAPI&gt;();
    ///     mock.Setup(t => t.GetActiveWalletAddress()).Returns("tz1abc...");
    ///     ITezosAPI tezos = mock.Object;
    ///
    /// HOW TO USE THIS MOCK IN TESTS:
    ///   1. Instantiate it:     var mock = new MockTezosAPI();
    ///   2. Configure it:       mock.ActiveWalletAddress = "tz1abc...123";
    ///   3. Pass it as API:     bool result = mock.HasActiveWalletAddress();
    ///   4. Inspect calls:      Assert.That(mock.ContractCallLog, Has.Count.EqualTo(1));
    ///   5. Reset between tests: mock.Reset();
    ///
    /// WHAT THIS MOCK DOES NOT COVER:
    ///   - MessageReceiver events (BeaconMessageReceiver is a MonoBehaviour; it cannot
    ///     be instantiated in an EditMode test without a GameObject. Tests that need
    ///     event behaviour should use Unity PlayMode tests instead.)
    ///   - Coroutine-based methods (ReadBalance, ReadView, GetTokensForOwner) are stubbed
    ///     to yield nothing. Wrap them in PlayMode tests to exercise coroutine behaviour.
    /// </summary>
    public class MockTezosAPI : ITezosAPI
    {
        // ---------------------------------------------------------------
        //  Configuration — set these before calling methods under test
        // ---------------------------------------------------------------

        /// <summary>
        /// The address returned by <see cref="GetActiveWalletAddress"/>.
        /// Set to a valid tz1... string to simulate an authenticated player.
        /// Leave null to simulate no wallet connected.
        /// </summary>
        public string ActiveWalletAddress { get; set; } = null;

        /// <summary>
        /// The result returned by <see cref="VerifySignedPayload"/>.
        /// Defaults to false (signature not verified).
        /// </summary>
        public bool VerifySignedPayloadResult { get; set; } = false;


        // ---------------------------------------------------------------
        //  Call log — inspect these in assertions
        // ---------------------------------------------------------------

        /// <summary>True if <see cref="ConnectWallet"/> was called at least once.</summary>
        public bool ConnectWalletCalled { get; private set; }

        /// <summary>True if <see cref="DisconnectWallet"/> was called at least once.</summary>
        public bool DisconnectWalletCalled { get; private set; }

        /// <summary>
        /// Every <see cref="CallContract"/> invocation in order.
        /// Each entry captures the three string arguments for assertion.
        /// </summary>
        public List<ContractCallRecord> ContractCallLog { get; } = new List<ContractCallRecord>();

        /// <summary>
        /// Represents one recorded <see cref="CallContract"/> invocation.
        /// </summary>
        public struct ContractCallRecord
        {
            public string ContractAddress;
            public string EntryPoint;
            public string Input;
            public ulong  Amount;
        }


        // ---------------------------------------------------------------
        //  Reset — call in [TearDown] to isolate tests
        // ---------------------------------------------------------------

        /// <summary>
        /// Resets all call logs and restores default configuration.
        /// Call this in a [TearDown] method to prevent test state from leaking.
        /// </summary>
        public void Reset()
        {
            ActiveWalletAddress      = null;
            VerifySignedPayloadResult = false;
            ConnectWalletCalled      = false;
            DisconnectWalletCalled   = false;
            ContractCallLog.Clear();
        }


        // ---------------------------------------------------------------
        //  ITezosAPI implementation
        // ---------------------------------------------------------------

        // NetworkRPC is not exercised in unit tests — returns a placeholder.
        public string NetworkRPC => "https://rpc.ghostnet.teztnets.xyz (mock)";

        // MessageReceiver is a MonoBehaviour and cannot be instantiated in EditMode tests.
        // Any test that needs to fire or observe MessageReceiver events must use
        // Unity PlayMode tests with a proper scene setup.
        public BeaconMessageReceiver MessageReceiver => null;

        public void ConnectWallet()    => ConnectWalletCalled    = true;
        public void DisconnectWallet() => DisconnectWalletCalled = true;

        public string GetActiveWalletAddress() => ActiveWalletAddress;

        public void CallContract(string contractAddress, string entryPoint, string input, ulong amount = 0)
        {
            ContractCallLog.Add(new ContractCallRecord
            {
                ContractAddress = contractAddress,
                EntryPoint      = entryPoint,
                Input           = input,
                Amount          = amount
            });
        }

        public void RequestPermission() { /* no-op for unit tests */ }

        public void RequestSignPayload(SignPayloadType signingType, string payload)
        { /* no-op for unit tests */ }

        public bool VerifySignedPayload(SignPayloadType signingType, string payload)
            => VerifySignedPayloadResult;

        // Coroutine stubs — yield immediately without doing anything.
        // To test coroutine-based behaviour, use Unity PlayMode tests.
        public IEnumerator ReadBalance(Action<ulong> callback)
        {
            yield break;
        }

        public IEnumerator ReadView(
            string contractAddress, string entryPoint, object input, Action<JsonElement> callback)
        {
            yield break;
        }

        public IEnumerator GetTokensForOwner(
            Action<IEnumerable<TokenBalance>> cb,
            string owner,
            bool withMetadata,
            long maxItems,
            TokensForOwnerOrder orderBy)
        {
            yield break;
        }
    }
}
