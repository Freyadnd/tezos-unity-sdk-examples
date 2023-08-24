using Cysharp.Threading.Tasks;
using TezosAPI;
using TezosSDKExamples.Controllers;
using TezosSDKExamples.Shared.Tezos;
using UnityEngine;

#pragma warning disable CS4014, CS1998, CS0219
namespace TezosSDKExamples.Scenes
{
    /// <summary>
    /// EXAMPLE: Demonstrates transferring an FA2 token between two wallets
    /// using the Tezos SDK's CallContract() method.
    ///
    /// WEB3 CONCEPT — What is an FA2 transfer?
    ///   FA2 (TZIP-12) is the Tezos standard for fungible and non-fungible tokens.
    ///   Every NFT or in-game item minted on Tezos lives in an FA2 contract.
    ///   The contract exposes a "transfer" entrypoint that moves token ownership
    ///   from one wallet to another by updating the on-chain ledger.
    ///
    ///   Unlike a database UPDATE, an on-chain transfer:
    ///   - Requires the sender to sign the operation in their wallet
    ///   - Is permanent and publicly auditable once confirmed
    ///   - Cannot be reversed by your game — only the new owner can transfer again
    ///   - Costs a small network fee (gas) paid in tez by the sender
    ///
    /// WEB3 CONCEPT — How does CallContract() work?
    ///   The Tezos SDK does not expose a dedicated "transfer NFT" method.
    ///   Instead, it provides the general-purpose CallContract() which can invoke
    ///   any entrypoint on any smart contract. For FA2 transfers, you call the
    ///   "transfer" entrypoint with a Micheline-encoded parameter describing
    ///   the sender, recipient, token ID, and amount.
    ///
    ///   This is intentional — the same pattern works for any smart contract
    ///   interaction: minting, burning, voting, marketplace listings, etc.
    ///
    /// WEB3 CONCEPT — What is Micheline?
    ///   Micheline is the JSON-based serialisation format for Tezos smart contract
    ///   parameters. Rather than passing a plain C# object, you construct a string
    ///   that the Tezos node can interpret as typed contract input.
    ///   See BuildFA2TransferParameters() below for a detailed breakdown.
    ///
    /// IN-GAME TRADING AND MARKETPLACE PATTERNS:
    ///   This transfer primitive is the building block for all on-chain item exchange:
    ///
    ///   1. Direct gift / reward
    ///      Your game's backend wallet calls transfer to send an NFT directly to
    ///      a player's address when they complete a quest or purchase an item.
    ///      The player's inventory updates automatically on their next wallet sync.
    ///
    ///   2. Player-to-player trading
    ///      Player A calls transfer to send their item to Player B's address.
    ///      Both players can verify the trade on TzKT before and after it occurs.
    ///      No escrow server required — the blockchain is the authority.
    ///
    ///   3. Marketplace listing (operator pattern)
    ///      Player A calls the FA2 update_operators entrypoint to authorise a
    ///      marketplace contract to transfer on their behalf. The marketplace
    ///      contract then calls transfer atomically when a buyer pays.
    ///      This allows trustless, non-custodial item sales.
    ///
    ///   4. Burn / consume
    ///      Transfer to the zero address (tz1burnburnburnburnburnburnburjAYjjX)
    ///      permanently removes a token from circulation. Use this for consumable
    ///      items (potions, keys) that should disappear when used.
    ///
    ///   5. Cross-game item loan
    ///      Transfer an item to another player's address for a fixed duration,
    ///      then have them transfer it back. Since all transfers are on-chain,
    ///      both games can independently verify current ownership at any time.
    /// </summary>
    public class Example04_FA2Transfer : Example04_Parent
    {
        //  Fields ----------------------------------------

        // ITezosAPI is the main entry point for all Tezos SDK functionality.
        // See Example01_Authentication.cs for a full explanation of this singleton.
        private ITezosAPI _tezos;

        // Tracks whether a transfer operation is currently awaiting wallet confirmation
        // or on-chain inclusion. Prevents the player from submitting duplicate transfers.
        private bool _isTransferPending = false;


