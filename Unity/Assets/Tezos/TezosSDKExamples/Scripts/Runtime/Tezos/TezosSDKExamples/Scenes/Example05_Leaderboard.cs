using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using TezosAPI;
using TezosSDKExamples.Controllers;
using TezosSDKExamples.Shared.Tezos;
using UnityEngine;

#pragma warning disable CS4014, CS1998, CS0219
namespace TezosSDKExamples.Scenes
{
    /// <summary>
    /// EXAMPLE: Demonstrates a blockchain leaderboard — submitting player scores
    /// to a smart contract and reading the leaderboard back from on-chain storage.
    ///
    /// MOCK NOTE:
    ///   Score submission uses the real Tezos SDK CallContract() pattern. Because no
    ///   live leaderboard contract is deployed for this example, UseMockContract = true
    ///   by default, which simulates the on-chain response locally so the scene runs
    ///   without a wallet. Set UseMockContract = false and supply a real contract address
    ///   to send actual on-chain transactions.
    ///   Leaderboard reads (ViewLeaderboard) are always mocked with demo data; the
    ///   comments show exactly what the real ReadView() call would look like.
    ///
    /// WEB3 CONCEPT — Why put a leaderboard on-chain?
    ///   A traditional leaderboard is a database table your server controls. You decide
    ///   what scores are valid, you can edit or delete entries, and players must trust
    ///   that you are not manipulating the rankings.
    ///
    ///   An on-chain leaderboard stores scores in a smart contract. The contract's rules
    ///   (e.g. "only one submission per address per day", "scores must be positive") are
    ///   enforced by the Tezos network — not your server. Every submission is a signed
    ///   transaction that is permanently auditable. No player or developer can silently
    ///   alter the rankings after the fact.
    ///
    ///   Properties of an on-chain leaderboard:
    ///   - Tamper-proof: scores are locked once the block is confirmed
    ///   - Trustless: rankings are verifiable by anyone on TzKT without trusting you
    ///   - Persistent: the leaderboard survives game shutdowns, server migrations, etc.
    ///   - Composable: other games or websites can read the same leaderboard contract
    ///
    /// WEB3 CONCEPT — Leaderboard contract design patterns:
    ///
    ///   1. Player-signed submission (this example)
    ///      The player's wallet signs each score submission. The contract can enforce
    ///      rules like "one submission per address" or "only increasing scores accepted".
    ///      Best for: competitive games where anti-cheat is critical.
    ///      Trade-off: every score update costs the player a small gas fee and requires
    ///      wallet approval, which interrupts gameplay if done frequently.
    ///
    ///   2. Backend-signed submission
    ///      Your game server validates the score server-side, then a backend wallet
    ///      calls the contract's "submit_score" entrypoint on the player's behalf.
    ///      The contract stores the player's address alongside the server-submitted score.
    ///      Best for: games where scores are computed server-side (anti-cheat) but you
    ///      still want the on-chain transparency and persistence guarantees.
    ///      Trade-off: introduces a trusted intermediary (your server).
    ///
    ///   3. End-of-season settlement
    ///      Scores accumulate off-chain during a season. At season end, a Merkle root
    ///      of all final scores is written to a contract. Players claim rewards by
    ///      providing a Merkle proof. No per-submission gas cost.
    ///      Best for: high-frequency scoring games (idle games, match-based shooters).
    ///      Trade-off: complex contract logic; rankings are not live on-chain mid-season.
    ///
    /// WEB3 CONCEPT — How score reads work (ReadView):
    ///   Tezos smart contracts can expose on-chain views — read-only entrypoints that
    ///   return computed data from contract storage without creating a transaction.
    ///   The SDK's ReadView() method calls these views via the Tezos RPC node.
    ///   No wallet signature or gas fee is required for a read.
    ///   See OnViewLeaderboardButtonClicked() for the real ReadView() call pattern.
    ///
    /// EXTENDING THIS EXAMPLE INTO PLAYER MARKETPLACES:
    ///   The same CallContract() pattern used for score submission generalises to
    ///   marketplace actions. A marketplace contract might expose:
    ///     - "list_item"    — list an NFT for sale at a fixed tez price
    ///     - "buy_item"     — purchase a listed item (send tez with the call via 'amount')
    ///     - "cancel_listing" — remove your own listing
    ///     - "make_offer"   — place a bid on any token
    ///   Each of these is a CallContract() call with the relevant entrypoint name
    ///   and a Micheline-encoded parameter. The pattern in SubmitScoreAsync() applies
    ///   directly — swap the entrypoint name and parameter encoding.
    /// </summary>
    public class Example05_Leaderboard : Example05_Parent
    {
        //  Configuration ----------------------------------------

