using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cysharp.Threading.Tasks;
using TezosAPI;
using UnityEngine;
using UnityEngine.Networking;

namespace TezosSDKExamples.Shared.Tezos
{
    /// <summary>
    /// Extension methods over ITezosAPI for common Web3 game queries.
    ///
    /// All public methods on this class are async and make HTTP requests to the
    /// TzKT indexer API (https://api.tzkt.io). Always await them — never fire-and-forget
    /// on the main thread unless you are certain the result is not needed immediately.
    ///
    /// ERROR HANDLING OVERVIEW:
    ///   All HTTP calls route through SendWithRetryAsync(), which retries on transient
    ///   failures (connection errors, HTTP 429 Too Many Requests, HTTP 5xx server errors)
    ///   up to MaxRetries times with a fixed RetryDelaySeconds delay between attempts.
    ///   Non-retryable failures (HTTP 4xx, malformed JSON) fail immediately and return
    ///   safe default values (false for ownership checks, empty lists for inventory).
    ///   All failures are logged with a consistent [TezosExtensions] prefix so they are
    ///   easy to filter in the Unity Console.
    ///
    /// PAGINATION OVERVIEW:
    ///   TzKT paginates results using two query parameters:
    ///     limit  — how many records to return in one response (max 10000, practical max ~100)
    ///     offset — how many records to skip before returning results (0-based)
    ///
    ///   Example: a wallet with 35 NFTs, page size 10
    ///     Page 1: offset=0,  limit=10 → tokens 1–10   (10 results → more pages exist)
    ///     Page 2: offset=10, limit=10 → tokens 11–20  (10 results → more pages exist)
    ///     Page 3: offset=20, limit=10 → tokens 21–30  (10 results → more pages exist)
    ///     Page 4: offset=30, limit=10 → tokens 31–35  ( 5 results → last page reached)
    ///
    ///   A page is the last page when the number of results returned is less than the
    ///   requested limit. This avoids a separate "count" request.
    ///
    /// CHOOSING A PAGE SIZE:
    ///   DefaultPageSize (10) — safe for inventory preview panels and "load more" UX
    ///   MaxPageSize     (50) — suitable for full inventory loads in a loading screen
    ///   Larger values reduce round-trips but increase per-frame memory allocation.
    /// </summary>
    public static class TezosExtensions
    {
        // TzKT base endpoint for all token balance queries.
        // balance.ne=0 filters out tokens that were previously held but fully transferred.
        private const string TzKtBaseUrl = "https://api.tzkt.io/v1/tokens/balances?balance.ne=0";

        // URL-encoded select clause requesting only the fields mapped by TokenBalance.
        // Requesting only needed fields reduces response payload size.
        private const string TokenSelectFields =
            "account.address%20as%20owner," +
            "balance," +
            "token.contract.address%20as%20tokenContract," +
            "token.tokenId%20as%20tokenId," +
            "token.metadata%20as%20tokenMetadata," +
            "lastTime," +
            "id";

        /// <summary>
        /// Number of tokens fetched per page when using paginated methods.
        /// Suitable for inventory preview panels and incremental "load more" UX.
        /// </summary>
        public const int DefaultPageSize = 10;

        /// <summary>
        /// Largest recommended page size for a single TzKT request.
        /// Use this for full inventory loads that happen during a loading screen.
        /// TzKT supports up to 10000, but allocating large arrays per frame is wasteful.
        /// </summary>
        public const int MaxPageSize = 50;

        // Maximum number of attempts for a single HTTP request before giving up.
        // The first attempt counts, so MaxRetries = 3 means one initial try plus two retries.
        private const int MaxRetries = 3;

        // Seconds to wait between retry attempts.
        // A fixed delay is sufficient for a game SDK. In a production service you would
        // use exponential backoff: delay = RetryDelaySeconds * Math.Pow(2, attempt - 1).
        private const float RetryDelaySeconds = 1.5f;


        // ---------------------------------------------------------------
        //  Authentication helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns true if the SDK currently holds an active wallet address.
        /// Use this as your "is the player logged in?" check.
        /// </summary>
        public static bool HasActiveWalletAddress(this ITezosAPI tezos)
        {
            return !string.IsNullOrEmpty(tezos.GetActiveWalletAddress());
        }


