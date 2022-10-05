using System.Collections.Generic;
using System.Security.Cryptography;
using Chaos.NaCl;
using Merkator.BitCoin;
using Newtonsoft.Json;
using UnityEngine;

namespace Phantom
{
	public class PhantomBridgeVault
	{
		private byte[] _publicKey;
		private byte[] _privateKey;
		private byte[] _sharedSecret;

		public string PublicKey
		{
			get { return Base58Encoding.Encode(_publicKey); }
		}

		public PhantomBridgeVault()
		{
			GenerateKeypair();
		}

		public void GenerateSecret(byte[] handshakePublicKey)
		{
			_sharedSecret = MontgomeryCurve25519.KeyExchange(handshakePublicKey, _privateKey);
		}

		public string DecryptPayload(string payload, byte[] nonce)
		{
			var encodedData = Base58Encoding.Decode(payload);
			var decryptedData = XSalsa20Poly1305.TryDecrypt(encodedData, _sharedSecret, nonce);
			string decryptedStr = System.Text.Encoding.UTF8.GetString(decryptedData);
			;

			return decryptedStr;
		}

		public (string, string) EncryptPayload(Dictionary<string, string> payload)
		{
			var payloadStr = JsonConvert.SerializeObject(payload);
			Debug.Log("Payload str: " + payloadStr);
			var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payloadStr);
			var nonce = GetRandomBytes(24);

			var encryptedBytes = XSalsa20Poly1305.Encrypt(payloadBytes, _sharedSecret, nonce);
			return (Base58Encoding.Encode(encryptedBytes), Base58Encoding.Encode(nonce));
		}
		
		private void GenerateKeypair()
		{
			_privateKey = GetRandomBytes(32);
			_publicKey = MontgomeryCurve25519.GetPublicKey(_privateKey);
		}
		
		private static byte[] GetRandomBytes(int length)
		{
			var randomBytes = new byte[length];
			var rnd = new RNGCryptoServiceProvider();
			rnd.GetBytes(randomBytes);

			return randomBytes;
		}
	}
}