        // Set to false and provide a real LeaderboardContract address to send actual
        // on-chain transactions. When true, the contract call is simulated locally
        // so the scene runs without a wallet or a deployed contract.
        private const bool UseMockContract = true;

        // The KT1... address of a deployed leaderboard smart contract.
        // Unused when UseMockContract = true.
        // Deploy your own contract using the Michelson template shown in the comments
        // of BuildSubmitScoreParameters() below, or use an existing one.
        private const string LeaderboardContract = "KT1_REPLACE_WITH_YOUR_CONTRACT_ADDRESS";

        // How many demo entries appear in the mock leaderboard view.
        private const int MockLeaderboardSize = 5;


        //  Fields ----------------------------------------

        private ITezosAPI _tezos;

        // Guards against double-submitting while a wallet confirmation is pending.
        private bool _isSubmitPending = false;

        // Simulated player score. Increments on each submission for demo variety.
        // In a real game this would come from your game's score system.
        private int _localScore = 0;


        //  Mock leaderboard data ----------------------------------------

        // Represents one row in the leaderboard.
        private struct LeaderboardEntry
        {
            public int    Rank;
            public string WalletAddress;
            public int    Score;
        }

        // Static demo data used when reading the leaderboard in mock mode.
        // In production, these values come from ReadView() against the contract.
        private static readonly LeaderboardEntry[] MockEntries =
        {
            new LeaderboardEntry { Rank = 1, WalletAddress = "tz1AAA...111", Score = 98_500 },
            new LeaderboardEntry { Rank = 2, WalletAddress = "tz1BBB...222", Score = 87_200 },
            new LeaderboardEntry { Rank = 3, WalletAddress = "tz1CCC...333", Score = 74_100 },
            new LeaderboardEntry { Rank = 4, WalletAddress = "tz1DDD...444", Score = 65_800 },
            new LeaderboardEntry { Rank = 5, WalletAddress = "tz1EEE...555", Score = 51_300 },
        };


        //  Methods ----------------------------------------

        protected override async void Start()
        {
            base.Start();

            _tezos = TezosSingleton.Instance;

            // Subscribe to wallet and contract call events.
            // IMPORTANT: All unsubscribed in OnDestroy() below.
            _tezos.MessageReceiver.AccountConnected      += Tezos_OnAccountConnected;
            _tezos.MessageReceiver.AccountDisconnected   += Tezos_OnAccountDisconnected;
            _tezos.MessageReceiver.ContractCallInjected  += Tezos_OnContractCallInjected;
            _tezos.MessageReceiver.ContractCallCompleted += Tezos_OnContractCallCompleted;
            _tezos.MessageReceiver.ContractCallFailed    += Tezos_OnContractCallFailed;
        }

        protected void OnDestroy()
        {
            if (_tezos != null)
            {
                _tezos.MessageReceiver.AccountConnected      -= Tezos_OnAccountConnected;
                _tezos.MessageReceiver.AccountDisconnected   -= Tezos_OnAccountDisconnected;
                _tezos.MessageReceiver.ContractCallInjected  -= Tezos_OnContractCallInjected;
                _tezos.MessageReceiver.ContractCallCompleted -= Tezos_OnContractCallCompleted;
                _tezos.MessageReceiver.ContractCallFailed    -= Tezos_OnContractCallFailed;
            }
        }


        //  Event Handlers — wallet state ----------------------------------------

        private async void Tezos_OnAccountConnected(string address)
        {
            await RefreshUIAsync();
            Debug.Log($"[Example05] Wallet connected: <b>{address}</b>. Ready to submit scores.");
        }

        private async void Tezos_OnAccountDisconnected(string address)
        {
            _isSubmitPending = false;
            await RefreshUIAsync();
            Debug.Log($"[Example05] Wallet disconnected.");
        }


