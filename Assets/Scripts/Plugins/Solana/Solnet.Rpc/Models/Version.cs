using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Solnet.Rpc.Models
{
    /// <summary>
    /// Represents the current solana versions running on the node.
    /// </summary>
    public class NodeVersion
    {
        /// <summary>
        /// Software version of solana-core.
        /// </summary>
        [JsonProperty(PropertyName = "solana-core")]
        public string SolanaCore { get; set; }

        /// <summary>
        /// unique identifier of the current software's feature set.
        /// </summary>
        [JsonProperty(PropertyName = "feature-set")]
        public ulong? FeatureSet { get; set; }
    }
}