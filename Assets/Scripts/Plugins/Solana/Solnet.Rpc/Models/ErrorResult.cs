using Newtonsoft.Json;

namespace Solnet.Rpc.Models
{
    /// <summary>
    /// Holds an error result.
    /// </summary>
    public class ErrorResult
    {
        /// <summary>
        /// The error string.
        /// </summary>
        [JsonProperty(PropertyName = "err")]
        public TransactionError Error { get; set; }
    }
}