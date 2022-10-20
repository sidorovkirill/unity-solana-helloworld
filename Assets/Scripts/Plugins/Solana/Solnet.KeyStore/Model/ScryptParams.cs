using Newtonsoft.Json;

namespace Solnet.KeyStore.Model
{
    public class ScryptParams : KdfParams
    {
        [JsonProperty(PropertyName = "n")]
        public int N { get; set; }

        [JsonProperty(PropertyName = "r")]
        public int R { get; set; }

        [JsonProperty(PropertyName = "p")]
        public int P { get; set; }
    }
}