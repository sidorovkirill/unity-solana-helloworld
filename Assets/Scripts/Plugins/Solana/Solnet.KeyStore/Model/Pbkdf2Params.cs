
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Solnet.KeyStore.Model
{
    public class Pbkdf2Params : KdfParams
    {
        [JsonProperty(PropertyName = "c")]
        public int Count { get; set; }

        [JsonProperty(PropertyName = "prf")]
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string Prf { get; set; }
    }
}