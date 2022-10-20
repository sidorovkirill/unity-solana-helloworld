using System;
using System.Threading.Tasks;
using Phantomity;
using Phantomity.Constants;
using Phantomity.DTO;
using Phantomity.Infrastructure;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using UnityEngine;
using Cluster = Solnet.Rpc.Cluster;

namespace DefaultNamespace
{
	public class BridgeExample : MonoBehaviour
	{
		private readonly IRpcClient _rpcClient = ClientFactory.GetClient(Cluster.DevNet);
		private IPhantomBridge _phantomBridge;

		private void Awake()
		{
			_phantomBridge = new PhantomBridge();
		}

		private void Start()
		{
			// Initialize().ConfigureAwait(false);
			TransactionExample().ConfigureAwait(false);
		}

		private async Task Initialize()
		{
			var address = await _phantomBridge.Connect();
			Debug.Log("address = " + address);
			var signature = await _phantomBridge.SignMessage("HelloWorld");
			Debug.Log("signature = " + signature);
		}

		private async Task TransactionExample()
		{
			RequestResult<ResponseValue<LatestBlockHash>> blockHash = await _rpcClient.GetLatestBlockHashAsync();
			var address = await _phantomBridge.Connect();
			var payer = new PublicKey(address);
			var receiver = new PublicKey("4qp9sCC6wK8pUK2SahRX9YcYHRdbqwXPkQDUMwEGSGyt");
			
			byte[] tx = new TransactionBuilder()
				.SetRecentBlockHash(blockHash.Result.Value.Blockhash)
				.SetFeePayer(payer)
				.AddInstruction(SystemProgram.Transfer(payer, receiver, 10000000))
				.Serialize();

			try
			{
				var sendOptions = new SendOptions
				{
					MaxRetries = 3,
					PreflightCommitment = Commitment.Confirmed
				};
				var hash = await _phantomBridge.SignAndSendTransaction(tx, sendOptions);
				Debug.Log("hash = " + hash);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		private async Task OnDestroy()
		{
			await _phantomBridge.Disconnect();
		}
	}
}