using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Threading.Tasks;
using Merkator.BitCoin;
using Newtonsoft.Json;
using Phantom.DTO;

namespace Phantom
{
	public class PhantomBridge
	{
		private string _appUrl = "https://unity.com";
		private string _appUrlScheme = "unitydl://";
		private string _cluster;

		private readonly DeepLinkProtocol _protocol;
		private readonly PhantomBridgeVault _vault;

		private PhantomConnectDTO _connectData;
		
		public bool IsConnected { get; private set; }
		public bool AutoConnect { get; set; } = false;

		public PhantomBridge()
		{
			_vault = new PhantomBridgeVault();
			_protocol = new DeepLinkProtocol();
		}
		
		public PhantomBridge(string appUrlScheme, string appUrl, string cluster = Cluster.Devnet) : this()
		{
			_appUrlScheme = appUrlScheme;
			_appUrl = appUrl;
			_cluster = cluster;
		}

		public void Init()
		{
			_protocol.Init();
		}

		public async Task<string> Connect()
		{
			if (IsConnected)
			{
				throw new InvalidOperationException("Wallet already connected");
			}
			
			var data = new DeepLinkData
			{
				Method = Requests.ConnectMethod,
				Params = new Dictionary<string, string>
				{
					{ QueryParams.AppURL, Encode(_appUrl) },
					{ QueryParams.RedirectLink, GetCallbackUrl(Responses.ConnectCallbackUrl) },
					{ QueryParams.PubKey, _vault.PublicKey },
					{ QueryParams.Cluster, _cluster },
				}
			};

			var response = await _protocol.Send(data);

			HasError(response);

			return HandleConnectResponse(response);
		}

		public async Task Disconnect()
		{
			if (!IsConnected)
			{
				throw new InvalidOperationException("Wallet not connected");
			}

			var payload = new Dictionary<string, string>();
			payload.Add(PayloadParams.Session, _connectData.Session);

			var (encryptedPayload, nonce) = _vault.EncryptPayload(payload);

			var data = new DeepLinkData
			{
				Method = Requests.DisconnectMethod,
				Params = new Dictionary<string, string>
				{
					{ QueryParams.RedirectLink, GetCallbackUrl(Responses.DisconnectCallbackUrl) },
					{ QueryParams.PubKey, _vault.PublicKey },
					{ QueryParams.Nonce, nonce },
					{ QueryParams.Payload, encryptedPayload },
				}
			};

			var response = await _protocol.Send(data);

			HasError(response);

			HandleDisconnect();
		}

		public async Task<string> SignMessage(string msg)
		{
			await CheckConnection();

			var payload = new Dictionary<string, string>();
			payload.Add(PayloadParams.Session, _connectData.Session);
			payload.Add(PayloadParams.Message, Base58Encoding.Encode(System.Text.Encoding.UTF8.GetBytes(msg)));

			var (encryptedPayload, nonce) = _vault.EncryptPayload(payload);

			var data = new DeepLinkData
			{
				Method = Requests.SignMessageMethod,
				Params = new Dictionary<string, string>
				{
					{ QueryParams.RedirectLink, GetCallbackUrl(Responses.SignMessageCallbackUrl) },
					{ QueryParams.PubKey, _vault.PublicKey },
					{ QueryParams.Nonce, nonce },
					{ QueryParams.Payload, encryptedPayload },
				}
			};

			var response = await _protocol.Send(data);

			HasError(response);

			return HandleMessageSigned(response);
		}