        //  Event Handlers — transaction lifecycle ----------------------------------------

        // Fired when the score submission enters the mempool.
        // Show a "submitting..." state in your game UI — the score is not confirmed yet.
        private async void Tezos_OnContractCallInjected(string operationHash)
        {
            string message = $"Score submission injected.\nOperation: <b>{operationHash}</b>\n\n" +
                             $"Waiting for on-chain confirmation (~30s)...";
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = message;
            Debug.Log($"[Example05] Score injected. Track: https://tzkt.io/{operationHash}");
        }

        // Fired when the score is confirmed in a block.
        // The leaderboard contract's storage now reflects the new score.
        // Safe to call ReadView() here to refresh the leaderboard display.
        private async void Tezos_OnContractCallCompleted(string operationHash)
        {
            _isSubmitPending = false;

            string message = $"Score confirmed on-chain!\nOperation: <b>{operationHash}</b>\n\n" +
                             $"Score <b>{_localScore}</b> is now permanently recorded.\n" +
                             $"Click <b>View Leaderboard</b> to refresh the rankings.\n\n" +
                             $"Track: https://tzkt.io/{operationHash}";
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = message;

            await RefreshUIAsync();
            Debug.Log($"[Example05] Score confirmed. Operation: {operationHash}");
        }

        // Fired when the contract call is rejected or fails on-chain.
        // Common leaderboard contract rejection reasons are listed below.
        private async void Tezos_OnContractCallFailed(string errorMessage)
        {
            _isSubmitPending = false;

            string message = $"Score submission failed.\n\n<b>Reason:</b> {errorMessage}\n\n" +
                             $"Common causes:\n" +
                             $" • Score is not higher than your existing on-chain record\n" +
                             $" • Submission limit reached for this period\n" +
                             $" • Insufficient tez balance for gas fees\n" +
                             $" • Contract paused or in maintenance";
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = message;

            await RefreshUIAsync();
            Debug.LogError($"[Example05] Score submission failed: {errorMessage}");
        }


        //  Button handlers ----------------------------------------

        protected override async UniTask OnSubmitScoreButtonClicked()
        {
            base.OnSubmitScoreButtonClicked();

            if (_isSubmitPending)
            {
                Debug.LogWarning("[Example05] Score submission already pending. Wait for confirmation.");
                return;
            }

            await ShowDialogAsync("Submit Score", async () =>
            {
                await SubmitScoreAsync();
            });
        }

        protected override async UniTask OnViewLeaderboardButtonClicked()
        {
            base.OnViewLeaderboardButtonClicked();

            await ShowDialogAsync("Leaderboard", async () =>
            {
                await ReadLeaderboardAsync();
            });
        }


        //  Score submission ----------------------------------------

        // SCORE SUBMISSION — two paths controlled by UseMockContract.
        //
        // Both paths build and log the same Micheline parameter string so you can
        // see exactly what would be sent to the real contract. The only difference
        // is whether CallContract() is actually invoked.
        private async UniTask SubmitScoreAsync()
        {
            // Simulate earning a score. In a real game, read from your scoring system:
            //   int score = GameManager.Instance.CurrentScore;
            _localScore += Random.Range(1000, 9999);

            string walletAddress = _tezos.GetActiveWalletAddress();

            // MICHELINE ENCODING — nat (natural number)
            // The submit_score entrypoint takes a single nat parameter (the score value).
            // Micheline encodes a nat as: {"int": "<value>"}
            // See BuildSubmitScoreParameters() for the full encoding explanation.
            string scoreParameter = BuildSubmitScoreParameters(_localScore);

            Debug.Log($"[Example05] Submitting score...");
            Debug.Log($"[Example05]   Wallet:     {walletAddress}");
            Debug.Log($"[Example05]   Score:      {_localScore}");
            Debug.Log($"[Example05]   Contract:   {(UseMockContract ? "(mock)" : LeaderboardContract)}");
            Debug.Log($"[Example05]   Parameters: {scoreParameter}");

            if (UseMockContract)
            {
                // MOCK PATH — simulates the full submission → injected → confirmed flow
                // locally without a wallet or real contract. Fires the same contract call
                // events (via simulated messages) so the UI responds identically to a
                // real on-chain flow. Remove this block when UseMockContract = false.
                await SimulateMockSubmissionAsync(walletAddress);
            }
            else
            {
                // PRODUCTION PATH — real on-chain score submission.
                //
                // CallContract() sends the operation to the player's Beacon wallet for signing.
                // The player approves the transaction in their wallet app.
                // ContractCallInjected fires → ContractCallCompleted fires on block inclusion.
                //
                // The leaderboard contract's submit_score entrypoint would typically:
                //   1. Verify the caller matches the score's claimed wallet address
                //   2. Optionally reject if the new score <= the existing on-chain score
                //   3. Update big_map storage: wallet_address → score
                //
                _isSubmitPending = true;

                _tezos.CallContract(
                    contractAddress: LeaderboardContract,
                    entryPoint:      "submit_score",
                    input:           scoreParameter,
                    amount:          0);

                string pendingMessage =
                    $"Score <b>{_localScore}</b> submitted for wallet <b>{walletAddress}</b>.\n\n" +
                    $"Waiting for wallet approval in your wallet app...";
                View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = pendingMessage;
            }
        }

