using Cysharp.Threading.Tasks;
using TezosAPI;
using TezosSDKExamples.Controllers;
using TezosSDKExamples.Shared.Tezos;
using UnityEngine;

#pragma warning disable CS4014, CS1998, CS0219
namespace TezosSDKExamples.Scenes
{
    /// <summary>
    /// EXAMPLE: Demonstrates wallet-based authentication with the Tezos blockchain.
    ///
    /// WEB3 CONCEPT — Why wallets replace traditional login:
    ///   In a traditional game, login means submitting a username + password to a server
    ///   that validates your identity against a database it controls.
    ///
    ///   In a Web3 game, login means proving you own a blockchain wallet address.
    ///   There is no password and no central server involved. Instead, the player's
    ///   wallet app signs a challenge using their private key. Your game receives the
    ///   public wallet address (e.g. tz1abc...123) as proof of identity.
    ///
    ///   Benefits for game developers:
    ///   - No user accounts or password storage to manage
    ///   - Identity is portable: the same wallet works across any Tezos game
    ///   - Ownership of in-game assets (NFTs, tokens) is verifiable on-chain
    ///   - Players control their own identity — no vendor lock-in
    /// </summary>
    public class Example01_Authentication : Example01_Parent
    {
        //  Fields ----------------------------------------

        // ITezosAPI is the main entry point for all Tezos SDK functionality.
        // Storing it here avoids repeated calls to TezosSingleton.Instance throughout the class.
        private ITezosAPI _tezos;


        //  Methods ---------------------------------------
        protected override async void Start()
        {
            // Required: Render UI
            base.Start();
            View.AuthenticationQr.IsVisible = false;

            // TezosSingleton.Instance is a Unity MonoBehaviour singleton that initialises
            // the SDK, manages the Beacon session, and persists across scene loads.
            // Think of it as the blockchain equivalent of a NetworkManager.
            _tezos = TezosSingleton.Instance;

            // The SDK communicates wallet state changes through C# events on MessageReceiver.
            // Subscribe here so the game reacts whenever a wallet connects or disconnects,
            // regardless of what triggered the change (user action, session timeout, etc.).
            // IMPORTANT: Unsubscribe these in OnDestroy() to prevent memory leaks if you
            // reuse this pattern in a long-lived scene.
            _tezos.MessageReceiver.AccountConnected += Tezos_OnAccountConnected;
            _tezos.MessageReceiver.AccountDisconnected += Tezos_AccountDisconnected;
        }


        //  Event Handlers --------------------------------
        protected override async UniTask OnAuthenticateButtonClicked()
        {
            // Required: Render UI
            base.OnAuthenticateButtonClicked();
            base.ShowDialogAsync(async () =>
            {
                // HasActiveWalletAddress() checks whether the SDK currently holds a
                // wallet address from a completed Beacon session. It returns false if:
                //   - The player has never connected a wallet
                //   - The previous session was disconnected or expired
                // Use this as your "is the player logged in?" check throughout your game.
                if (!_tezos.HasActiveWalletAddress())
                {
                    // BEGIN WALLET CONNECTION — Beacon protocol
                    //
                    // ConnectWallet() initiates a Beacon pairing request. Beacon is an
                    // open standard (walletbeacon.io) for connecting dApps to Tezos wallets.
                    //
                    // What happens on each platform:
                    //   Desktop: A QR code is generated and displayed (see ShowQrCode() below).
                    //            The player scans it with their mobile wallet app (e.g. Temple,
                    //            Kukai). Scanning establishes an encrypted Beacon channel.
                    //   Mobile:  A deep link is triggered instead of a QR code, opening the
                    //            wallet app directly on the same device.
                    //
                    // The private key never leaves the player's device. Your game only ever
                    // receives the public wallet address — it cannot sign anything on the
                    // player's behalf without their explicit approval in the wallet app.
                    View.AuthenticationQr.IsVisible = true;
                    View.AuthenticationQr.ShowQrCode();
                    _tezos.ConnectWallet();
                }
                else
                {
                    // DISCONNECT WALLET
                    //
                    // DisconnectWallet() closes the active Beacon session and clears the
                    // stored wallet address. After this call, HasActiveWalletAddress()
                    // returns false and the AccountDisconnected event fires.
                    //
                    // In your game, treat disconnection the same as a traditional logout:
                    // revoke access to gated content, clear player-specific UI, etc.
                    View.AuthenticationQr.IsVisible = true;
                    _tezos.DisconnectWallet();
                }
            });
        }


        // Fired by the SDK when a Beacon session is successfully established.
        // 'address' is the player's public Tezos wallet address (tz1..., tz2..., or tz3...).
        //
        // This is the Web3 equivalent of a successful login callback. Use it to:
        //   - Store the address as the player's unique identifier
        //   - Load player-specific data (NFT inventory, on-chain progress, etc.)
        //   - Unlock authenticated areas of your game UI
        private async void Tezos_OnAccountConnected(string address)
        {
            // Required: Render UI
            await RefreshUIAsync();
            View.AuthenticationQr.IsVisible = false;

            // GetActiveWalletAddress() returns the tz1... address of the connected wallet.
            // This address is the player's permanent, globally unique identity on Tezos.
            // Store it wherever you currently store a user ID — it serves the same purpose.
            string activeWalletAddress = _tezos.GetActiveWalletAddress();
            Debug.Log($"You are connected to a wallet with address <b>{activeWalletAddress}</b>.");
        }


        // Fired by the SDK when the Beacon session ends — either because the player
        // disconnected manually, or because the session expired.
        //
        // Treat this as a logout event: clear any state that was gated behind authentication.
        private async void Tezos_AccountDisconnected(string address)
        {
            // Required: Render UI
            await RefreshUIAsync();
            View.AuthenticationQr.IsVisible = false;

            // Optional: Add any custom code here
            Debug.Log($"You are not connected to a wallet.");
        }
    }
}
