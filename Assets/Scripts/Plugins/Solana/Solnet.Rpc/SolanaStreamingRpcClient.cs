using Solnet.Rpc.Converters;
using Solnet.Rpc.Core;
using Solnet.Rpc.Core.Http;
using Solnet.Rpc.Core.Sockets;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Models;
using Solnet.Rpc.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Solnet.Rpc
{
    /// <summary>
    /// Implementation of the Solana streaming RPC API abstraction client.
    /// </summary>
    internal class SolanaStreamingRpcClient : StreamingRpcClient, IStreamingRpcClient
    {
        /// <summary>
        /// Message Id generator.
        /// </summary>
        private readonly IdGenerator _idGenerator = new IdGenerator();

        /// <summary>
        /// Maps the internal ids to the unconfirmed subscription state objects.
        /// </summary>
        private readonly Dictionary<int, SubscriptionState> unconfirmedRequests = new Dictionary<int, SubscriptionState>();

        /// <summary>
        /// Maps the server ids to the confirmed subscription state objects.
        /// </summary>
        private readonly Dictionary<int, SubscriptionState> confirmedSubscriptions = new Dictionary<int, SubscriptionState>();

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="url">The url of the server to connect to.</param>
        /// <param name="websocket">The possible IWebSocket instance.</param>
        /// <param name="clientWebSocket">The possible ClientWebSocket instance.</param>
        internal SolanaStreamingRpcClient(string url, IWebSocket websocket = default, ClientWebSocket clientWebSocket = default) : base(url, websocket, clientWebSocket)
        {
        }

        /// <inheritdoc cref="StreamingRpcClient.CleanupSubscriptions"/>
        protected override void CleanupSubscriptions()
        {
            foreach (var sub in confirmedSubscriptions)
            {
                sub.Value.ChangeState(SubscriptionStatus.Unsubscribed, "Connection terminated");
            }

            foreach (var sub in unconfirmedRequests)
            {
                sub.Value.ChangeState(SubscriptionStatus.Unsubscribed, "Connection terminated");
            }
            unconfirmedRequests.Clear();
            confirmedSubscriptions.Clear();
        }


        /// <inheritdoc cref="StreamingRpcClient.HandleNewMessage(Memory{byte})"/>
        protected override void HandleNewMessage(Memory<byte> messagePayload)
        {
            string str = Encoding.UTF8.GetString(messagePayload.ToArray());
            JsonTextReader jsonReader = new JsonTextReader(new StringReader(str));
            
            Debug.Log($"[Received]{str}");

            string prop = "", method = "";
            int id = -1, intResult = -1;
            bool handled = false;
            bool? boolResult = null;

            JsonTextReader savedState = default;

            // {"jsonrpc":"2.0","method":"signatureNotification","params":{"result":{"context":{"slot":153775504},"value":{"err":null}},"subscription":83076}}
            while (!handled && jsonReader.Read())
            {
                switch (jsonReader.TokenType)
                {
                    case JsonToken.PropertyName:
                        prop = jsonReader.Value.ToString();
                        Debug.Log("PropertyName -- " + prop);
                        if (prop == "error")
                        {
                            HandleError(ref jsonReader);
                            handled = true;
                        }
                        break;
                    case JsonToken.String:
                        Debug.Log("String -- " + prop);
                        if (prop == "method")
                        {
                            method = jsonReader.Value.ToString();
                        }
                        break;
                    case JsonToken.Integer:
                        Debug.Log("Integer -- " + prop);
                        if (prop == "id")
                        {
                            id = Convert.ToInt32(jsonReader.Value);
                        }
                        else if (prop == "result")
                        {
                            intResult = Convert.ToInt32(jsonReader.Value);
                        }
                        else if (prop == "subscription")
                        {
                            id = Convert.ToInt32(jsonReader.Value);
                            HandleDataMessage(str, method, id);
                            handled = true;
                        }
                        if (id != -1 && intResult != -1)
                        {
                            ConfirmSubscription(id, intResult);
                            handled = true;
                        }
                        break;
                    case JsonToken.Boolean:
                        Debug.Log("Boolean -- " + prop);
                        if (prop == "result")
                        {
                            // this is the result of an unsubscription
                            // I don't think its supposed to ever be false if we correctly manage the subscription ids
                            // maybe future followup
                            boolResult = jsonReader.ReadAsBoolean();
                        }
                        break;
                }
            }

            if (boolResult.HasValue)
            {
                RemoveSubscription(id, boolResult.Value);
            }
        }

        /// <summary>
        /// Handles and finishes parsing the contents of an error message.
        /// </summary>
        /// <param name="reader">The jsonReader that read the message so far.</param>
        private void HandleError(ref JsonTextReader reader)
        {
            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };

            var opts = new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                Formatting = Formatting.Indented
            };
            
            var err = JsonConvert.DeserializeObject<ErrorContent>(reader.Value.ToString(), opts);

            reader.Read();

            //var prop = reader.GetString(); //don't care about property name

            reader.Read();

            var id = Convert.ToInt32(reader.ReadAsInt32());;

            var sub = RemoveUnconfirmedSubscription(id);

            sub?.ChangeState(SubscriptionStatus.ErrorSubscribing, err.Message, err.Code.ToString());
        }


        #region SubscriptionMapHandling
        /// <summary>
        /// Removes an unconfirmed subscription.
        /// </summary>
        /// <param name="id">The subscription id.</param>
        /// <returns>Returns the subscription object if it was found.</returns>
        private SubscriptionState RemoveUnconfirmedSubscription(int id)
        {
            SubscriptionState sub;
            lock (this)
            {
                if (!unconfirmedRequests.Remove(id, out sub))
                {
                    Debug.LogError( $"No unconfirmed subscription found with ID:{id}");
                }
            }
            return sub;
        }

        /// <summary>
        /// Removes a given subscription object from the map and notifies the object of the unsubscription.
        /// </summary>
        /// <param name="id">The subscription id.</param>
        /// <param name="shouldNotify">Whether or not to notify that the subscription was removed.</param>
        private void RemoveSubscription(int id, bool shouldNotify)
        {
            SubscriptionState sub;
            lock (this)
            {
                if (!confirmedSubscriptions.Remove(id, out sub))
                {
                    Debug.LogError($"No subscription found with ID:{id}");
                }
            }
            if (shouldNotify)
            {
                sub?.ChangeState(SubscriptionStatus.Unsubscribed);
            }
        }

        /// <summary>
        /// Confirms a given subcription based on the internal subscription id and the newly received external id.
        /// Moves the subcription state object from the unconfirmed map to the confirmed map.
        /// </summary>
        /// <param name="internalId"></param>
        /// <param name="resultId"></param>
        private void ConfirmSubscription(int internalId, int resultId)
        {
            SubscriptionState sub;
            lock (this)
            {
                if (unconfirmedRequests.Remove(internalId, out sub))
                {
                    sub.SubscriptionId = resultId;
                    confirmedSubscriptions.Add(resultId, sub);
                }
            }

            sub?.ChangeState(SubscriptionStatus.Subscribed);
        }

        /// <summary>
        /// Adds a new subscription state object into the unconfirmed subscriptions map.
        /// </summary>
        /// <param name="subscription">The subcription to add.</param>
        /// <param name="internalId">The internally generated id of the subscription.</param>
        private void AddSubscription(SubscriptionState subscription, int internalId)
        {
            lock (this)
            {
                unconfirmedRequests.Add(internalId, subscription);
            }
        }

        /// <summary>
        /// Safely retrieves a subscription state object from a given subscription id.
        /// </summary>
        /// <param name="subscriptionId">The subscription id.</param>
        /// <returns>The subscription state object.</returns>
        private SubscriptionState RetrieveSubscription(int subscriptionId)
        {
            lock (this)
            {
                return confirmedSubscriptions[subscriptionId];
            }
        }
        #endregion
        /// <summary>
        /// Handles a notification message and finishes parsing the contents.
        /// </summary>
        /// <param name="reader">The current JsonReader being used to parse the message.</param>
        /// <param name="method">The method parameter already parsed within the message.</param>
        /// <param name="subscriptionId">The subscriptionId for this message.</param>
        private void HandleDataMessage(string message, string method, int subscriptionId)
        {
            var opts = new JsonSerializerSettings
            {
                ContractResolver  = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                },
                Formatting = Formatting.Indented
            };
            var sub = RetrieveSubscription(subscriptionId);

            var response = JsonConvert.DeserializeObject<JsonSubscriptionResponse>(message, opts);
            
            object result = null;

            switch (method)
            {
                case "accountNotification":
                    {
                        if (sub.Channel == SubscriptionChannel.TokenAccount)
                        {
                            //var newReader = new Utf8JsonReader()
                            var tokenAccNotification = response.Params.ToObject<JsonRpcStreamResponse<ResponseValue<TokenAccountInfo>>>();
                            result = tokenAccNotification.Result;
                        }
                        else
                        {
                            var accNotification = response.Params.ToObject<JsonRpcStreamResponse<ResponseValue<AccountInfo>>>();
                            result = accNotification.Result;
                        }
                        break;
                    }
                case "logsNotification":
                    var logsNotification = response.Params.ToObject<JsonRpcStreamResponse<ResponseValue<LogInfo>>>();
                    result = logsNotification.Result;
                    break;
                case "programNotification":
                    var programNotification = response.Params.ToObject<JsonRpcStreamResponse<ResponseValue<AccountKeyPair>>>();
                    result = programNotification.Result; 
                    break;
                case "signatureNotification":
                    var signatureNotification = response.Params.ToObject<JsonRpcStreamResponse<ResponseValue<ErrorResult>>>();
                    Debug.Log("!!! result !!! ->" + JsonConvert.SerializeObject(signatureNotification));
                    result = signatureNotification.Result;
                    RemoveSubscription(signatureNotification.Subscription, true);
                    break;
                case "slotNotification":
                    var slotNotification = response.Params.ToObject<JsonRpcStreamResponse<SlotInfo>>();
                    result = slotNotification.Result;
                    break;
                case "rootNotification":
                    var rootNotification = response.Params.ToObject<JsonRpcStreamResponse<int>>();
                    result = rootNotification.Result;
                    break;
            }
            
            sub.HandleData(result);
        }

        #region AccountInfo
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeAccountInfoAsync(string, Action{SubscriptionState, ResponseValue{AccountInfo}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeAccountInfoAsync(string pubkey, Action<SubscriptionState, ResponseValue<AccountInfo>> callback, Commitment commitment = Commitment.Finalized)

        {
            var parameters = new List<object> { pubkey };
            var configParams = new Dictionary<string, object> { { "encoding", "base64" } };

            if (commitment != Commitment.Finalized)
            {
                configParams.Add("commitment", commitment);
            }

            parameters.Add(configParams);

            var sub = new SubscriptionState<ResponseValue<AccountInfo>>(this, SubscriptionChannel.Account, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "accountSubscribe", parameters);

            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeAccountInfo(string, Action{SubscriptionState, ResponseValue{AccountInfo}}, Commitment)"/>
        public SubscriptionState SubscribeAccountInfo(string pubkey, Action<SubscriptionState, ResponseValue<AccountInfo>> callback, Commitment commitment = Commitment.Finalized)
            => SubscribeAccountInfoAsync(pubkey, callback, commitment).Result;
        #endregion

        #region TokenAccount
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeTokenAccountAsync(string, Action{SubscriptionState, ResponseValue{TokenAccountInfo}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeTokenAccountAsync(string pubkey, Action<SubscriptionState, ResponseValue<TokenAccountInfo>> callback, Commitment commitment = Commitment.Finalized)

        {
            var parameters = new List<object> { pubkey };
            var configParams = new Dictionary<string, object> { { "encoding", "jsonParsed" } };

            if (commitment != Commitment.Finalized)
            {
                configParams.Add("commitment", commitment);
            }

            parameters.Add(configParams);

            var sub = new SubscriptionState<ResponseValue<TokenAccountInfo>>(this, SubscriptionChannel.TokenAccount, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "accountSubscribe", parameters);

            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeTokenAccount(string, Action{SubscriptionState, ResponseValue{TokenAccountInfo}}, Commitment)"/>
        public SubscriptionState SubscribeTokenAccount(string pubkey, Action<SubscriptionState, ResponseValue<TokenAccountInfo>> callback, Commitment commitment = Commitment.Finalized)
            => SubscribeTokenAccountAsync(pubkey, callback, commitment).Result;
        #endregion

        #region Logs
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeLogInfoAsync(string, Action{SubscriptionState, ResponseValue{LogInfo}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeLogInfoAsync(string pubkey, Action<SubscriptionState, ResponseValue<LogInfo>> callback, Commitment commitment = Commitment.Finalized)
        {
            var parameters = new List<object> { new Dictionary<string, object> { { "mentions", new List<string> { pubkey } } } };

            if (commitment != Commitment.Finalized)
            {
                var configParams = new Dictionary<string, Commitment> { { "commitment", commitment } };
                parameters.Add(configParams);
            }

            var sub = new SubscriptionState<ResponseValue<LogInfo>>(this, SubscriptionChannel.Logs, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "logsSubscribe", parameters);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeLogInfo(string, Action{SubscriptionState, ResponseValue{LogInfo}}, Commitment)"/>
        public SubscriptionState SubscribeLogInfo(string pubkey, Action<SubscriptionState, ResponseValue<LogInfo>> callback, Commitment commitment = Commitment.Finalized)
            => SubscribeLogInfoAsync(pubkey, callback, commitment).Result;

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeLogInfoAsync(LogsSubscriptionType, Action{SubscriptionState, ResponseValue{LogInfo}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeLogInfoAsync(LogsSubscriptionType subscriptionType, Action<SubscriptionState, ResponseValue<LogInfo>> callback, Commitment commitment = Commitment.Finalized)
        {
            var parameters = new List<object> { subscriptionType };

            if (commitment != Commitment.Finalized)
            {
                var configParams = new Dictionary<string, Commitment> { { "commitment", commitment } };
                parameters.Add(configParams);
            }

            var sub = new SubscriptionState<ResponseValue<LogInfo>>(this, SubscriptionChannel.Logs, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "logsSubscribe", parameters);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeLogInfo(LogsSubscriptionType, Action{SubscriptionState, ResponseValue{LogInfo}}, Commitment)"/>
        public SubscriptionState SubscribeLogInfo(LogsSubscriptionType subscriptionType, Action<SubscriptionState, ResponseValue<LogInfo>> callback, Commitment commitment = Commitment.Finalized)
            => SubscribeLogInfoAsync(subscriptionType, callback, commitment).Result;
        #endregion

        #region Signature
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeSignatureAsync(string, Action{SubscriptionState, ResponseValue{ErrorResult}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeSignatureAsync(string transactionSignature, Action<SubscriptionState, ResponseValue<ErrorResult>> callback, Commitment commitment = Commitment.Finalized)
        {
            var parameters = new List<object> { transactionSignature };

            if (commitment != Commitment.Finalized)
            {
                var configParams = new Dictionary<string, Commitment> { { "commitment", commitment } };
                parameters.Add(configParams);
            }

            var sub = new SubscriptionState<ResponseValue<ErrorResult>>(this, SubscriptionChannel.Signature, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "signatureSubscribe", parameters);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeSignature(string, Action{SubscriptionState, ResponseValue{ErrorResult}}, Commitment)"/>
        public SubscriptionState SubscribeSignature(string transactionSignature, Action<SubscriptionState, ResponseValue<ErrorResult>> callback, Commitment commitment = Commitment.Finalized)
            => SubscribeSignatureAsync(transactionSignature, callback, commitment).Result;
        #endregion

        #region Program
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeProgramAsync(string, Action{SubscriptionState, ResponseValue{AccountKeyPair}}, Commitment)"/>
        public async Task<SubscriptionState> SubscribeProgramAsync(string programPubkey, Action<SubscriptionState, 
            ResponseValue<AccountKeyPair>> callback, Commitment commitment = Commitment.Finalized, int? dataSize = null, 
            IList<MemCmp> memCmpList = null)
        {
            List<object> filters = Parameters.Create(ConfigObject.Create(KeyValue.Create("dataSize", dataSize)));
            if (memCmpList != null)
            {
                filters ??= new List<object>();
                filters.AddRange(memCmpList.Select(filter => ConfigObject.Create(KeyValue.Create("memcmp",
                    ConfigObject.Create(KeyValue.Create("offset", filter.Offset),
                        KeyValue.Create("bytes", filter.Bytes))))));
            }
            
            List<object> parameters = Parameters.Create(
                programPubkey,
                ConfigObject.Create(
                    KeyValue.Create("encoding", "base64"),
                    KeyValue.Create("filters", filters),
                    commitment != Commitment.Finalized ? KeyValue.Create("commitment", commitment) : null));

            var sub = new SubscriptionState<ResponseValue<AccountKeyPair>>(this, SubscriptionChannel.Program, callback, parameters);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "programSubscribe", parameters);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeProgram(string, Action{SubscriptionState, ResponseValue{AccountKeyPair}}, Commitment)"/>
        public SubscriptionState SubscribeProgram(string programPubkey, Action<SubscriptionState, ResponseValue<AccountKeyPair>> callback, 
            Commitment commitment = Commitment.Finalized, int? dataSize = null, IList<MemCmp> memCmpList = null)
            => SubscribeProgramAsync(programPubkey, callback, commitment, dataSize, memCmpList).Result;
        #endregion

        #region SlotInfo
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeSlotInfoAsync(Action{SubscriptionState, SlotInfo})"/>
        public async Task<SubscriptionState> SubscribeSlotInfoAsync(Action<SubscriptionState, SlotInfo> callback)
        {
            var sub = new SubscriptionState<SlotInfo>(this, SubscriptionChannel.Slot, callback);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "slotSubscribe", null);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeSlotInfo(Action{SubscriptionState, SlotInfo})"/>
        public SubscriptionState SubscribeSlotInfo(Action<SubscriptionState, SlotInfo> callback)
            => SubscribeSlotInfoAsync(callback).Result;
        #endregion

        #region Root
        /// <inheritdoc cref="IStreamingRpcClient.SubscribeRootAsync(Action{SubscriptionState, int})"/>
        public async Task<SubscriptionState> SubscribeRootAsync(Action<SubscriptionState, int> callback)
        {
            var sub = new SubscriptionState<int>(this, SubscriptionChannel.Root, callback);

            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), "rootSubscribe", null);
            return await Subscribe(sub, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.SubscribeRoot(Action{SubscriptionState, int})"/>
        public SubscriptionState SubscribeRoot(Action<SubscriptionState, int> callback)
            => SubscribeRootAsync(callback).Result;
        #endregion

        /// <summary>
        /// Internal subscribe function, finishes the serialization and sends the message payload.
        /// </summary>
        /// <param name="sub">The subscription state object.</param>
        /// <param name="msg">The message to be serialized and sent.</param>
        /// <returns>A task representing the state of the asynchronous operation-</returns>
        private async Task<SubscriptionState> Subscribe(SubscriptionState sub, JsonRpcRequest msg)
        {
            var encodingConverter = new EncodingConverter();
            var enumConverter = new StringEnumConverter(new CamelCaseNamingStrategy());
            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };

            var opts = new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                Formatting = Formatting.Indented,
                Converters = new List<JsonConverter> { encodingConverter, enumConverter } 
            };

            var jsonString = JsonConvert.SerializeObject(msg, opts);
            
            Debug.Log($"[Sending]{jsonString}");

            var json = Encoding.UTF8.GetBytes(jsonString);
            ArraySegment<byte> mem = new ArraySegment<byte>(json);

            try
            {
                await ClientSocket.SendAsync(mem, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                AddSubscription(sub, msg.Id);
            }
            catch (Exception e)
            {
                sub.ChangeState(SubscriptionStatus.ErrorSubscribing, e.Message);
                Debug.Log( $"Unable to send message");
                Debug.LogException(e);
            }

            return sub;
        }

        private string GetUnsubscribeMethodName(SubscriptionChannel channel) => channel switch
        {
            SubscriptionChannel.Account => "accountUnsubscribe",
            SubscriptionChannel.Logs => "logsUnsubscribe",
            SubscriptionChannel.Program => "programUnsubscribe",
            SubscriptionChannel.Root => "rootUnsubscribe",
            SubscriptionChannel.Signature => "signatureUnsubscribe",
            SubscriptionChannel.Slot => "slotUnsubscribe",
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "invalid message type")
        };

        /// <inheritdoc cref="IStreamingRpcClient.UnsubscribeAsync(SubscriptionState)"/>
        public async Task UnsubscribeAsync(SubscriptionState subscription)
        {
            var msg = new JsonRpcRequest(_idGenerator.GetNextId(), GetUnsubscribeMethodName(subscription.Channel), new List<object> { subscription.SubscriptionId });

            await Subscribe(subscription, msg).ConfigureAwait(false);
        }

        /// <inheritdoc cref="IStreamingRpcClient.Unsubscribe(SubscriptionState)"/>
        public void Unsubscribe(SubscriptionState subscription) => UnsubscribeAsync(subscription).Wait();
    }
}