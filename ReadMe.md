# Tezos SDK For Unity - Examples

A Unity project demonstrating how to integrate Tezos blockchain features into games using the [Tezos SDK For Unity](https://github.com/trilitech/tezos-unity-sdk). Each example is an isolated scene focused on a single integration pattern.

---

## Project Overview

This repo is a practical reference for Unity developers adding Tezos blockchain functionality to a game. It covers the two most common integration patterns: authenticating a player via their Tezos wallet, and gating game features behind NFT ownership.

It is not a full game — it is a focused collection of runnable examples with annotated source code you can copy into your own project.

---

## Features Demonstrated

| Feature | Scene | API Used |
|---|---|---|
| Wallet connect / disconnect | `Example01_Authentication` | `ITezosAPI.ConnectWallet()` |
| Check authentication state | `Example01_Authentication` | `ITezosAPI.GetActiveWalletAddress()` |
| NFT ownership check (token gating) | `Example02_NFTTokenGating` | `ITezosAPI.IsOwnerOfToken()` |
| List all NFTs for an account | `Example02_NFTTokenGating` | `ITezosAPI.GetAllTokensForOwner()` |

---

## Example Scenes

### Example 01 — Authentication

**Scene:** `Example01_Authentication`
**Script:** [`Example01_Authentication.cs`](./Unity/Assets/Tezos/TezosSDKExamples/Scripts/Runtime/Tezos/TezosSDKExamples/Scenes/Example01_Authentication.cs)

Demonstrates connecting a player to the Tezos network via a Tezos-compatible mobile wallet (Beacon protocol). On desktop, a QR code is displayed; on mobile, a deep link opens the wallet app directly.

```csharp
ITezosAPI tezos = TezosSingleton.Instance;

// Subscribe to wallet events
tezos.MessageReceiver.AccountConnected += OnAccountConnected;
tezos.MessageReceiver.AccountDisconnected += OnAccountDisconnected;

// Initiate wallet connection (shows QR code on desktop, deep link on mobile)
if (!tezos.HasActiveWalletAddress())
{
    tezos.ConnectWallet();
}
```

---

### Example 02 — NFT Token Gating

**Scene:** `Example02_NFTTokenGating`
**Script:** [`Example02_NFTTokenGating.cs`](./Unity/Assets/Tezos/TezosSDKExamples/Scripts/Runtime/Tezos/TezosSDKExamples/Scenes/Example02_NFTTokenGating.cs)

Demonstrates querying the Tezos blockchain to check whether a player owns a specific NFT, and listing all NFTs held by an account. Use this pattern to unlock game features, characters, or content based on wallet holdings.

```csharp
ITezosAPI tezos = TezosSingleton.Instance;

string walletAddress = tezos.GetActiveWalletAddress();
string nftContract   = "KT1BRADdqGk2eLmMqvyWzqVmPQ1RCBCbW5dY";
int    tokenId       = 1;

// Returns true if the wallet owns the token
bool hasNft = await tezos.IsOwnerOfToken(walletAddress, nftContract, tokenId);

if (hasNft)
{
    // Unlock game feature, character, level, etc.
}

// List every token held by an account
List<TokenBalance> tokens = await tezos.GetAllTokensForOwner(walletAddress);
```

---

## Architecture Overview

The project follows a three-layer structure. Each example scene has its own view, controller, and scene script — keeping Tezos calls isolated from UI logic.

```
Scripts/Runtime/
├── Scenes/
│   ├── Example01_Authentication.cs     ← Tezos calls live here
│   └── Example02_NFTTokenGating.cs     ← Tezos calls live here
├── Shared/
│   ├── Controllers/                    ← Base controller classes (UI refresh, event wiring)
│   ├── View/                           ← UI component wrappers
│   └── Tezos/
│       ├── TezosExtensions.cs          ← Extension methods over ITezosAPI
│       └── AuthenticationQr.cs         ← QR code / deep link UI component
```

**Key design decisions:**
- `TezosSingleton.Instance` provides a single entry point to the SDK — treat it like a service locator.
- All blockchain calls are `async/await` using [UniTask](https://github.com/Cysharp/UniTask). Never call them on the main thread without `await`.
- Wallet events (`AccountConnected`, `AccountDisconnected`) are fired by `ITezosAPI.MessageReceiver` — subscribe in `Start()`, unsubscribe in `OnDestroy()`.
- NFT ownership queries go through [`TezosExtensions.cs`](./Unity/Assets/Tezos/TezosSDKExamples/Scripts/Runtime/Tezos/TezosSDKExamples/Shared/Tezos/TezosExtensions.cs), which wraps the [TzKT public API](https://api.tzkt.io) (`https://api.tzkt.io/v1/tokens/balances`).

---

## How Tezos Integrates Into Unity Games

```
Unity Game
    │
    ├── TezosSingleton.Instance  (ITezosAPI)
    │       │
    │       ├── ConnectWallet()           → Beacon protocol → player's mobile wallet
    │       ├── GetActiveWalletAddress()  → returns tz1... address
    │       └── MessageReceiver           → C# events for wallet state changes
    │
    └── TezosExtensions (this project)
            │
            ├── IsOwnerOfToken()          → HTTP GET → TzKT API → true/false
            └── GetAllTokensForOwner()    → HTTP GET → TzKT API → List<TokenBalance>
```

The SDK handles the Beacon handshake and wallet session. Your game code only sees `ITezosAPI` — a clean interface with no blockchain boilerplate. NFT queries bypass the SDK and hit the TzKT indexer directly via `UnityWebRequest`, since TzKT provides richer query options than the SDK's built-in methods.

---

## Tezos SDK Architecture in a Unity Game

This section explains the full stack behind a Tezos-integrated Unity game — from the C# code in your scene down to the smart contracts on-chain.

### System Diagram

```
┌─────────────────────────────────────────────────────┐
│                    Unity Game                        │
│                                                      │
│  Your C# scripts call ITezosAPI via                  │
│  TezosSingleton.Instance                             │
└──────────────────────┬──────────────────────────────┘
                       │ SDK calls (ConnectWallet,
                       │ GetActiveWalletAddress, etc.)
                       ▼
┌─────────────────────────────────────────────────────┐
│               Tezos Unity SDK                        │
│                                                      │
│  Manages wallet sessions, signs requests,            │
│  surfaces C# events via ITezosAPI.MessageReceiver    │
└──────────────────────┬──────────────────────────────┘
                       │ Beacon protocol
                       │ (QR code on desktop,
                       │  deep link on mobile)
                       ▼
┌─────────────────────────────────────────────────────┐
│              Beacon Wallet                           │
│                                                      │
│  Player's mobile wallet app (e.g. Temple, Kukai).   │
│  Holds the private key. Signs operations and        │
│  permission requests — keys never leave the device. │
└──────────────┬──────────────────────┬───────────────┘
               │ Signed operations    │ Read queries
               ▼                      ▼
┌──────────────────────┐  ┌──────────────────────────┐
│   Tezos RPC Node     │  │    TzKT Indexer API       │
│                      │  │                           │
│  Broadcasts signed   │  │  Fast, queryable index    │
│  transactions to     │  │  of on-chain state.       │
│  the network.        │  │  Used for token balance   │
│                      │  │  and ownership lookups.   │
└──────────┬───────────┘  └───────────────────────────┘
           │ Confirmed transactions
           ▼
┌─────────────────────────────────────────────────────┐
│               Smart Contracts                        │
│                                                      │
│  FA2 token contracts store NFT ownership records.   │
│  Custom game contracts can store on-chain state     │
│  (scores, inventories, achievements).               │
└─────────────────────────────────────────────────────┘
```

---

### How Wallet Authentication Works

Authentication uses the [Beacon](https://docs.walletbeacon.io) open standard for connecting dApps to Tezos wallets. No password or API key is involved — the wallet signs a challenge to prove ownership of an address.

**Flow:**

1. Your game calls `tezos.ConnectWallet()`
2. The SDK generates a Beacon pairing request
3. **Desktop:** A QR code is shown — the player scans it with their wallet app
   **Mobile:** A deep link opens the wallet app directly
4. The player approves the connection in their wallet
5. The SDK fires `MessageReceiver.AccountConnected` with the player's `tz1...` address
6. Your game receives the address and updates its state

```
Game                SDK              Wallet App         Tezos Network
 │                   │                   │                   │
 │ ConnectWallet()   │                   │                   │
 │──────────────────>│                   │                   │
 │                   │── Beacon pair ───>│                   │
 │                   │                  │── player approves  │
 │                   │<─ tz1... address ─│                   │
 │<─ AccountConnected│                   │                   │
 │   (tz1... address)│                   │                   │
```

The address is a public identifier — no private key ever touches your game code.

---

### How NFT Verification Works

NFT ownership is not stored in the SDK or your game — it lives in FA2 smart contracts on-chain. To verify ownership, the game queries the TzKT indexer, which provides a fast HTTP API over indexed blockchain state.

**Flow:**

1. Player connects their wallet (authentication, see above)
2. Game calls `tezos.IsOwnerOfToken(walletAddress, contractAddress, tokenId)`
3. `TezosExtensions.cs` sends a `GET` request to `https://api.tzkt.io/v1/tokens/balances`
4. TzKT returns matching balance records from the FA2 contract's ledger
5. A non-empty result means the player holds the token → feature unlocked

```
Game                  TezosExtensions       TzKT API            FA2 Contract
 │                          │                   │                     │
 │ IsOwnerOfToken(          │                   │                     │
 │   address,              │                   │          (indexed   │
 │   contract,             │                   │           from here)│
 │   tokenId)              │                   │<────────────────────│
 │────────────────────────>│                   │                     │
 │                         │── GET /tokens/    │                     │
 │                         │   balances?...   >│                     │
 │                         │<── [ {balance} ] ─│                     │
 │<── true / false ────────│                   │                     │
```

**Why TzKT instead of direct RPC?**
Tezos RPC nodes expose raw contract storage, which requires unpacking Michelson data. TzKT pre-indexes FA2 ledgers and exposes them as a simple REST API — no Michelson parsing needed.

---

## Getting Started

1. Clone or download this repo
2. Install [Unity Editor](https://store.unity.com/#plans-individual) (see [required version](./Unity/ProjectSettings/ProjectVersion.txt))
3. Open the `./Unity/` folder in Unity Hub
4. All dependencies resolve automatically via the [Unity Package Manager](https://docs.unity3d.com/Manual/upm-ui.html) — no manual setup required
5. Open a scene from `Assets/Tezos/TezosSDKExamples/Scenes/` and press Play

> Additional orientation: `Unity → Window → Tezos → Tezos SDK For Unity → Open ReadMe`

---

## Configuration

| Setting | Value |
|---|---|
| Unity Project | [`./Unity/`](./Unity/) |
| Unity Version | [See ProjectVersion.txt](./Unity/ProjectSettings/ProjectVersion.txt) |
| Target Platform | Standalone Mac/PC (mobile wallet connection supported) |
| Dependencies | Resolved automatically via [manifest.json](./Unity/Packages/manifest.json) |

---

## Documentation

- [Tezos SDK For Unity — OpenTezos](https://opentezos.com/gaming/unity-sdk)
- [Tezos SDK For Unity — GitHub](https://github.com/trilitech/tezos-unity-sdk)
- [TzKT API Reference](https://api.tzkt.io)

---

## Videos

<table>
<tr>
<th>Tezos SDK For Unity - Authentication</th>
<th>Tezos SDK For Unity - NFTs</th>
</tr>
<tr>
<td>
<a href="https://tbd/youtube/link"><img width="500" src="./Unity/Assets/Tezos/TezosSDKExamples/Documentation/Images/YT_Thumbnail_Video_03.png" /></a>
</td>
<td>
<a href="https://tbd/youtube/link"><img width="500" src="./Unity/Assets/Tezos/TezosSDKExamples/Documentation/Images/YT_Thumbnail_Video_04.png" /></a>
</td>
</tr>
</table>

---

## Screenshots

<table>
<tr>
<th>Example01_Authentication</th>
<th>Example02_NFTTokenGating</th>
</tr>
<tr>
<td>
<a href="./Unity/Assets/Tezos/TezosSDKExamples/Documentation/Images/Example01_Authentication.png"><img width="500" src="./Unity/Assets/Tezos/TezosSDKExamples/Documentation/Images/Example01_Authentication.png" /></a>
</td>
<td>
<a href="./Unity/Assets/Tezos/TezosSDKExamples/Documentation/Images/Example02_NFTTokenGating.png"><img width="500" src="./Unity/Assets/Tezos/TezosSDKExamples/Documentation/Images/Example02_NFTTokenGating.png" /></a>
</td>
</tr>
</table>