		public async Task<string> SignAndSendTransaction(byte[] serializedTx)
		{
			await CheckConnection();

			var payload = new Dictionary<string, string>();
			payload.Add(PayloadParams.Session, _connectData.Session);
			payload.Add(PayloadParams.Transaction, Base58Encoding.Encode(serializedTx));

			var (encryptedPayload, nonce) = _vault.EncryptPayload(payload);

			var data = new DeepLinkData
			{
				Method = Requests.SignAndSendTxMethod,
				Params = new Dictionary<string, string>
				{
					{ QueryParams.RedirectLink, GetCallbackUrl(Responses.SignAndSendTxCallbackUrl) },
					{ QueryParams.PubKey, _vault.PublicKey },
					{ QueryParams.Nonce, nonce },
					{ QueryParams.Payload, encryptedPayload },
				}
			};

			var response = await _protocol.Send(data);

			HasError(response);

			return HandleTxSignedAndSent(response);
		}

		public async Task<string> SignTransaction(byte[] serializedTx)
		{
			await CheckConnection();

			var payload = new Dictionary<string, string>();
			payload.Add(PayloadParams.Session, _connectData.Session);
			payload.Add(PayloadParams.Transaction, Base58Encoding.Encode(serializedTx));

			var (encryptedPayload, nonce) = _vault.EncryptPayload(payload);

			var data = new DeepLinkData
			{
				Method = Requests.SignTxMethod,
				Params = new Dictionary<string, string>
				{
					{ QueryParams.RedirectLink, GetCallbackUrl(Responses.SignTxCallbackUrl) },
					{ QueryParams.PubKey, _vault.PublicKey },
					{ QueryParams.Nonce, nonce },
					{ QueryParams.Payload, encryptedPayload },
				}
			};

			var response = await _protocol.Send(data);

			HasError(response);

			return HandleTxSigned(response);
		}

		private string HandleConnectResponse(DeepLinkData data)
		{
			var phantomPubkey = Base58Encoding.Decode(data.Params[QueryParams.PubKey]);
			_vault.GenerateSecret(phantomPubkey);
			var nonce = Base58Encoding.Decode(data.Params[QueryParams.PubKey]);

			var decryptedStr = _vault.DecryptPayload(data.Params[QueryParams.Data], nonce);
			_connectData = JsonConvert.DeserializeObject<PhantomConnectDTO>(decryptedStr);

			IsConnected = true;
			return _connectData.WalletPublicKey;
		}

		private void HandleDisconnect()
		{
			IsConnected = false;
		}

		private string HandleTxSignedAndSent(DeepLinkData data)
		{
			var nonce = Base58Encoding.Decode(data.Params[QueryParams.Nonce]);
			var decryptedStr = _vault.DecryptPayload(data.Params[QueryParams.Data], nonce);
			var dataDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(decryptedStr);

			return dataDic[PayloadParams.Signature];
		}

		private string HandleMessageSigned(DeepLinkData data)
		{
			var nonce = Base58Encoding.Decode(data.Params[QueryParams.Nonce]);
			var decryptedStr = _vault.DecryptPayload(data.Params[QueryParams.Data], nonce);
			var dataDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(decryptedStr);
			var signature = dataDic[PayloadParams.Signature];

			return signature;
		}

		private string HandleTxSigned(DeepLinkData data)
		{
			var nonce = Base58Encoding.Decode(data.Params[QueryParams.Nonce]);
			var decryptedStr = _vault.DecryptPayload(data.Params[QueryParams.Data], nonce);
			var dataDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(decryptedStr);
			var signature = dataDic[PayloadParams.Transaction];

			return signature;
		}

		private async Task CheckConnection()
		{
			if (!AutoConnect && !IsConnected)
			{
				throw new InvalidOperationException("Wallet not connected");
			}

			if (AutoConnect && !IsConnected)
			{
				await Connect();
			}
		}

		private string GetCallbackUrl(string method)
		{
			return Encode($"{_appUrlScheme}{method}");
		}

		private static string Encode(string url)
		{
			return UnityWebRequest.EscapeURL(url);
		}

		private static bool HasError(DeepLinkData data)
		{
			if (data.Params.ContainsKey(QueryParams.ErrorCode))
			{
				throw new Exception($"Error {data.Params[QueryParams.ErrorCode]}: {data.Params[QueryParams.ErrorMessage]}");
			}

			return false;
		}
	}
}