﻿using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Wallet.Bip39;
using System;
using UnityEngine;

namespace Solnet.Examples
{
    public class HelloWorldExample : IExample
    {
        public void Run()
        {
            var wallet = new Wallet.Wallet(WordCount.TwentyFour, WordList.English);

            Debug.Log("Hello World!");
            Debug.Log($"Mnemonic: {wallet.Mnemonic}");
            Debug.Log($"PubKey: {wallet.Account.PublicKey.Key}");
            Debug.Log($"PrivateKey: {wallet.Account.PrivateKey.Key}");

            IRpcClient rpcClient = ClientFactory.GetClient(Cluster.TestNet);

            var balance = rpcClient.GetBalance(wallet.Account.PublicKey);

            Debug.Log($"Balance: {balance.Result.Value}");

            var transactionHash = rpcClient.RequestAirdrop(wallet.Account.PublicKey, 100_000_000);

            Debug.Log($"TxHash: {transactionHash.Result}");

            IStreamingRpcClient streamingRpcClient = ClientFactory.GetStreamingClient(Cluster.TestNet);

            streamingRpcClient.ConnectAsync().Wait();

            var subscription = streamingRpcClient.SubscribeSignature(transactionHash.Result, (sub, data) =>
            {
                if (data.Value.Error == null)
                {
                    var balance = rpcClient.GetBalance(wallet.Account.PublicKey);

                    Debug.Log($"Balance: {balance.Result.Value}");

                    var memoInstruction = MemoProgram.NewMemoV2("Hello Solana World, using Solnet :)");

                    var recentHash = rpcClient.GetRecentBlockHash();

                    var tx = new TransactionBuilder().AddInstruction(memoInstruction).SetFeePayer(wallet.Account)
                        .SetRecentBlockHash(recentHash.Result.Value.Blockhash).Build(wallet.Account);

                    var txHash = rpcClient.SendTransaction(tx);

                    Debug.Log($"TxHash: {txHash.Result}");
                }
                else
                {
                    Debug.Log($"Transaction error: {data.Value.Error.Type}");
                }
            });

            Console.ReadLine();
        }
    }
}