#region Using
using CommandLineParser.Arguments;
using CommandLineParser.Exceptions;
using Logging.io;
using Newtonsoft.Json;
using Plugable.io;
using Plugable.io.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using uhttpsharp;
using uhttpsharp.Handlers.Compression;
using uhttpsharp.Listeners;
using uhttpsharp.RequestProviders;
using WebProt.WebHttp.Provider.Extensions;
using WebProt.WebHttp.Provider.Handlers;
#endregion

namespace WebProt.WebHttp.Provider
{
    public class WebExtension : IPlugable, IProtocolProvider
    {
        #region Variables
        private PluginsManager server;
        private ILog Logger = LogProvider.GetCurrentClassLogger();
        private IList<IRequestProvider> _providers;
        private bool _isActive;

        public const string WebProtNotifierProvider = "WebProt.WebSocket.Provider";
        #endregion

        #region Initialize
        public void Initialize(string[] args, PluginsManager server)
        {
            _providers = new List<IRequestProvider>();

            if (args.Length > 0)
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
                this.server = server;

                string[] parsedArgs = null;

                try {
                    ValueArgument<string> argument = new ValueArgument<string>('p', "hp", "Arguements for this plugin");

                    var _parser = new CommandLineParser.CommandLineParser(args);
                    _parser.Arguments.Add(argument);
                    _parser.ParseCommandLine();

                    parsedArgs = argument.Value.ToArguments();
                }
                catch (CommandLineException e)
                {
                    Extension.Error(e.Message);
                    if (server != null)
                    {
                        server.MessageProvider(WebExtension.WebProtNotifierProvider, new
                        {
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture),
                            MessageType = "Error",
                            Message = e.Message
                        });
                    }
                }

                if (parsedArgs != null)
                {
                    var parser = new CommandLineParser.CommandLineParser(parsedArgs);

                    ValueArgument<int> nonSecuredArgument = new ValueArgument<int>('h', "http", "The http port");
                    nonSecuredArgument.AllowMultiple = true;

                    ValueArgument<int> SecuredArgument = new ValueArgument<int>('s', "https", "The https port");
                    SecuredArgument.AllowMultiple = true;

                    parser.Arguments.Add(nonSecuredArgument);
                    parser.Arguments.Add(SecuredArgument);

                    try { parser.ParseCommandLine(); }
                    catch (CommandLineException e)
                    {
                        Extension.Error(e.Message);
                        if (server != null)
                        {
                            server.MessageProvider(WebExtension.WebProtNotifierProvider, new
                            {
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture),
                                MessageType = "Error",
                                Message = e.Message
                            });
                        }
                        parser.ShowUsage();
                    }

                    if ((nonSecuredArgument != null && nonSecuredArgument.Values.Any()) ||
                        (SecuredArgument != null && SecuredArgument.Values.Any()))
                    {
                        server.LogEvent += (s, arg) =>
                        {
                            if (arg.IsError) Extension.Error(arg.Message);
                            else Extension.Log(arg.Message);
                        };

                        System.Security.Cryptography.X509Certificates.X509Certificate2 serverCertificate = null;

                        var shutdowntimer = new System.Timers.Timer(3000);
                        shutdowntimer.AutoReset = false;
                        shutdowntimer.Elapsed += (s, arg) => { Environment.Exit(0); };

                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                        var provider = new HttpRequestProvider();
                        var plugins = Path.Combine(Environment.CurrentDirectory, "extensions").GetPlugable(getName()).OfType<IProtocolPlugin>().ToList();

                        var hostName = Dns.GetHostName();
                        var hostEntry = Dns.GetHostEntry(hostName);
                        var CurrentIP = hostEntry.AddressList.Where(s => s.IsIPv6LinkLocal == false).FirstOrDefault();

                        if (nonSecuredArgument != null)
                        {
                            foreach (var value in nonSecuredArgument.Values)
                            {
                                provider.Use(new TcpListenerAdapter(new TcpListener(IPAddress.Any, value)));
                                if (CurrentIP != null) Logger.InfoFormat(getName() + " listening on http://{0}:{1}", CurrentIP, value);
                                else Logger.InfoFormat(getName() + " listening (http) on {0}", value);
                            }
                        }

                        if (SecuredArgument != null)
                        {
                            var certFile = Path.Combine("certificates", "server.pfx");
                            var certPass = "admin123$";

                            if (File.Exists(certFile))
                            {
                                foreach (var value in SecuredArgument.Values)
                                {
                                    if (serverCertificate == null)
                                        serverCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certFile, certPass);

                                    provider.Use(new ListenerSslDecorator(new TcpListenerAdapter(new TcpListener(IPAddress.Any, value)), serverCertificate));
                                    if (CurrentIP != null) Logger.InfoFormat(getName() + " listening on https://{0}:{1}", CurrentIP, value);
                                    else Logger.InfoFormat(getName() + " listening (https) on {0}", value);
                                }
                            }
                            else Logger.InfoFormat(getName() + " cant create https : {0}", "missing certificate");
                        }