        //  Methods ---------------------------------------
        protected override async void Start()
        {
            // Required: Render UI
            base.Start();

            _tezos = TezosSingleton.Instance;

            // Subscribe to wallet connection events (show/hide transfer UI).
            // Subscribe to contract call events to receive the transfer outcome.
            // IMPORTANT: All subscriptions are unsubscribed in OnDestroy() below.
            _tezos.MessageReceiver.AccountConnected    += Tezos_OnAccountConnected;
            _tezos.MessageReceiver.AccountDisconnected += Tezos_OnAccountDisconnected;
            _tezos.MessageReceiver.ContractCallInjected   += Tezos_OnContractCallInjected;
            _tezos.MessageReceiver.ContractCallCompleted  += Tezos_OnContractCallCompleted;
            _tezos.MessageReceiver.ContractCallFailed     += Tezos_OnContractCallFailed;
        }

        protected void OnDestroy()
        {
            if (_tezos != null)
            {
                _tezos.MessageReceiver.AccountConnected    -= Tezos_OnAccountConnected;
                _tezos.MessageReceiver.AccountDisconnected -= Tezos_OnAccountDisconnected;
                _tezos.MessageReceiver.ContractCallInjected   -= Tezos_OnContractCallInjected;
                _tezos.MessageReceiver.ContractCallCompleted  -= Tezos_OnContractCallCompleted;
                _tezos.MessageReceiver.ContractCallFailed     -= Tezos_OnContractCallFailed;
            }
        }


        //  Event Handlers — wallet state --------------------------------

        private async void Tezos_OnAccountConnected(string address)
        {
            await RefreshUIAsync();
            Debug.Log($"[Example04] Wallet connected: <b>{address}</b>. Ready to transfer tokens.");
        }

        private async void Tezos_OnAccountDisconnected(string address)
        {
            _isTransferPending = false;
            await RefreshUIAsync();
            Debug.Log($"[Example04] Wallet disconnected.");
        }


        //  Event Handlers — transaction lifecycle --------------------------------

        // Fired when the signed operation enters the Tezos mempool.
        // At this point the transfer is submitted but not yet confirmed.
        // In your game: show a "Transfer pending..." spinner or optimistic UI update.
        //
        // 'operationHash' is the operation hash (op...) — use it to link to TzKT:
        //   https://tzkt.io/{operationHash}
        private async void Tezos_OnContractCallInjected(string operationHash)
        {
            string message = $"Transfer injected.\nOperation: <b>{operationHash}</b>\n\n" +
                             $"Waiting for on-chain confirmation...";
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = message;

            Debug.Log($"[Example04] Transfer injected. Operation hash: {operationHash}");
            Debug.Log($"[Example04] Track on TzKT: https://tzkt.io/{operationHash}");
        }

        // Fired when the operation is included in a confirmed block on-chain.
        // Ownership has now changed — the recipient's wallet holds the token.
        // In your game: hide the spinner, show success UI, refresh the sender's inventory.
        //
        // IMPORTANT: Always refresh inventory AFTER ContractCallCompleted, not after
        // ContractCallInjected. Injection means the transfer is pending; completion means
        // the on-chain ledger has been updated. Reading inventory before completion may
        // return stale data.
        private async void Tezos_OnContractCallCompleted(string operationHash)
        {
            _isTransferPending = false;

            string message = $"Transfer confirmed on-chain.\nOperation: <b>{operationHash}</b>\n\n" +
                             $"The recipient's wallet now holds the token.\n" +
                             $"Track: https://tzkt.io/{operationHash}";
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = message;

            await RefreshUIAsync();
            Debug.Log($"[Example04] Transfer confirmed. Operation hash: {operationHash}");
        }

        // Fired when the operation is rejected by the node or fails on-chain.
        // Common causes: the sender no longer owns the token, insufficient gas,
        // the FA2 contract rejected the transfer (e.g. paused or permission denied).
        // In your game: show an error message and re-enable the transfer button.
        private async void Tezos_OnContractCallFailed(string errorMessage)
        {
            _isTransferPending = false;

            string message = $"Transfer failed.\n\n<b>Reason:</b> {errorMessage}\n\n" +
                             $"Common causes:\n" +
                             $" • Sender does not own the token\n" +
                             $" • Insufficient tez balance for gas fees\n" +
                             $" • FA2 contract rejected the operation";
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = message;

            await RefreshUIAsync();
            Debug.LogError($"[Example04] Transfer failed: {errorMessage}");
        }


