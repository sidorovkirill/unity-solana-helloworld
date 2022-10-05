using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Solnet.KeyStore.Model
{
    public class CipherParams
    {
        public CipherParams()
        {
        }

        public CipherParams(byte[] iv)
        {
            Iv = iv.ToHex();
        }

        [JsonProperty(PropertyName = "iv")]
        // ReSharper disable once MemberCanBePrivate.Global
        public string Iv { get; set; }
    }
}