        // MOCK SUBMISSION — simulates the injected → confirmed lifecycle locally.
        //
        // Fires the same ContractCallInjected and ContractCallCompleted handler methods
        // that the real SDK events would trigger, so the full UI flow is exercised
        // without any network calls.
        private async UniTask SimulateMockSubmissionAsync(string walletAddress)
        {
            string fakeOperationHash = $"op{System.Guid.NewGuid().ToString("N").Substring(0, 20)}";

            // Simulate wallet signing delay (~0.5s)
            string signingMessage =
                $"[MOCK] Score <b>{_localScore}</b> submitted.\n\n" +
                $"Simulating wallet approval...";
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = signingMessage;
            await UniTask.Delay(500);

            // Simulate mempool injection
            Tezos_OnContractCallInjected(fakeOperationHash);
            await UniTask.Delay(1500);

            // Simulate block confirmation (~2s in mock vs ~30s on real network)
            Tezos_OnContractCallCompleted(fakeOperationHash);

            Debug.Log($"[Example05] Mock submission complete. " +
                      $"In production, this operation hash links to a real TzKT entry.");
        }


        //  Leaderboard read ----------------------------------------

        // LEADERBOARD READ — always uses mock data in this example.
        //
        // In production, replace the mock block with the ReadView() call shown below.
        // ReadView() invokes an on-chain view entrypoint — a read-only contract call
        // that returns data from contract storage with no transaction or gas cost.
        private async UniTask ReadLeaderboardAsync()
        {
            string walletAddress = _tezos.HasActiveWalletAddress()
                ? _tezos.GetActiveWalletAddress()
                : null;

            Debug.Log($"[Example05] Reading leaderboard...");

            List<LeaderboardEntry> entries;

            // MOCK PATH — returns static demo data immediately.
            // Swap for the production ReadView() block below to read from a real contract.
            entries = BuildMockLeaderboard(walletAddress, _localScore);

            // ---------------------------------------------------------------
            // PRODUCTION PATH (replace the mock block above with this):
            //
            //   bool received = false;
            //   List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
            //
            //   StartCoroutine(_tezos.ReadView(
            //       contractAddress: LeaderboardContract,
            //       entryPoint:      "get_leaderboard",
            //       input:           new { limit = MockLeaderboardSize },
            //       callback: (JsonElement result) =>
            //       {
            //           // result is the raw Micheline JSON returned by the contract view.
            //           // Parse it into LeaderboardEntry objects here.
            //           // The exact structure depends on your contract's return type.
            //           entries = ParseLeaderboardView(result);
            //           received = true;
            //       }));
            //
            //   await UniTask.WaitUntil(() => received);
            //
            // HOW ReadView() WORKS:
            //   - No wallet or signature required — reads are free and instant
            //   - The contract must expose a named view entrypoint (e.g. get_leaderboard)
            //   - 'input' is passed as the view parameter (can be Unit / empty object)
            //   - The callback fires on the main thread with the Micheline JSON result
            //   - Parse result using JsonDocument or a custom Micheline deserialiser
            // ---------------------------------------------------------------

            string display = FormatLeaderboard(entries, walletAddress);
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = display;

            Debug.Log($"[Example05] Leaderboard loaded. {entries.Count} entries.");
            foreach (LeaderboardEntry entry in entries)
                Debug.Log($"[Example05]   #{entry.Rank}  {entry.WalletAddress}  {entry.Score:N0}");
        }


