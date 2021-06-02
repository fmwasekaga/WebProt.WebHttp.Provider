using Newtonsoft.Json;
using Plugable.io;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using uhttpsharp;
using uhttpsharp.Headers;
using WebProt.WebHttp.Provider.Extensions;
using WebProt.WebHttp.Provider.Helpers;

namespace WebProt.WebHttp.Provider.Handlers
{
    public class APIHandler : IHttpRequestHandler
    {
        private Router _router;
        private TimeSpan _timeout;

        public APIHandler(TimeSpan timeout)
        {
            _router = new Router();
            _timeout = timeout;
        }

        public APIHandler With(string path, Func<IHttpContext, Dictionary<string, string>, Func<Task>, Task> fn)
        {
            _router.Add(path, fn);
            return this;
        }

        public APIHandler Plugins(List<IProtocolPlugin> plugins, string[] args, PluginsManager parent)
        {
            if (plugins != null)
            {
                foreach (var plugin in plugins)
                {
                    if (plugin != null)
                        plugin.Initialize(args, parent, _router);
                }
            }
            return this;
        }

        public Task Handle(IHttpContext context, Func<Task> nextHandler)
        {
            Func<IHttpContext, Dictionary<string, string>, Func<Task>, Task> fnRoute;
            Dictionary<string, string> data;
            if (_router.TryGetValue(context.Request.Uri.ToString(), out fnRoute, out data)
                    || _router.TryGetValue("*", out fnRoute, out data))
            {
                return fnRoute(context, data, nextHandler).WithTimeout(_timeout);
            }
            else
            {
                context.Response = HttpResponse.CreateRawMessage(HttpResponseCode.NotFound, JsonConvert.SerializeObject(new
                {
                    Status = "Failed",
                    Data = "Unknown request"
                })
                , "application/json; charset=utf-8", context.Request.Headers.KeepAliveConnection());
                return Task.Factory.GetCompleted();
            }
        }
    }
}
