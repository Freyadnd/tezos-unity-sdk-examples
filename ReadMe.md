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