        //  Transfer initiation --------------------------------

        protected override async UniTask OnTransferButtonClicked()
        {
            // Required: Render UI
            base.OnTransferButtonClicked();

            // Guard against duplicate submissions while a transfer is in-flight.
            // The player must wait for ContractCallCompleted or ContractCallFailed
            // before initiating another transfer.
            if (_isTransferPending)
            {
                Debug.LogWarning("[Example04] Transfer already pending. Wait for confirmation.");
                return;
            }

            await ShowDialogAsync("FA2 Transfer", async () =>
            {
                await InitiateTransferAsync();
            });
        }


        // FA2 TRANSFER — STEP BY STEP
        //
        // A token transfer on Tezos involves two phases:
        //
        //   Phase 1 — Submission (this method):
        //     CallContract() sends the operation to the Beacon wallet for signing.
        //     The player approves the transfer in their wallet app.
        //     The SDK then submits the signed operation to the Tezos node.
        //     ContractCallInjected fires when the operation enters the mempool.
        //
        //   Phase 2 — Confirmation (Tezos_OnContractCallCompleted above):
        //     The Tezos network includes the operation in a block (~30 seconds).
        //     The FA2 contract's on-chain ledger is updated.
        //     ContractCallCompleted fires — ownership has changed.
        //
        // CallContract() returns immediately after sending to Beacon. It does NOT
        // await the on-chain result. Use the MessageReceiver events to handle the outcome.
        private async UniTask InitiateTransferAsync()
        {
            // DEMO TRANSFER PARAMETERS
            //
            // In a real game these would come from player input, a trade UI, or
            // game logic (e.g. quest reward, marketplace purchase).
            //
            // fromAddress: the sender — must be the currently connected wallet.
            //   FA2 contracts reject transfers where from_ != the operation signer,
            //   unless the signer has been granted operator permission by the owner.
            string fromAddress = _tezos.GetActiveWalletAddress();

            // toAddress: the recipient's Tezos wallet address (tz1..., tz2..., or tz3...).
            //   In production: read this from a trade request, friend list, or marketplace order.
            //   The recipient does not need to be online or connected — their address is enough.
            string toAddress = "tz1TiZ74DtsT74VyWfbAuSis5KcncH1WvNB9";

            // fa2Contract: the KT1... address of the FA2 contract holding the token.
            //   Every NFT collection has its own contract. Get this from your minting
            //   transaction, the TzKT explorer, or your game's item registry.
            string fa2Contract = "KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY";

            // tokenId: the specific token to transfer within the FA2 contract.
            //   For NFTs this is typically the unique token ID. For fungible FA2 tokens
            //   multiple units of the same ID may exist.
            int tokenId = 1;

            // amount: how many units to transfer. For a unique NFT this is always 1.
            //   For fungible or semi-fungible FA2 tokens, this can be more than 1.
            int amount = 1;

            // Build the Micheline-encoded parameter string for the FA2 transfer entrypoint.
            // See BuildFA2TransferParameters() below for a full breakdown of the format.
            string transferParameters = BuildFA2TransferParameters(
                fromAddress, toAddress, tokenId, amount);

            Debug.Log($"[Example04] Initiating FA2 transfer...");
            Debug.Log($"[Example04]   From:     {fromAddress}");
            Debug.Log($"[Example04]   To:       {toAddress}");
            Debug.Log($"[Example04]   Contract: {fa2Contract}");
            Debug.Log($"[Example04]   Token ID: {tokenId}  Amount: {amount}");
            Debug.Log($"[Example04]   Parameters: {transferParameters}");

            _isTransferPending = true;

            // FA2 TRANSFER — THE ACTUAL SDK CALL
            //
            // CallContract() invokes any entrypoint on any Tezos smart contract.
            // For FA2 transfers, the entrypoint is always "transfer" (TZIP-12 standard).
            //
            // Parameters:
            //   contractAddress — the FA2 contract that holds the token (KT1...)
            //   entryPoint      — "transfer" (the FA2 standard entrypoint name)
            //   input           — Micheline JSON encoding of the transfer batch
            //   amount          — tez to send with the call; 0 for plain transfers
            //
            // After this call:
            //   1. The SDK forwards the operation to the player's Beacon wallet
            //   2. The player sees a confirmation prompt in their wallet app
            //   3. On approval, the wallet signs and submits the operation
            //   4. ContractCallInjected fires (mempool), then ContractCallCompleted (on-chain)
            //   5. On rejection or error, ContractCallFailed fires instead
            _tezos.CallContract(
                contractAddress: fa2Contract,
                entryPoint: "transfer",
                input: transferParameters,
                amount: 0);

            string pendingMessage = $"Transfer submitted to wallet for signing.\n\n" +
                                    $"From:     <b>{fromAddress}</b>\n" +
                                    $"To:       <b>{toAddress}</b>\n" +
                                    $"Contract: <b>{fa2Contract}</b>\n" +
                                    $"Token ID: <b>{tokenId}</b>  Amount: <b>{amount}</b>\n\n" +
                                    $"Waiting for wallet approval...";
            View.DetailsTextPanelUI.BodyTextAreaUI.Text.text = pendingMessage;
        }