        // ---------------------------------------------------------------
        //  Token ownership — single-token check
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns true if <paramref name="account"/> currently holds at least one
        /// unit of the token identified by <paramref name="contract"/> and <paramref name="tokenId"/>.
        ///
        /// Returns false on network failure — treat a false result as "ownership unconfirmed"
        /// rather than "definitively does not own" if you expect network issues.
        ///
        /// Use this for token gating: checking whether the player owns a specific NFT
        /// before unlocking a feature, area, or character.
        /// </summary>
        public static async UniTask<bool> IsOwnerOfToken(
            this ITezosAPI tezos, string account, string contract, int tokenId)
        {
            return await CheckTokenBalance(account, contract, tokenId);
        }


        // ---------------------------------------------------------------
        //  Token inventory — full load (original API, preserved)
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns all tokens currently held by <paramref name="account"/>,
        /// fetching every page automatically until the full inventory is assembled.
        ///
        /// Returns an empty list on unrecoverable network failure.
        ///
        /// Suitable for wallets with small-to-moderate inventories. For large inventories
        /// (100+ tokens) or incremental UI loading, prefer
        /// <see cref="GetAllTokensForOwnerPaginated"/> or <see cref="GetTokensForOwnerPage"/>.
        /// </summary>
        public static async UniTask<List<TokenBalance>> GetAllTokensForOwner(
            this ITezosAPI tezos, string account)
        {
            return await FetchAllPages(account, pageSize: MaxPageSize);
        }


        // ---------------------------------------------------------------
        //  Token inventory — single page fetch
        // ---------------------------------------------------------------

        /// <summary>
        /// Fetches one page of tokens held by <paramref name="account"/>.
        ///
        /// Returns an empty list on unrecoverable network failure.
        ///
        /// PAGINATION CONCEPT — Single page fetch:
        ///   Use this to implement "Load More" buttons or lazy-loading inventory grids.
        ///   Store the current offset in your MonoBehaviour and increment it by the
        ///   returned list's Count after each call.
        ///
        ///   Example usage:
        ///   <code>
        ///   // First page
        ///   List&lt;TokenBalance&gt; page1 = await tezos.GetTokensForOwnerPage(address, offset: 0, limit: 10);
        ///
        ///   // Next page — called when the player clicks "Load More"
        ///   List&lt;TokenBalance&gt; page2 = await tezos.GetTokensForOwnerPage(address, offset: 10, limit: 10);
        ///
        ///   // Stop when the returned list is smaller than the requested limit
        ///   bool isLastPage = page2.Count &lt; 10;
        ///   </code>
        /// </summary>
        /// <param name="offset">Number of records to skip. Pass 0 for the first page.</param>
        /// <param name="limit">Maximum records to return. Defaults to <see cref="DefaultPageSize"/>.</param>
        public static async UniTask<List<TokenBalance>> GetTokensForOwnerPage(
            this ITezosAPI tezos, string account, int offset = 0, int limit = DefaultPageSize)
        {
            return await FetchTokensPage(account, offset, limit);
        }


        // ---------------------------------------------------------------
        //  Token inventory — progressive full load with per-page callback
        // ---------------------------------------------------------------

        /// <summary>
        /// Fetches the complete token inventory of <paramref name="account"/> page by page,
        /// invoking <paramref name="onPageLoaded"/> after each page arrives.
        ///
        /// Returns the partial inventory assembled before any unrecoverable failure.
        ///
        /// PAGINATION CONCEPT — Progressive loading:
        ///   Rather than waiting for all tokens before updating the UI, this method lets
        ///   your game display inventory items as each page arrives. The player sees the
        ///   first items almost immediately, with more appearing as subsequent pages load.
        ///
        ///   This is the recommended pattern for inventory screens with many tokens.
        ///
        ///   Example usage:
        ///   <code>
        ///   int totalLoaded = 0;
        ///   await tezos.GetAllTokensForOwnerPaginated(
        ///       address,
        ///       pageSize: 10,
        ///       onPageLoaded: (page, pageIndex) =>
        ///       {
        ///           totalLoaded += page.Count;
        ///           foreach (TokenBalance token in page)
        ///           {
        ///               SpawnInventoryCard(token); // update UI immediately per page
        ///           }
        ///           Debug.Log($"Page {pageIndex + 1} loaded: {page.Count} tokens ({totalLoaded} total so far)");
        ///       });
        ///   </code>
        /// </summary>
        /// <param name="pageSize">Tokens per request. Defaults to <see cref="DefaultPageSize"/>.</param>
        /// <param name="onPageLoaded">
        ///   Called after each page is received. Parameters: the page's token list and the
        ///   zero-based page index. Use this to update your inventory UI incrementally.
        /// </param>
        /// <returns>The complete assembled inventory across all pages.</returns>
        public static async UniTask<List<TokenBalance>> GetAllTokensForOwnerPaginated(
            this ITezosAPI tezos,
            string account,
            int pageSize = DefaultPageSize,
            Action<List<TokenBalance>, int> onPageLoaded = null)
        {
            return await FetchAllPages(account, pageSize, onPageLoaded);
        }


