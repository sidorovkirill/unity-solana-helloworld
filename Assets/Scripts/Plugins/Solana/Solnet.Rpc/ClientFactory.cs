using Solnet.Rpc.Utilities;
using System.Net.Http;
using System.Net.WebSockets;

namespace Solnet.Rpc
{
    /// <summary>
    /// Implements a client factory for Solana RPC and Streaming RPC APIs.
    /// </summary>
    public static class ClientFactory
    {
        /// <summary>
        /// The dev net cluster.
        /// </summary>
        private const string RpcDevNet = "https://api.devnet.solana.com";

        /// <summary>
        /// The test net cluster.
        /// </summary>
        private const string RpcTestNet = "https://api.testnet.solana.com";

        /// <summary>
        /// The main net cluster.
        /// </summary>
        private const string RpcMainNet = "https://api.mainnet-beta.solana.com";


        /// <summary>
        /// The dev net cluster.
        /// </summary>
        private const string StreamingRpcDevNet = "wss://api.devnet.solana.com";

        /// <summary>
        /// The test net cluster.
        /// </summary>
        private const string StreamingRpcTestNet = "wss://api.testnet.solana.com";

        /// <summary>
        /// The main net cluster.
        /// </summary>
        private const string StreamingRpcMainNet = "wss://api.mainnet-beta.solana.com";

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="cluster">The network cluster.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(Cluster cluster)
        {
            return GetClient(cluster, rateLimiter: null);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="cluster">The network cluster.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(
            Cluster cluster,
            IRateLimiter rateLimiter = null)
        {
            return GetClient(cluster, httpClient: null, rateLimiter: rateLimiter);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="cluster">The network cluster.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClient">A HttpClient instance. If null, a new instance will be created.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(Cluster cluster, HttpClient httpClient = null, IRateLimiter rateLimiter = null)
        {
            var url = cluster switch
            {
                Cluster.DevNet => RpcDevNet,
                Cluster.TestNet => RpcTestNet,
                _ => RpcMainNet,
            };
            
            return GetClient(url, httpClient, rateLimiter);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="url">The network cluster url.</param>
        /// <param name="logger">The logger.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(string url)
        {
            return GetClient(url);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="url">The network cluster url.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(string url, IRateLimiter rateLimiter)
        {
            return GetClient(url, httpClient: null, rateLimiter);
        }

        /// <summary>
        /// Instantiate a http client.
        /// </summary>
        /// <param name="url">The network cluster url.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClient">A HttpClient instance. If null, a new instance will be created.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null.</param>
        /// <returns>The http client.</returns>
        public static IRpcClient GetClient(string url, HttpClient httpClient = null, IRateLimiter rateLimiter = null)
        {
            return new SolanaRpcClient(url, httpClient, rateLimiter);
        }

        /// <summary>
        /// Instantiate a streaming client.
        /// </summary>
        /// <param name="cluster">The network cluster.</param>
        /// <param name="logger">The logger.</param>
        /// <returns>The streaming client.</returns>
        public static IStreamingRpcClient GetStreamingClient(Cluster cluster)
        {
            var url = cluster switch
            {
                Cluster.DevNet => StreamingRpcDevNet,
                Cluster.TestNet => StreamingRpcTestNet,
                _ => StreamingRpcMainNet,
            };
            return GetStreamingClient(url);
        }

        /// <summary>
        /// Instantiate a streaming client.
        /// </summary>
        /// <param name="url">The network cluster url.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="clientWebSocket">A ClientWebSocket instance. If null, a new instance will be created.</param>
        /// <returns>The streaming client.</returns>
        public static IStreamingRpcClient GetStreamingClient(string url, ClientWebSocket clientWebSocket = null)
        {
            return new SolanaStreamingRpcClient(url, null, clientWebSocket);
        }
    }
}