        //  Micheline encoding ----------------------------------------

        // SCORE PARAMETER — Micheline encoding for a nat value.
        //
        // The submit_score entrypoint takes a single nat (natural number).
        // Micheline represents a nat as: {"int": "<decimal string>"}
        //
        // Example for score 12345: {"int":"12345"}
        //
        // HYPOTHETICAL CONTRACT MICHELSON (for reference):
        //   parameter (nat %submit_score);
        //   storage   (big_map address nat);    (* wallet_address → best_score *)
        //   code {
        //     UNPAIR;                           (* score : storage *)
        //     SENDER;                           (* sender : score : storage *)
        //     DIG 2;                            (* storage : sender : score *)
        //     DUP 2;                            (* sender : storage : sender : score *)
        //     MEM;                              (* exists : storage : sender : score *)
        //     IF {
        //       (* existing entry — only update if new score is higher *)
        //       DUP; DUP 3; GET; IF_SOME {
        //         DUP 4; COMPARE; GT;           (* new > existing? *)
        //         IF { UPDATE } { DROP 2; DROP }
        //       } { DROP }
        //     } {
        //       (* new entry — insert unconditionally *)
        //       DIG 2; SOME; DIG 2; UPDATE
        //     };
        //     NIL operation; PAIR
        //   }
        private static string BuildSubmitScoreParameters(int score)
        {
            // A nat in Micheline is simply an object with a single "int" field.
            // The value is a decimal string, not a JSON number, because Micheline
            // integers have arbitrary precision and JSON numbers do not.
            return $"{{\"int\":\"{score}\"}}";
        }


        //  Mock helpers ----------------------------------------

        // Builds a mock leaderboard list, inserting the connected player at the
        // correct rank position based on their current local score.
        private List<LeaderboardEntry> BuildMockLeaderboard(string walletAddress, int playerScore)
        {
            List<LeaderboardEntry> all = new List<LeaderboardEntry>(MockEntries);

            // Insert the connected player's local score if they have submitted one
            if (walletAddress != null && playerScore > 0)
            {
                all.Add(new LeaderboardEntry
                {
                    Rank          = 0,    // assigned after sort
                    WalletAddress = walletAddress,
                    Score         = playerScore
                });
            }

            // Sort descending by score, then assign sequential ranks
            all.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (int i = 0; i < all.Count; i++)
            {
                LeaderboardEntry entry = all[i];
                entry.Rank = i + 1;
                all[i] = entry;
            }

            // Cap at MockLeaderboardSize
            if (all.Count > MockLeaderboardSize)
                all = all.GetRange(0, MockLeaderboardSize);

            return all;
        }

        // Formats leaderboard entries into a UI-ready string.
        // Replace this with a prefab-based leaderboard row in a real game.
        private static string FormatLeaderboard(List<LeaderboardEntry> entries, string highlightAddress)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Leaderboard</b>  [MOCK DATA]\n");

            foreach (LeaderboardEntry entry in entries)
            {
                // Highlight the connected player's row
                bool isPlayer = entry.WalletAddress == highlightAddress;
                string rank   = $"#{entry.Rank}";
                string addr   = entry.WalletAddress.Length > 12
                    ? entry.WalletAddress.Substring(0, 8) + "..." + entry.WalletAddress.Substring(entry.WalletAddress.Length - 4)
                    : entry.WalletAddress;

                if (isPlayer)
                    sb.AppendLine($"<b>{rank,-4} {addr,-16} {entry.Score,8:N0}  ← YOU</b>");
                else
                    sb.AppendLine($"{rank,-4} {addr,-16} {entry.Score,8:N0}");
            }

            sb.AppendLine("\n<size=24>[MOCK] In production, these scores come from ReadView().\n" +
                          "See the comments in ReadLeaderboardAsync() for the real call.</size>");

            return sb.ToString();
        }
    }
}