        // ---------------------------------------------------------------
        //  Private implementation
        // ---------------------------------------------------------------

        /// <summary>
        /// Checks whether a specific token balance exists for an account.
        /// Requests only the id field — the smallest possible payload for a boolean check.
        /// Returns false on any unrecoverable failure.
        /// </summary>
        private static async UniTask<bool> CheckTokenBalance(
            string account, string contract, int tokenId)
        {
            string context = $"CheckTokenBalance({account}, {contract}#{tokenId})";
            string url = $"{TzKtBaseUrl}&account={account}" +
                         $"&token.contract={contract}" +
                         $"&token.tokenId={tokenId}" +
                         $"&select=id";

            HttpResult result = await SendWithRetryAsync(url, context);
            if (!result.Success)
                return false;

            try
            {
                return JsonHelper.FromJson<int>(result.Text).Length > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TezosExtensions] {context} — JSON parse failed: {ex.Message}" +
                               $"\n  Raw response: {Truncate(result.Text, 200)}");
                return false;
            }
        }

        /// <summary>
        /// Fetches a single page of token balances from TzKT.
        ///
        /// This is the single HTTP-calling method for all inventory queries. Both
        /// <see cref="GetTokensForOwnerPage"/> and <see cref="FetchAllPages"/> delegate here,
        /// keeping network logic in one place.
        /// Returns an empty list on any unrecoverable failure.
        /// </summary>
        /// <param name="account">Wallet address to query (tz1...).</param>
        /// <param name="offset">Number of records to skip (0 for the first page).</param>
        /// <param name="limit">Maximum records to return in this request.</param>
        private static async UniTask<List<TokenBalance>> FetchTokensPage(
            string account, int offset, int limit)
        {
            string context = $"FetchTokensPage({account}, offset={offset}, limit={limit})";

            // &sort.asc=id ensures stable ordering across pages — without a sort order,
            // TzKT may return overlapping or skipped records between requests.
            string url = $"{TzKtBaseUrl}" +
                         $"&account={account}" +
                         $"&select={TokenSelectFields}" +
                         $"&sort.asc=id" +
                         $"&offset={offset}" +
                         $"&limit={limit}";

            HttpResult result = await SendWithRetryAsync(url, context);
            if (!result.Success)
                return new List<TokenBalance>();

            try
            {
                return JsonHelper.FromJson<TokenBalance>(result.Text).ToList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TezosExtensions] {context} — JSON parse failed: {ex.Message}" +
                               $"\n  Raw response: {Truncate(result.Text, 200)}");
                return new List<TokenBalance>();
            }
        }

        /// <summary>
        /// Fetches all pages for an account sequentially, assembling the full inventory.
        /// Stops when a page returns fewer items than <paramref name="pageSize"/>,
        /// which signals that the last page has been reached. Also stops early if a page
        /// fetch fails, returning the partial inventory collected so far.
        /// </summary>
        /// <param name="onPageLoaded">
        ///   Optional callback invoked after each successful page. Receives the page list
        ///   and its zero-based index. Null-safe — omit it for a silent full load.
        /// </param>
        private static async UniTask<List<TokenBalance>> FetchAllPages(
            string account,
            int pageSize,
            Action<List<TokenBalance>, int> onPageLoaded = null)
        {
            List<TokenBalance> allTokens = new List<TokenBalance>();
            int pageIndex = 0;

            while (true)
            {
                int offset = pageIndex * pageSize;
                List<TokenBalance> page = await FetchTokensPage(account, offset, pageSize);

                // An empty page on the first request means the wallet has no tokens,
                // or the request failed. Either way, stop — there is nothing more to fetch.
                // On subsequent pages, an empty result after a previous success means the
                // fetch failed (FetchTokensPage returns [] on error). Stop and return what
                // we have rather than looping forever.
                if (page.Count == 0)
                {
                    if (pageIndex > 0)
                        Debug.LogWarning($"[TezosExtensions] FetchAllPages — page {pageIndex + 1} " +
                                         $"returned 0 results. Returning {allTokens.Count} token(s) fetched so far.");
                    break;
                }

                allTokens.AddRange(page);
                onPageLoaded?.Invoke(page, pageIndex);

                // A page smaller than the requested limit means we have reached the end.
                // This avoids an extra round-trip to confirm the inventory is exhausted.
                if (page.Count < pageSize)
                    break;

                pageIndex++;
            }

            Debug.Log($"[TezosExtensions] FetchAllPages — complete. " +
                      $"{allTokens.Count} token(s) for {account} across {pageIndex + 1} page(s).");

            return allTokens;
        }


        // ---------------------------------------------------------------
        //  HTTP helper — retry logic
        // ---------------------------------------------------------------

        /// <summary>
        /// Sends an HTTP GET request to <paramref name="url"/>, retrying up to
        /// <see cref="MaxRetries"/> times on transient failures before giving up.
        ///
        /// RETRY POLICY:
        ///   Retried (temporary, worth trying again):
        ///     - ConnectionError  — no network, DNS failure, timeout
        ///     - HTTP 429         — Too Many Requests (TzKT rate limit)
        ///     - HTTP 5xx         — TzKT server error
        ///
        ///   Not retried (permanent, retrying will not help):
        ///     - HTTP 400         — malformed request URL (bug in this code)
        ///     - HTTP 404         — resource does not exist
        ///     - HTTP 4xx (other) — client error unlikely to resolve on retry
        ///     - DataProcessingError — Unity failed to process the response body
        ///
        /// <paramref name="context"/> is a short description of the calling operation
        /// used to prefix all log messages, making them easy to filter in the Console.
        /// </summary>
        private static async UniTask<HttpResult> SendWithRetryAsync(string url, string context)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    using (UnityWebRequest request = UnityWebRequest.Get(url))
                    {
                        await request.SendWebRequest();

                        // SUCCESS
                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            if (attempt > 1)
                                Debug.Log($"[TezosExtensions] {context} — succeeded on attempt {attempt}/{MaxRetries}.");
                            return new HttpResult(true, request.downloadHandler.text, request.responseCode);
                        }

                        long statusCode = request.responseCode;
                        string error    = request.error;
                        bool isRetryable = IsRetryable(request.result, statusCode);

                        // PERMANENT FAILURE — log full detail and return immediately
                        if (!isRetryable || attempt == MaxRetries)
                        {
                            string detail = request.result == UnityWebRequest.Result.ProtocolError
                                ? $"HTTP {statusCode}"
                                : request.result.ToString();
                            Debug.LogError(
                                $"[TezosExtensions] {context} — failed after {attempt} attempt(s).\n" +
                                $"  Result:  {detail}\n" +
                                $"  Error:   {error}\n" +
                                $"  URL:     {url}");
                            return new HttpResult(false, null, statusCode);
                        }

                        // TRANSIENT FAILURE — warn and retry
                        Debug.LogWarning(
                            $"[TezosExtensions] {context} — attempt {attempt}/{MaxRetries} failed " +
                            $"({request.result}, HTTP {statusCode}). " +
                            $"Retrying in {RetryDelaySeconds}s...");
                    }
                }
                catch (Exception ex)
                {
                    // Catches unexpected exceptions from SendWebRequest or handler access.
                    // Treat as a retryable failure unless we are out of attempts.
                    if (attempt == MaxRetries)
                    {
                        Debug.LogError(
                            $"[TezosExtensions] {context} — unhandled exception after {attempt} attempt(s).\n" +
                            $"  Exception: {ex.GetType().Name}: {ex.Message}\n" +
                            $"  URL:       {url}");
                        return new HttpResult(false, null, 0);
                    }

                    Debug.LogWarning(
                        $"[TezosExtensions] {context} — attempt {attempt}/{MaxRetries} threw " +
                        $"{ex.GetType().Name}: {ex.Message}. Retrying in {RetryDelaySeconds}s...");
                }

                await UniTask.Delay(TimeSpan.FromSeconds(RetryDelaySeconds));
            }

            // Unreachable — the loop always returns or breaks before here.
            return new HttpResult(false, null, 0);
        }

        /// <summary>
        /// Returns true if the given result/status code combination is worth retrying.
        /// Connection errors and server-side faults are transient; client errors are not.
        /// </summary>
        private static bool IsRetryable(UnityWebRequest.Result result, long statusCode)
        {
            if (result == UnityWebRequest.Result.ConnectionError)
                return true;

            if (result == UnityWebRequest.Result.ProtocolError)
                return statusCode == 429   // Too Many Requests — back off and retry
                    || statusCode >= 500;  // Server error — TzKT may be temporarily unavailable

            // DataProcessingError and other results are not retryable.
            return false;
        }

        /// <summary>
        /// Truncates a string to <paramref name="maxLength"/> characters for safe log output.
        /// Appends "…" when truncation occurs so it is clear the text is incomplete.
        /// </summary>
        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text ?? "(null)";
            return text.Substring(0, maxLength) + "…";
        }


        // ---------------------------------------------------------------
        //  HTTP result value type
        // ---------------------------------------------------------------

        /// <summary>
        /// Carries the outcome of a single HTTP request out of <see cref="SendWithRetryAsync"/>.
        /// Using a value type avoids a heap allocation per request compared to a class.
        /// </summary>
        private readonly struct HttpResult
        {
            /// <summary>True if the request completed with HTTP 2xx.</summary>
            public readonly bool   Success;

            /// <summary>Response body text. Null when Success is false.</summary>
            public readonly string Text;

            /// <summary>HTTP status code. 0 if no response was received (connection error).</summary>
            public readonly long   StatusCode;

            public HttpResult(bool success, string text, long statusCode)
            {
                Success    = success;
                Text       = text;
                StatusCode = statusCode;
            }
        }


        // ---------------------------------------------------------------
        //  JSON helper
        // ---------------------------------------------------------------

        /// <summary>
        /// Wraps a raw JSON array string so UnityEngine.JsonUtility can deserialize it.
        /// JsonUtility does not support top-level arrays; this class provides the required wrapper.
        /// </summary>
        public static class JsonHelper
        {
            public static T[] FromJson<T>(string json_array)
            {
                string json_obj = "{\"items\":" + json_array + "}";
                Wrapper<T> wrapper = UnityEngine.JsonUtility.FromJson<Wrapper<T>>(json_obj);
                return wrapper.items;
            }

            [Serializable]
            private class Wrapper<T>
            {
                public T[] items;
            }
        }
    }


    // ---------------------------------------------------------------
    //  Token balance data model
    // ---------------------------------------------------------------

    /// <summary>
    /// Represents one token balance record returned by the TzKT API.
    /// Each instance corresponds to one token type held by a wallet.
    ///
    /// Fields are populated by UnityEngine.JsonUtility from the TzKT response.
    /// Field names match the aliases defined in the TokenSelectFields query parameter
    /// in <see cref="TezosExtensions"/>.
    /// </summary>
    [Serializable]
    public class TokenBalance
    {
        /// <summary>
        /// Internal TzKT record ID. Used as the stable sort key for pagination (&sort.asc=id).
        /// Do not use this as a game-facing item ID — use tokenContract + tokenId instead.
        /// </summary>
        public long id;

        /// <summary>
        /// Wallet address of the token owner (tz1...).
        /// Matches the account address used in the query.
        /// </summary>
        public string owner;

        /// <summary>
        /// Raw token balance as a string (not divided by decimals).
        /// For standard NFTs this will be "1". For fungible or semi-fungible FA2 tokens
        /// it may be higher. Parse to long or decimal before arithmetic.
        /// </summary>
        public string balance;

        /// <summary>
        /// KT1... address of the FA2 contract that defines this token.
        /// Use this to identify which game or collection the token belongs to.
        /// </summary>
        public string tokenContract;

        /// <summary>
        /// Token ID within the FA2 contract. Combined with tokenContract, this uniquely
        /// identifies one token type across the entire Tezos blockchain.
        /// </summary>
        public string tokenId;

        /// <summary>
        /// On-chain JSON metadata for this token (TZIP-21 standard).
        /// Common fields: name, description, displayUri (image URL), attributes (array).
        /// Use this to drive item display names, thumbnails, and stat values in your game.
        ///
        /// Note: JsonUtility cannot deserialize arbitrary JSON objects into JsonElement.
        /// Parse tokenMetadata fields manually using JsonDocument or Newtonsoft.Json if needed.
        /// </summary>
        public JsonElement tokenMetadata;

        /// <summary>
        /// ISO 8601 timestamp of the block where this balance was last changed
        /// (mint, transfer, or burn). Stored as string because JsonUtility cannot
        /// reliably deserialize ISO dates to DateTime.
        /// </summary>
        public string lastTime;
    }
}
