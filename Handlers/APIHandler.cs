using Newtonsoft.Json;
using Plugable.io;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using uhttpsharp;
using uhttpsharp.Headers;
using uhttpsharp.Helpers;
using uhttpsharp.Interfaces;
using WebProt.WebHttp.Provider.Extensions;

namespace WebProt.WebHttp.Provider.Handlers
{
    public class APIHandler : IHttpRequestHandler
    {
        private Router _router;
        private PluginsManager _server;
        private TimeSpan _timeout;

        public APIHandler(PluginsManager server, TimeSpan timeout)
        {
            _router = new Router();
            _timeout = timeout;
            _server = server;
        }

        public APIHandler With(string path, RouteAction fn)
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
                    {
                        var _r = (IRoutable)plugin;
                        if(_r != null)
                        {
                            _r.Initialize(args, _server, _router);
                            foreach (var route in _r.GetRoutes())
                            {
                                _router.Add(route.Key, route.Value);
                            }
                        }                        
                    }
                }
            }
            return this;
        }

        public Task Handle(IHttpContext context, Func<Task> nextHandler)
        {
            RouteAction fnRoute;
            Dictionary<string, string> data;
            if (_router.TryGetValue(context.Request.Uri.ToString(), out fnRoute, out data)
                    || _router.TryGetValue("*", out fnRoute, out data))
            {
                return fnRoute(_router, context, data, nextHandler).WithTimeout(_timeout);
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
