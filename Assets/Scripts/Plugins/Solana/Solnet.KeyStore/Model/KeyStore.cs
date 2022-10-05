using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Solnet.KeyStore.Model
{
    public class KeyStore<TKdfParams> where TKdfParams : KdfParams
    {
        [JsonProperty(PropertyName = "crypto")]
        public CryptoInfo<TKdfParams> Crypto { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }
    }
}