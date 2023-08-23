using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TezosAPI;
using TezosSDKExamples.Controllers;
using TezosSDKExamples.Shared.Tezos;
using UnityEngine;

#pragma warning disable CS4014, CS1998, CS0219
namespace TezosSDKExamples.Scenes
{
    /// <summary>
    /// EXAMPLE: Demonstrates fetching and displaying a player's full NFT inventory,
    /// including paginated loading strategies for large wallets.
    ///
    /// WEB3 CONCEPT — Player inventory in a blockchain game:
    ///   In a traditional game, a player's inventory is stored in your game's database.
    ///   You control it: you can grant items, revoke them, or wipe the database.
    ///
    ///   In a Web3 game, inventory is stored on-chain in FA2 token contracts. The player's
    ///   wallet address is the key. Any game that queries the same address sees the same
    ///   inventory — no database sync required, and no single server controls it.
    ///
    /// PAGINATION STRATEGIES DEMONSTRATED:
    ///   This example shows three ways to load inventory, each suited to a different UX:
    ///
    ///   1. First-page preview  — load only the first N tokens immediately.
    ///      Best for: inventory drawer, quick-glance panels, initial scene load.
    ///
    ///   2. Progressive full load — fetch all pages, updating the UI after each arrives.
    ///      Best for: inventory screens where you want items to appear incrementally.
    ///
    ///   3. Full silent load  — fetch all pages, display results only when complete.
    ///      Best for: loading screens, background prefetching, non-interactive contexts.
    ///
    /// HOW TO ADAPT THIS FOR YOUR GAME:
    ///   - Filter results by tokenContract to show only your game's items
    ///   - Map tokenId values to ScriptableObjects for item names, icons, and stats
    ///   - Use tokenMetadata fields (name, displayUri, attributes) to drive item cards
    ///   - Persist the full inventory list as your authoritative in-game item source
    /// </summary>
    public class Example03_PlayerInventory : Example03_Parent
    {
        //  Fields ----------------------------------------

        // ITezosAPI is the main entry point for all Tezos SDK functionality.
        // See Example01_Authentication.cs for a full explanation of this singleton.
        private ITezosAPI _tezos;

        // Tracks the current pagination offset for the manual "next page" pattern.
        // Persists between button clicks so each call advances through the inventory.
        private int _pageOffset = 0;


        //  Methods ---------------------------------------
        protected override async void Start()
        {
            // Required: Render UI
            base.Start();

            _tezos = TezosSingleton.Instance;

            // Subscribe to wallet events so the UI responds to connection changes.
            // Reset the page offset on connect so inventory always starts from the beginning.
            // IMPORTANT: Unsubscribed in OnDestroy() below to prevent memory leaks.
            _tezos.MessageReceiver.AccountConnected += Tezos_OnAccountConnected;
            _tezos.MessageReceiver.AccountDisconnected += Tezos_OnAccountDisconnected;
        }

        protected void OnDestroy()
        {
            if (_tezos != null)
            {
                _tezos.MessageReceiver.AccountConnected -= Tezos_OnAccountConnected;
                _tezos.MessageReceiver.AccountDisconnected -= Tezos_OnAccountDisconnected;
            }
        }


        //  Event Handlers --------------------------------

        private async void Tezos_OnAccountConnected(string address)
        {
            // Reset pagination state when a new wallet connects
            _pageOffset = 0;
            await RefreshUIAsync();
            Debug.Log($"[Example03] Wallet connected: <b>{address}</b>. Ready to fetch inventory.");
        }

        private async void Tezos_OnAccountDisconnected(string address)
        {
            _pageOffset = 0;
            await RefreshUIAsync();
            Debug.Log($"[Example03] Wallet disconnected. Inventory cleared.");
        }


        // INVENTORY FETCH — demonstrates all three pagination strategies
        //
        // In a real game, choose one strategy per screen. They are all shown here
        // for illustration — in production, each would be a separate button or trigger.
        protected override async UniTask OnRefreshInventoryButtonClicked()
        {
            // Required: Render UI
            base.OnRefreshInventoryButtonClicked();

            string activeWalletAddress = _tezos.GetActiveWalletAddress();

            await ShowDialogAsync("Player Inventory", async () =>
            {
                await LoadInventoryAsync(activeWalletAddress);
            });
        }


        // INVENTORY LOADING — three strategies in sequence for demonstration
        //
        // Remove the strategies you do not need and keep only the one that fits
        // your game's UX. They are separated into named methods so each pattern
        // is clearly readable on its own.
        private async UniTask LoadInventoryAsync(string walletAddress)
        {
            // ---------------------------------------------------------------
            // STRATEGY 1: First-page preview
            // ---------------------------------------------------------------
            // Fetch only the first DefaultPageSize tokens. Fast round-trip, minimal
            // memory. Use when you only need to show a preview or the first N items.
            await LoadFirstPageAsync(walletAddress);

            // ---------------------------------------------------------------
            // STRATEGY 2: Progressive full load (recommended for inventory screens)
            // ---------------------------------------------------------------
            // Fetch all tokens page by page. The onPageLoaded callback fires after
            // each page so the UI can update incrementally — the player sees items
            // appear rather than waiting for the entire inventory.
            await LoadProgressivelyAsync(walletAddress);

            // ---------------------------------------------------------------
            // STRATEGY 3: Full silent load
            // ---------------------------------------------------------------
            // Fetch all pages with no per-page callback. Waits for the complete
            // inventory before doing anything. Use for background prefetching or
            // when displaying a loading screen rather than incremental UI.
            await LoadAllAtOnceAsync(walletAddress);
        }


