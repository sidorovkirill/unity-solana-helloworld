using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Phantom
{
    public class DeepLinkProtocol
    {
        private readonly Dictionary<string, TaskCompletionSource<DeepLinkData>> _requests;

        private readonly string _targetUrl = "https://phantom.app/ul/v1";

        public DeepLinkProtocol()
        {
            _requests = new Dictionary<string, TaskCompletionSource<DeepLinkData>>();
        }
        
        public DeepLinkProtocol(string targetUrl) : this()
        {
            _targetUrl = targetUrl;
        }
        
        public void Init()
        {
            Application.deepLinkActivated += OnDeepLinkActivated;
        }

        public Task<DeepLinkData> Send(DeepLinkData payload)
        {
            var requestCompletionSource = new TaskCompletionSource<DeepLinkData>();
            _requests.Add(payload.Method, requestCompletionSource);

            var url = Serialize(payload);
            Application.OpenURL(url);
            
            return requestCompletionSource.Task;
        }

        private void OnDeepLinkActivated(string url)
        {
            var data = Deserialize(url);
            if (_requests.ContainsKey(data.Method))
            {
                _requests[data.Method].TrySetResult(data);
                _requests.Remove(data.Method);
            }
        }

        private string Serialize(DeepLinkData data)
        {
            var url = $"{_targetUrl}/{data.Method}";
            if (data.Params.Count > 0)
            {
                var query = new List<string>();
                foreach (var param in data.Params)
                {
                    query.Add($"{param.Key}={param.Value}");
                }
                url += "?"+String.Join("&", query.ToArray());   
            }

            return url;
        }
        
        private static DeepLinkData Deserialize(string url)
        {
            var res = new DeepLinkData();
            
            var method = string.Empty;
            
            var body = url.Split("//")[1];
            if (!body.Contains('?'))
            {
                res.Method = body;
                return res;
            }

            var parts = body.Split('?');
            method = parts[0];
            res.Method = method;

            var paramVals = parts[1].Split('&');
            foreach (var paramVal in paramVals)
            {
                var pts = paramVal.Split('=');
                res.Params.Add(pts[0], pts[1]);
            }
            
            return res;
        }
    }

}