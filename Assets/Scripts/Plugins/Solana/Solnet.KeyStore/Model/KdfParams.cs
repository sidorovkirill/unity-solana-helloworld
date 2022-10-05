using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Solnet.KeyStore.Model
{
    public class KdfParams
    {
        // ReSharper disable once StringLiteralTypo
        [JsonProperty(PropertyName = "dklen")]
        // ReSharper disable once IdentifierTypo
        public int Dklen { get; set; }

        [JsonProperty(PropertyName = "salt")]
        public string Salt { get; set; }
    }
}