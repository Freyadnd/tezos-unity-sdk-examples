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
    /// EXAMPLE: Demonstrates NFT token gating — using on-chain token ownership to
    /// control access to game features.
    ///
    /// WEB3 CONCEPT — What is token gating?
    ///   Token gating is the practice of restricting access to content, features, or
    ///   areas of your game based on whether the player owns a specific on-chain token.
    ///   It is the Web3 equivalent of a season pass, DLC entitlement, or access key —
    ///   except ownership is recorded on a public blockchain rather than a private
    ///   server database.
    ///
    ///   Because ownership lives on-chain, it has properties traditional DLC cannot match:
    ///   - Ownership is verifiable by anyone without trusting your server
    ///   - Players can trade, sell, or gift their tokens on secondary markets
    ///   - The same token can grant access across multiple games or platforms
    ///   - Your game does not need to store or manage entitlement records
    ///
    /// WEB3 CONCEPT — What is an NFT?
    ///   An NFT (Non-Fungible Token) is a unique entry in a smart contract's ledger.
    ///   On Tezos, NFTs follow the FA2 standard (TZIP-12). Each token is identified by:
    ///   - A contract address (KT1...) — the smart contract that defines the collection
    ///   - A token ID (integer) — the specific token within that contract
    ///
    ///   Owning an NFT means the FA2 contract's on-chain ledger records your wallet
    ///   address against that token ID with a balance greater than zero.
    ///
    /// COMMON TOKEN GATING PATTERNS IN GAMES:
    ///   1. Character / skin unlock    — own token X → character Y is playable
    ///   2. Level / area access        — own token X → zone Y is accessible
    ///   3. Early access / beta        — own a founder token → play before launch
    ///   4. Cross-game items           — own a weapon NFT minted in Game A, use it in Game B
    ///   5. Loyalty rewards            — own N tokens from a collection → bonus multipliers
    ///   6. DAO voting rights          — own a governance token → vote on game updates
    /// </summary>
    public class Example02_NFTTokenGating : Example02_Parent
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

            // Subscribe to wallet connection events so the UI stays in sync with the
            // player's authentication state. Token gating checks require a connected
            // wallet — these events tell us when the player is ready (or has left).
            // IMPORTANT: Unsubscribe these in OnDestroy() to prevent memory leaks.
            _tezos.MessageReceiver.AccountConnected += Tezos_OnAccountConnected;
            _tezos.MessageReceiver.AccountDisconnected += Tezos_AccountDisconnected;
        }


        //  Event Handlers --------------------------------

        // Fires when the player successfully connects their wallet (Beacon handshake complete).
        // This is the earliest point at which it is safe to run token gating checks,
        // because GetActiveWalletAddress() will now return a valid tz1... address.
        private async void Tezos_OnAccountConnected(string address)
        {
            // Required: Render UI
            await RefreshUIAsync();

            string activeWalletAddress = _tezos.GetActiveWalletAddress();
            Debug.Log($"You are connected to a wallet with address '{activeWalletAddress}'.");
        }


        // Fires when the player disconnects their wallet or the Beacon session expires.
        // Revoke any access that was granted by a token gating check — the player's
        // identity can no longer be verified until they reconnect.
        private async void Tezos_AccountDisconnected(string address)
        {
            // Required: Render UI
            await RefreshUIAsync();

            // Optional: Add any custom code here
            Debug.Log($"You are not connected to a wallet.");
        }


        // TOKEN GATING CHECK — Connected wallet (the logged-in player)
        //
        // Checks whether the currently authenticated player owns a specific NFT.
        // This is the core token gating pattern: query ownership, then branch on the result.
        //
        // Use this variant when you want to gate features for the active player —
        // e.g. "does THIS player own the sword NFT that unlocks the bonus level?"
        protected override async UniTask OnCheckForNft01ButtonClicked()
        {
            // Required: Render UI
            base.OnCheckForNft01ButtonClicked();

            // The active wallet address identifies the logged-in player on-chain.
            // This is the address whose token balance will be queried.
            string activeWalletAddress = _tezos.GetActiveWalletAddress();

            // NFT OWNERSHIP VERIFICATION — what these two values mean:
            //
            // demoNFTAddress (KT1...): The contract address of the NFT collection.
            //   Every FA2 collection on Tezos is deployed as its own smart contract.
            //   You get this address when you deploy or purchase the collection.
            //   It is public and can be looked up on https://tzkt.io
            //
            // demoTokenId (int): The specific token within that contract.
            //   FA2 contracts can hold many distinct tokens. Token ID 0 might be a
            //   common item, ID 1 a rare skin, ID 2 a legendary weapon, etc.
            //   The combination of contract address + token ID uniquely identifies
            //   one NFT type across the entire Tezos blockchain.
            string demoNFTAddress = "KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY";
            int demoTokenId = 1;

            // IsOwnerOfToken() queries the TzKT indexer API to check the FA2 contract's
            // on-chain ledger. It returns true if the wallet holds a balance > 0 for
            // the given contract + token ID combination.
            //
            // Under the hood: GET https://api.tzkt.io/v1/tokens/balances
            //   ?account={address}&token.contract={contract}&token.tokenId={id}&balance.ne=0
            //
            // This call is async — it makes a network request to TzKT. Always await it.
            bool hasTheNft = false;
            await ShowDialogAsync("Check For NFT", async () =>
            {
                hasTheNft = await _tezos.IsOwnerOfToken(
                    activeWalletAddress,
                    demoNFTAddress,
                    demoTokenId);
            });

            // TOKEN GATING BRANCH — act on the result
            //
            // In your game, replace this display logic with whatever the token gates:
            //   if (hasTheNft) { LoadBonusLevel(); }
            //   if (hasTheNft) { UnlockCharacterSkin("dragon"); }
            //   if (hasTheNft) { GrantEarlyAccessLobby(); }
            string result = "";
            if (hasTheNft)
            {
                result = $"The wallet address <b>{activeWalletAddress}</b> has " +
                         $"the NFT with address <b>{demoNFTAddress}</b> and tokenId <b>{demoTokenId}</b>.\n\n";
            }
            else
            {
                result = $"The wallet address <b>{activeWalletAddress}</b> does NOT have " +
                         $"the NFT with address <b>{demoNFTAddress}</b> and tokenId <b>{demoTokenId}</b>.\n\n";
            }

            // Required: Render UI
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = result;
            base.RefreshUIAsync();
        }


        // TOKEN GATING CHECK — Arbitrary wallet address (any player, NPC, leaderboard entry)
        //
        // Identical logic to OnCheckForNft01ButtonClicked(), but queries a hardcoded
        // demo address rather than the connected player's wallet.
        //
        // DEVELOPER NOTE — When to use an arbitrary address check:
        //   - Leaderboards: display NFT badges next to any player's score
        //   - Social features: show what items a friend's wallet holds
        //   - NPC / world state: check if a specific address "owns" a location NFT
        //   - Admin tools: verify ownership of any address during development
        //   - Anti-cheat: cross-check a reported address against expected holdings
        protected override async UniTask OnCheckForNft02ButtonClicked()
        {
            // Required: Render UI
            base.OnCheckForNft02ButtonClicked();

            // A demo wallet address unrelated to the connected player.
            // In production, this would come from a leaderboard entry, friend list,
            // player profile lookup, or any other source of wallet addresses.
            string demoWalletAddress = "tz1TiZ74DtsT74VyWfbAuSis5KcncH1WvNB9";
            string demoNFTAddress = "KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY";
            int demoTokenId = 1;

            bool hasTheNft = false;
            await ShowDialogAsync("Check For NFT", async () =>
            {
                hasTheNft = await _tezos.IsOwnerOfToken(
                    demoWalletAddress,
                    demoNFTAddress,
                    demoTokenId);
            });

            string result = "";
            if (hasTheNft)
            {
                result = $"The wallet address <b>{demoWalletAddress}</b> has " +
                         $"the NFT with address <b>{demoNFTAddress}</b> and tokenId <b>{demoTokenId}</b>.\n\n";
            }
            else
            {
                result = $"The wallet address <b>{demoWalletAddress}</b> does NOT have " +
                         $"the NFT with address <b>{demoNFTAddress}</b> and tokenId <b>{demoTokenId}</b>.\n\n";
            }

            // Required: Render UI
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = result;
            base.RefreshUIAsync();
        }


        // NFT INVENTORY FETCH — List all tokens owned by a wallet
        //
        // Rather than checking for one specific token, this retrieves the player's
        // complete on-chain token inventory across all FA2 contracts.
        //
        // DEVELOPER NOTE — Common uses for a full inventory:
        //   - Inventory screen: show every NFT the player owns, with metadata
        //   - Dynamic unlocks: iterate the list and unlock all matching features at once
        //     (e.g. any token from collection KT1... grants a cosmetic effect)
        //   - Rarity display: show token IDs and balances as part of a profile card
        //   - Crafting systems: check if the player holds all required component tokens
        //
        // DEVELOPER NOTE — TokenBalance fields available per entry:
        //   tokenContract  — KT1... address of the FA2 contract
        //   tokenId        — ID of the token within that contract
        //   balance        — quantity held (always > 0; fungible tokens may be > 1)
        //   tokenMetadata  — JSON metadata from the contract (name, image, attributes)
        //   lastTime       — timestamp of the last balance change (trade, mint, burn)
        protected override async UniTask OnListAllNftsButtonClicked()
        {
            // Required: Render UI
            base.OnListAllNftsButtonClicked();

            // A demo wallet address. In production, use _tezos.GetActiveWalletAddress()
            // to fetch the inventory of the logged-in player, or pass any tz1... address
            // to inspect another account's holdings (all balances are public on-chain).
            string demoWalletAddress = "tz2U7C8cf4W5Qw6onYjF8QLhnh5hMRbrrDon";

            // GetAllTokensForOwner() queries TzKT for every token balance > 0 held by
            // the address, across all FA2 contracts. Each TokenBalance in the returned
            // list represents one distinct token type the wallet currently holds.
            //
            // PERFORMANCE NOTE: For wallets with large inventories this list can be long.
            // Consider filtering by contract address in production using the TzKT API
            // directly if you only care about tokens from your own collection.
            List<TokenBalance> tokenBalances = new List<TokenBalance>();
            await ShowDialogAsync("List All NFTs", async () =>
            {
                tokenBalances = await _tezos.GetAllTokensForOwner(demoWalletAddress);
            });

            string result = $"The wallet address <b>{demoWalletAddress}</b> has {tokenBalances.Count} NFTs.\n\n";

            foreach (TokenBalance tokenBalance in tokenBalances)
            {
                // Each TokenBalance exposes the contract, token ID, and quantity.
                // tokenBalance.tokenMetadata contains the full JSON metadata object
                // (name, description, image URL, custom attributes) — use it to drive
                // in-game item display, stats, or visual effects.
                result += $" • tokenId = {tokenBalance.tokenId}, balance = {tokenBalance.balance}\n";
            }

            // Required: Render UI
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = result;
            base.RefreshUIAsync();
        }
    }
}