                        provider
                            .Uses(new CompressionHandler(DeflateCompressor.Default, GZipCompressor.Default))
                            .Uses(new LoggingHandler(server))
                            .Uses(new APIHandler(TimeSpan.FromMinutes(5))
                                .With("*", (ctx, data, handler) =>
                                {
                                    return ctx.OutputUtf8(JsonConvert.SerializeObject(new
                                    {
                                        Status = "OK",
                                        Version = "1",
                                        Data = "Kagaconnect API",
                                        Request = "*"
                                    }), "application/json");
                                })
                                .With(@"/shutdown/{email}", (ctx, data, handler) =>
                                {
                                    var email = data["email"];
                                    if (!string.IsNullOrEmpty(email))
                                    {
                                        shutdowntimer.Start();

                                        return ctx.OutputUtf8(JsonConvert.SerializeObject(new
                                        {
                                            Status = "Successful",
                                            Data = "Shutting down in 3 seconds",
                                            Request = @"/shutdown/email"
                                        }), "application /json");
                                    }
                                    else
                                    {
                                        return ctx.OutputUtf8(JsonConvert.SerializeObject(new
                                        {
                                            Status = "Error",
                                            Data = "invalid email"
                                        }), "application/json");
                                    }
                                })
                                .Plugins(plugins, args, server)
                             );

                        _providers.Add(provider);
                    }
                }
            }
            else Logger.InfoFormat(getName()+" not started, missing startup arguments.");
        }
        #endregion

        #region Start
        public void Start()
        {
            _isActive = true;

            foreach (var provider in _providers)
            {
                foreach (var listener in provider.getListeners())
                {
                    IListener tempListener = listener;

                    Task.Factory.StartNew(() => Listen(tempListener, provider), TaskCreationOptions.LongRunning);
                }
            }
        }
        #endregion

        #region Stop
        public void Stop()
        {
            _isActive = false;
        }
        #endregion

        #region Listen
        private async void Listen(IListener _listener, IRequestProvider _requestProvider)
        {
            var aggregatedHandler = _requestProvider.getRequestHandlers().Aggregate();

            while (_isActive)
            {
                try
                {
                    var client = new HttpClientHandler(await _listener.GetClient().ConfigureAwait(false), aggregatedHandler, _requestProvider);
                    client.ClientEvent += (s, args) =>
                    {
                        var _client = (HttpClientHandler)s;
                        if (_client != null) _client.Dispose();
                        _client = null;
                    };
                    client.LogEvent += (s, args) =>
                    {
                        server?.EventLog(this, new Plugable.io.Events.LogEventArgs(args.Message, args.IsError));
                    };
                }
                catch (Exception e)
                {
                    server?.EventLog(this, new Plugable.io.Events.LogEventArgs("Error while getting client. " + e.Message + Environment.NewLine + e.StackTrace, true));
                }
            }

            server?.EventLog(this, new Plugable.io.Events.LogEventArgs("Embedded uhttpserver stopped."));
        }
        #endregion

        #region getName
        public string getName()
        {
            return GetType().Assembly.GetName().Name;
        }
        #endregion

        #region getVersion
        public string getVersion()
        {
            return GetType().Assembly.GetName().Version.ToString();
        }
        #endregion

        #region Message
        public void Message(dynamic message)
        {

        }
        #endregion

        #region ResolveAssembly
        public Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var assembly = (args.Name.Contains(","))
                    ? args.Name.Substring(0, args.Name.IndexOf(','))
                    : args.Name;

            var directory = Path.Combine(Environment.CurrentDirectory, "extensions");
            var plugin = getName() + "_" + getVersion() + ".zip";

            return Path.Combine(directory, plugin).GetAssemblyFromPlugin(assembly);
        }
        #endregion
    }
}
