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
    /// EXAMPLE: Demonstrates fetching and displaying a player's full NFT inventory.
    ///
    /// WEB3 CONCEPT — Player inventory in a blockchain game:
    ///   In a traditional game, a player's inventory is stored in your game's database.
    ///   You control it: you can grant items, revoke them, or wipe the database.
    ///
    ///   In a Web3 game, inventory is stored on-chain in FA2 token contracts. The player's
    ///   wallet address is the key. Any game that queries the same wallet address sees the
    ///   same inventory — no database sync required, and no single server controls it.
    ///
    ///   Practical implications:
    ///   - Items can persist across game versions, relaunches, or studio shutdowns
    ///   - Players can trade or sell items without your game's involvement
    ///   - Your game does not need to store or replicate inventory state
    ///   - The inventory is the same whether the player opens your game or another one
    ///
    /// WHAT THIS EXAMPLE SHOWS:
    ///   1. Subscribe to wallet connection events (required before any inventory fetch)
    ///   2. Fetch all FA2 token balances for the connected wallet via the TzKT API
    ///   3. Iterate the results and log each item's contract, token ID, and balance
    ///   4. Display a formatted inventory summary in the scene UI
    ///
    /// HOW TO ADAPT THIS FOR YOUR GAME:
    ///   - Filter tokenBalances by your own contract address to show only your game's items
    ///   - Map token IDs to ScriptableObjects to drive item names, icons, and stats
    ///   - Use tokenBalance.tokenMetadata to read on-chain name, description, and image URL
    ///   - Store the list as the authoritative source for your in-game inventory screen
    /// </summary>
    public class Example03_PlayerInventory : Example03_Parent
    {
        //  Fields ----------------------------------------

        // ITezosAPI is the main entry point for all Tezos SDK functionality.
        // See Example01_Authentication.cs for a full explanation of this singleton.
        private ITezosAPI _tezos;


        //  Methods ---------------------------------------
        protected override async void Start()
        {
            // Required: Render UI
            base.Start();

            // Retrieve the SDK singleton. All blockchain calls go through this interface.
            _tezos = TezosSingleton.Instance;

            // Subscribe to wallet events so the UI and inventory respond to the player
            // connecting or disconnecting their wallet at any point during the session.
            // IMPORTANT: Unsubscribe these in OnDestroy() to prevent memory leaks.
            _tezos.MessageReceiver.AccountConnected += Tezos_OnAccountConnected;
            _tezos.MessageReceiver.AccountDisconnected += Tezos_OnAccountDisconnected;
        }

        protected void OnDestroy()
        {
            // Unsubscribe to avoid invoking callbacks on a destroyed MonoBehaviour.
            // This mirrors the subscription in Start() and is required for any scene
            // that may be unloaded while a wallet session is still active.
            if (_tezos != null)
            {
                _tezos.MessageReceiver.AccountConnected -= Tezos_OnAccountConnected;
                _tezos.MessageReceiver.AccountDisconnected -= Tezos_OnAccountDisconnected;
            }
        }


        //  Event Handlers --------------------------------

        // Fires when the Beacon handshake completes and a wallet address is available.
        // This is the earliest safe point to fetch inventory — GetActiveWalletAddress()
        // will now return a valid tz1... address to query against.
        private async void Tezos_OnAccountConnected(string address)
        {
            // Required: Render UI
            await RefreshUIAsync();

            Debug.Log($"[Example03] Wallet connected: <b>{address}</b>. Ready to fetch inventory.");
        }


        // Fires when the Beacon session ends. Clear any displayed inventory — the
        // address that was queried is no longer the verified active player.
        private async void Tezos_OnAccountDisconnected(string address)
        {
            // Required: Render UI
            await RefreshUIAsync();

            Debug.Log($"[Example03] Wallet disconnected. Inventory cleared.");
        }


        // INVENTORY FETCH — All FA2 tokens owned by the connected wallet
        //
        // Called when the player clicks "Refresh Inventory". Queries the TzKT indexer
        // for every token balance greater than zero held by the active wallet address,
        // across all FA2 contracts on the Tezos network.
        protected override async UniTask OnRefreshInventoryButtonClicked()
        {
            // Required: Render UI
            base.OnRefreshInventoryButtonClicked();

            // The connected player's wallet address. This is the on-chain identity used
            // to look up token ownership — equivalent to a player ID in a traditional game.
            string activeWalletAddress = _tezos.GetActiveWalletAddress();

            // INVENTORY FETCH — GetAllTokensForOwner()
            //
            // This call queries: GET https://api.tzkt.io/v1/tokens/balances
            //   ?account={address}&balance.ne=0&select=...
            //
            // It returns every FA2 token the wallet holds with a non-zero balance.
            // Each TokenBalance in the list represents one distinct token type and exposes:
            //   tokenContract  — KT1... address of the FA2 collection contract
            //   tokenId        — ID of the specific token within that contract
            //   balance        — quantity held (>1 for fungible/semi-fungible tokens)
            //   tokenMetadata  — on-chain JSON metadata (name, description, image, attributes)
            //   lastTime       — timestamp of the last balance change (mint, trade, burn)
            //
            // PERFORMANCE NOTE: This returns all tokens across all contracts. For large
            // wallets or production use, filter by your contract address directly via
            // the TzKT API using &token.contract={yourContractAddress}.
            List<TokenBalance> tokenBalances = new List<TokenBalance>();
            await ShowDialogAsync("Player Inventory", async () =>
            {
                tokenBalances = await _tezos.GetAllTokensForOwner(activeWalletAddress);
            });

            // BUILD INVENTORY DISPLAY
            //
            // In a real game, replace this section with your inventory UI logic:
            //   - Instantiate item card prefabs from a pool
            //   - Load item icons via tokenBalance.tokenMetadata (name/image fields)
            //   - Filter to only your game's contract: if (tb.tokenContract == myContract)
            //   - Map token IDs to ScriptableObjects for item stats and display names
            string result = $"Wallet <b>{activeWalletAddress}</b> holds <b>{tokenBalances.Count}</b> token(s).\n\n";

            if (tokenBalances.Count == 0)
            {
                result += "No tokens found. Mint or acquire some NFTs on Tezos to see them here.";
            }
            else
            {
                foreach (TokenBalance tokenBalance in tokenBalances)
                {
                    // Log a full inventory line per token for console inspection.
                    // tokenBalance.tokenMetadata contains the rich on-chain metadata JSON —
                    // parse it to populate item names, descriptions, and thumbnail images.
                    string inventoryLine =
                        $"Contract: {tokenBalance.tokenContract} | " +
                        $"Token ID: {tokenBalance.tokenId} | " +
                        $"Balance: {tokenBalance.balance} | " +
                        $"Last Updated: {tokenBalance.lastTime}";

                    Debug.Log($"[Example03] Inventory item — {inventoryLine}");

                    // Compact display line for the in-scene UI text panel
                    result += $" • tokenId = <b>{tokenBalance.tokenId}</b>, " +
                              $"balance = <b>{tokenBalance.balance}</b>, " +
                              $"contract = {tokenBalance.tokenContract}\n";
                }
            }

            // Required: Render UI
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = result;
            base.RefreshUIAsync();
        }
    }
}