        // FA2 TRANSFER PARAMETER ENCODING — Micheline JSON
        //
        // The FA2 "transfer" entrypoint has this Michelson type (TZIP-12 standard):
        //
        //   (list %transfer
        //     (pair
        //       (address %from_)
        //       (list %txs
        //         (pair
        //           (address %to_)
        //           (pair
        //             (nat %token_id)
        //             (nat %amount))))))
        //
        // In plain English: a list of transfer batches. Each batch names one sender
        // (from_) and a list of individual transfers (txs) from that sender.
        // Batching lets a single operation send from one address to many recipients,
        // which reduces gas costs compared to one operation per recipient.
        //
        // This method encodes a single transfer (one sender, one recipient) as a
        // Micheline JSON array containing one Pair node.
        //
        // The output format is:
        // [
        //   { "prim": "Pair", "args": [
        //       { "string": "<from_address>" },
        //       [
        //         { "prim": "Pair", "args": [
        //             { "string": "<to_address>" },
        //             { "prim": "Pair", "args": [
        //                 { "int": "<token_id>" },
        //                 { "int": "<amount>"   }
        //             ]}
        //         ]}
        //       ]
        //   ]}
        // ]
        //
        // HOW TO EXTEND THIS FOR MULTI-RECIPIENT TRANSFERS:
        //   To send to multiple recipients in one operation, add more Pair objects to
        //   the inner txs array. This is more gas-efficient than multiple CallContract calls:
        //
        //   [{"prim":"Pair","args":[{"string":"<from>"},[
        //     {"prim":"Pair","args":[{"string":"<to1>"},{"prim":"Pair","args":[{"int":"1"},{"int":"1"}]}]},
        //     {"prim":"Pair","args":[{"string":"<to2>"},{"prim":"Pair","args":[{"int":"2"},{"int":"1"}]}]}
        //   ]]}]
        private static string BuildFA2TransferParameters(
            string fromAddress,
            string toAddress,
            int tokenId,
            int amount)
        {
            // Each level of nesting corresponds to one level of the Michelson pair type.
            // - Outer list:  the transfer batch list (one element = one sender)
            // - from_ Pair:  the sender address and their list of txs
            // - txs list:    one element per recipient
            // - tx Pair:     recipient address + (token_id, amount) nested pair
            return
                "[{\"prim\":\"Pair\",\"args\":[" +
                    $"{{\"string\":\"{fromAddress}\"}}," +
                    "[{\"prim\":\"Pair\",\"args\":[" +
                        $"{{\"string\":\"{toAddress}\"}}," +
                        "{\"prim\":\"Pair\",\"args\":[" +
                            $"{{\"int\":\"{tokenId}\"}}," +
                            $"{{\"int\":\"{amount}\"}}" +
                        "]}" +
                    "]}]" +
                "]}]";
        }
    }
}
