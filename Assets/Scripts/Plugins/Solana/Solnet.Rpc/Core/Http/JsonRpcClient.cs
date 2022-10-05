using Solnet.Rpc.Converters;
using Solnet.Rpc.Messages;
using Solnet.Rpc.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Solnet.Rpc.Core.Http
{
    /// <summary>
    /// Base Rpc client class that abstracts the HttpClient handling.
    /// </summary>
    internal abstract class JsonRpcClient
    {
        /// <summary>
        /// The Json serializer options to be reused between calls.
        /// </summary>
        private readonly JsonSerializerSettings _serializerOptions;

        /// <summary>
        /// The HttpClient.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Rate limiting strategy
        /// </summary>
        private IRateLimiter _rateLimiter;

        /// <inheritdoc cref="IRpcClient.NodeAddress"/>
        public Uri NodeAddress { get; }

        /// <summary>
        /// The internal constructor that setups the client.
        /// </summary>
        /// <param name="url">The url of the RPC server.</param>
        /// <param name="httpClient">The possible HttpClient instance. If null, a new instance will be created.</param>
        /// <param name="rateLimiter">An IRateLimiter instance or null for no rate limiting.</param>
        protected JsonRpcClient(string url, HttpClient httpClient = default, IRateLimiter rateLimiter = null)
        {
            NodeAddress = new Uri(url);
            _httpClient = httpClient ?? new HttpClient { BaseAddress = NodeAddress };
            _rateLimiter = rateLimiter;
            
            var encodingConverter = new EncodingConverter();
            var enumConverter = new StringEnumConverter(new CamelCaseNamingStrategy());
            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };

            _serializerOptions = new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                Formatting = Formatting.Indented,
                Converters = new List<JsonConverter> { encodingConverter, enumConverter } 
            };
        }

        /// <summary>
        /// Sends a given message as a POST method and returns the deserialized message result based on the type parameter.
        /// </summary>
        /// <typeparam name="T">The type of the result to deserialize from json.</typeparam>
        /// <param name="req">The message request.</param>
        /// <returns>A task that represents the asynchronous operation that holds the request result.</returns>
        protected async Task<RequestResult<T>> SendRequest<T>(JsonRpcRequest req)
        {
            var requestJson = JsonConvert.SerializeObject(req, _serializerOptions);

            try
            {
                // pre-flight check with rate limiter if set
                _rateLimiter?.Fire(); 
                
                // logging
                Debug.Log($"Sending request: {requestJson}");

                // create byte buffer to avoid charset=utf-8 in content-type header
                // as this is rejected by some RPC nodes
                var buffer = Encoding.UTF8.GetBytes(requestJson);
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, (string)null)
                {
                    Content = new ByteArrayContent(buffer)
                    {
                        Headers = {
                            { "Content-Type", "application/json"}
                        }
                    }
                };

                // execute POST
                using (var response = await _httpClient.SendAsync(httpReq).ConfigureAwait(false))
                {
                    var result = await HandleResult<T>(req, response).ConfigureAwait(false);
                    result.RawRpcRequest = requestJson;
                    return result;
                }


            }
            catch (HttpRequestException e)
            {
                var result = new RequestResult<T>(System.Net.HttpStatusCode.BadRequest, e.Message);
                result.RawRpcRequest = requestJson;
                Debug.LogError( $"Caught exception: {e.Message}");
                Debug.LogException(e);
                return result;
            }
            catch (Exception e)
            {
                var result = new RequestResult<T>(System.Net.HttpStatusCode.BadRequest, e.Message);
                result.RawRpcRequest = requestJson;
                Debug.LogError( $"Caught exception: {e.Message}");
                Debug.LogException(e);
                return result;
            }


        }

        /// <summary>
        /// Handles the result after sending a request.
        /// </summary>
        /// <typeparam name="T">The type of the result to deserialize from json.</typeparam>
        /// <param name="req">The original message request.</param>
        /// <param name="response">The response obtained from the request.</param>
        /// <returns>A task that represents the asynchronous operation that holds the request result.</returns>
        private async Task<RequestResult<T>> HandleResult<T>(JsonRpcRequest req, HttpResponseMessage response)
        {
            RequestResult<T> result = new RequestResult<T>(response);
            try
            {
                result.RawRpcResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Debug.Log($"Result: {result.RawRpcResponse}");
                var res = JsonConvert.DeserializeObject<JsonRpcResponse<T>>(result.RawRpcResponse, _serializerOptions);

                if (res.Result != null)
                {
                    result.Result = res.Result;
                    result.WasRequestSuccessfullyHandled = true;
                }
                else
                {
                    var errorRes = JsonConvert.DeserializeObject<JsonRpcErrorResponse>(result.RawRpcResponse, _serializerOptions);
                    if (errorRes is { Error: { } })
                    {
                        result.Reason = errorRes.Error.Message;
                        result.ServerErrorCode = errorRes.Error.Code;
                        result.ErrorData = errorRes.Error.Data;
                    }
                    else if(errorRes is { ErrorMessage: { } })
                    {
                        result.Reason = errorRes.ErrorMessage;
                    }
                    else
                    {
                        result.Reason = "Something wrong happened.";
                    }
                }
            }
            catch (JsonException e)
            {
                Debug.LogError( $"Caught exception: {e.Message}");
                Debug.LogException(e);
                result.WasRequestSuccessfullyHandled = false;
                result.Reason = "Unable to parse json.";
            }

            return result;
        }

        /// <summary>
        /// Sends a batch of messages as a POST method and returns a collection of responses.
        /// </summary>
        /// <param name="reqs">The message request.</param>
        /// <returns>A task that represents the asynchronous operation that holds the request result.</returns>
        public async Task<RequestResult<JsonRpcBatchResponse>> SendBatchRequestAsync(JsonRpcBatchRequest reqs)
        {
            if (reqs == null) throw new ArgumentNullException(nameof(reqs));
            if (reqs.Count == 0) throw new ArgumentException("Empty batch");
            var id_for_log = reqs.Min(x => x.Id);
            var requestsJson = JsonConvert.SerializeObject(reqs, _serializerOptions);
            try
            {
                // pre-flight check with rate limiter if set
                _rateLimiter?.Fire(); 
                
                Debug.Log($"Sending request: {requestsJson}");

                // create byte buffer to avoid charset=utf-8 in content-type header
                // as this is rejected by some RPC nodes
                var buffer = Encoding.UTF8.GetBytes(requestsJson);
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, (string)null)
                {
                    Content = new ByteArrayContent(buffer)
                    {
                        Headers = {
                            { "Content-Type", "application/json"}
                        }
                    }
                };

                // execute POST
                using (var response = await _httpClient.SendAsync(httpReq).ConfigureAwait(false))
                {
                    var result = await HandleBatchResult(reqs, response).ConfigureAwait(false);
                    result.RawRpcRequest = requestsJson;
                    return result;
                }

            }
            catch (HttpRequestException e)
            {
                var result = new RequestResult<JsonRpcBatchResponse>(System.Net.HttpStatusCode.BadRequest, e.Message);
                result.RawRpcRequest = requestsJson;
                Debug.LogError( $"Caught exception: {e.Message}");
                Debug.LogException(e);
                return result;
            }
            catch (Exception e)
            {
                var result = new RequestResult<JsonRpcBatchResponse>(System.Net.HttpStatusCode.BadRequest, e.Message);
                result.RawRpcRequest = requestsJson;
                Debug.LogError( $"Caught exception: {e.Message}");
                Debug.LogException(e);
                return result;
            }

        }

        /// <summary>
        /// Handles the result after sending a batch of requests.
        /// Outcome could be a collection of failures due to a single API issue or a mixed bag of 
        /// success and failure depending on the individual request outcomes.
        /// </summary>
        /// <param name="reqs">The original batch of request messages.</param>
        /// <param name="response">The batch of responses obtained from the HTTP request.</param>
        /// <returns>A task that represents the asynchronous operation that holds the request result.</returns>
        private async Task<RequestResult<JsonRpcBatchResponse>> HandleBatchResult(JsonRpcBatchRequest reqs, HttpResponseMessage response)
        {
            var id_for_log = reqs.Min(x => x.Id);
            RequestResult<JsonRpcBatchResponse> result = new RequestResult<JsonRpcBatchResponse>(response);
            try
            {
                result.RawRpcResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Debug.Log($"Result: {result.RawRpcResponse}");
                var res = JsonConvert.DeserializeObject<JsonRpcBatchResponse>(result.RawRpcResponse, _serializerOptions);

                if (res != null)
                {
                    result.Result = res;
                    result.WasRequestSuccessfullyHandled = true;
                }
                else
                {
                    var errorRes = JsonConvert.DeserializeObject<JsonRpcErrorResponse>(result.RawRpcResponse, _serializerOptions);
                    if (errorRes is { Error: { } })
                    {
                        result.Reason = errorRes.Error.Message;
                        result.ServerErrorCode = errorRes.Error.Code;
                        result.ErrorData = errorRes.Error.Data;
                    }
                    else if (errorRes is { ErrorMessage: { } })
                    {
                        result.Reason = errorRes.ErrorMessage;
                    }
                    else
                    {
                        result.Reason = "Something wrong happened.";
                    }
                }
            }
            catch (JsonException e)
            {
                Debug.LogError( $"Caught exception: {e.Message}");
                Debug.LogException(e);
                result.WasRequestSuccessfullyHandled = false;
                result.Reason = "Unable to parse json.";
            }

            return result;
        }

    }

}