        // STRATEGY 1 — Single page preview
        //
        // GetTokensForOwnerPage() maps directly to one TzKT HTTP request:
        //   GET /v1/tokens/balances?account={addr}&offset=0&limit=10&sort.asc=id
        //
        // Call it again with a higher offset to implement "Load More":
        //   page 1: offset=0,  limit=10
        //   page 2: offset=10, limit=10
        //   page N: offset=(N-1)*limit, limit=10
        //
        // The returned list will have fewer items than limit when you reach the last page.
        private async UniTask LoadFirstPageAsync(string walletAddress)
        {
            Debug.Log($"[Example03] Strategy 1 — Fetching first {TezosExtensions.DefaultPageSize} tokens...");

            List<TokenBalance> firstPage = await _tezos.GetTokensForOwnerPage(
                walletAddress,
                offset: 0,
                limit: TezosExtensions.DefaultPageSize);

            bool isLastPage = firstPage.Count < TezosExtensions.DefaultPageSize;

            Debug.Log($"[Example03] Strategy 1 — First page: {firstPage.Count} token(s). " +
                      $"{(isLastPage ? "This is the only page." : "More pages available.")}");

            foreach (TokenBalance token in firstPage)
            {
                Debug.Log($"[Example03]   • {token.tokenContract}#{token.tokenId} (balance: {token.balance})");
            }

            string preview = $"<b>Preview (first {TezosExtensions.DefaultPageSize} tokens):</b>\n" +
                             FormatTokenList(firstPage) +
                             (isLastPage ? "" : $"\n...and more. Use progressive load to see all.\n");

            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = preview;
        }


        // STRATEGY 2 — Progressive full load
        //
        // GetAllTokensForOwnerPaginated() fetches every page internally, calling
        // onPageLoaded after each one. Use the callback to update your inventory
        // UI immediately as each page arrives — players see items appear in batches
        // rather than waiting for the full load to finish.
        //
        // The callback receives:
        //   List<TokenBalance> page      — the tokens in this page
        //   int                pageIndex — zero-based page index (0 = first page)
        private async UniTask LoadProgressivelyAsync(string walletAddress)
        {
            Debug.Log($"[Example03] Strategy 2 — Progressive load (page size: {TezosExtensions.DefaultPageSize})...");

            int totalLoaded = 0;
            string accumulatedDisplay = "<b>Full inventory (loaded progressively):</b>\n";

            List<TokenBalance> allTokens = await _tezos.GetAllTokensForOwnerPaginated(
                walletAddress,
                pageSize: TezosExtensions.DefaultPageSize,
                onPageLoaded: (page, pageIndex) =>
                {
                    // This callback fires on the Unity main thread after each page arrives.
                    // Safe to call UnityEngine APIs (Instantiate, SetText, etc.) here.
                    totalLoaded += page.Count;

                    Debug.Log($"[Example03] Strategy 2 — Page {pageIndex + 1}: " +
                              $"{page.Count} token(s) ({totalLoaded} total so far)");

                    // In a real inventory screen, spawn item card prefabs here:
                    //   foreach (TokenBalance token in page) { SpawnInventoryCard(token); }
                    //
                    // For this example, append each page to the display text so the
                    // incremental arrival of pages is visible in the UI.
                    accumulatedDisplay += $"\n[Page {pageIndex + 1}]\n" + FormatTokenList(page);
                    View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = accumulatedDisplay;
                });

            Debug.Log($"[Example03] Strategy 2 — Complete. {allTokens.Count} total token(s) loaded.");
        }


        // STRATEGY 3 — Full silent load
        //
        // GetAllTokensForOwner() fetches all pages silently and returns the complete list.
        // Use this when you want to process the full inventory at once — e.g. populating
        // a crafting system, running a full token-gate check across a collection, or
        // prefetching inventory in the background during a loading screen.
        private async UniTask LoadAllAtOnceAsync(string walletAddress)
        {
            Debug.Log($"[Example03] Strategy 3 — Full silent load...");

            List<TokenBalance> allTokens = await _tezos.GetAllTokensForOwner(walletAddress);

            Debug.Log($"[Example03] Strategy 3 — Complete. {allTokens.Count} total token(s).");

            string summary = $"<b>Full inventory ({allTokens.Count} token(s) total):</b>\n" +
                             FormatTokenList(allTokens);

            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = summary;
            base.RefreshUIAsync();
        }


        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        // Formats a flat list of TokenBalance into a UI-ready string.
        // Replace this with prefab instantiation or data binding in a real inventory screen.
        private static string FormatTokenList(List<TokenBalance> tokens)
        {
            if (tokens.Count == 0)
                return "  (none)\n";

            string result = "";
            foreach (TokenBalance token in tokens)
            {
                result += $"  • tokenId = <b>{token.tokenId}</b>, " +
                          $"balance = <b>{token.balance}</b>, " +
                          $"contract = {token.tokenContract}\n";
            }
            return result;
        }
    }
}
