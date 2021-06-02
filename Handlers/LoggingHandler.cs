#region Using
using Newtonsoft.Json;
using Plugable.io;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using uhttpsharp;
using uhttpsharp.Exceptions;
using WebProt.WebHttp.Provider.Extensions;
using WebProt.WebHttp.Provider.Helpers;
#endregion

namespace WebProt.WebHttp.Provider.Handlers
{
    public class LoggingHandler : IHttpRequestHandler
    {
        #region Variables
        public static Usage CurrentUsage = new Usage();
        private const int padding = 75;
        private PluginsManager server;
        #endregion

        #region Constructor
        public LoggingHandler(PluginsManager server)
        {
            this.server = server;
        }
        #endregion

        #region Error
        private void Error(Stopwatch stopWatch, IHttpContext context, Exception e)
        {
            CurrentUsage.Sync();
            var currentCpuUsage = CurrentUsage.GetCurrentCpuUsage();
            var availableRAM = CurrentUsage.GetAvailableRAM();
            TimeSpan ts = stopWatch.Elapsed;
            Extension.Error(string.Format("Error|{0}|{1}|{2}|{3:D2}:{4:D2}:{5:D2}:{6:D2}:{7:D3}|", context.RemoteEndPoint, currentCpuUsage, availableRAM, ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds).PadRight(padding, '-'));//.ConfigureAwait(false);

            if (e != null)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append(e.Message);
                stringBuilder.Append(Environment.NewLine);
                stringBuilder.Append(e.StackTrace);

                Extension.Error(stringBuilder.ToString());//.ConfigureAwait(false);

                if (server != null)
                {
                    server.MessageProvider(WebExtension.WebProtNotifierProvider, new
                    {
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture),
                        MessageType = "Error",
                        RemoteEndPoint = context.RemoteEndPoint.ToString(),
                        CurrentCpuUsage = currentCpuUsage,
                        AvailableRAM = availableRAM,
                        Days = ts.Days,
                        Hours = ts.Hours,
                        Minutes = ts.Minutes,
                        Seconds = ts.Seconds,
                        Milliseconds = ts.Milliseconds,
                        Message = stringBuilder
                    });
                }
            }
        }
        #endregion

        #region Handle
        public async Task Handle(IHttpContext context, Func<Task> next)
        {
            var stopWatch = Stopwatch.StartNew();
            CurrentUsage.Sync();
            //Console.WriteLine();

            var currentCpuUsage = CurrentUsage.GetCurrentCpuUsage();
            var availableRAM = CurrentUsage.GetAvailableRAM();
            Extension.Log(string.Format("Request |{0}|{1}|{2}|", context.RemoteEndPoint, currentCpuUsage, availableRAM).PadRight(padding, '-'));
            Extension.Log(string.Format("Query: {0}", context.Request.Uri));//.ConfigureAwait(false);

            if (server != null)
            {
                server.MessageProvider(WebExtension.WebProtNotifierProvider, new
                {
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture),
                    MessageType = "Request",
                    RemoteEndPoint = context.RemoteEndPoint.ToString(),
                    CurrentCpuUsage = currentCpuUsage,
                    AvailableRAM = availableRAM,
                    Query = context.Request.Uri
                });
            }

            try
            {
                await next().ConfigureAwait(false);

                CurrentUsage.Sync();

                currentCpuUsage = CurrentUsage.GetCurrentCpuUsage();
                availableRAM = CurrentUsage.GetAvailableRAM();
                TimeSpan ts = stopWatch.Elapsed;
                Extension.Log(string.Format("Response|{0}|{1}|{2}|{3:D2}:{4:D2}:{5:D2}:{6:D2}:{7:D3}|", context.RemoteEndPoint, currentCpuUsage, availableRAM, ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds).PadRight(padding, '-'));//.ConfigureAwait(false);

                if (server != null)
                {
                    server.MessageProvider(WebExtension.WebProtNotifierProvider, new
                    {
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture),
                        MessageType = "Response",
                        RemoteEndPoint = context.RemoteEndPoint.ToString(),
                        CurrentCpuUsage = currentCpuUsage,
                        AvailableRAM = availableRAM,
                        Days = ts.Days,
                        Hours = ts.Hours,
                        Minutes = ts.Minutes,
                        Seconds = ts.Seconds,
                        Milliseconds = ts.Milliseconds
                    });
                }
            }
            catch (HttpException e)
            {
                Error(stopWatch, context, e);

                context.Response = HttpResponse.CreateRawMessage(e.ResponseCode,
                    JsonConvert.SerializeObject(new
                    {
                        Status = "Failed",
                        Data = "Error while handling your request",
                        e.Message
                    })
                    , "application/json; charset=utf-8", false);
            }
            catch (Exception e)
            {
                Error(stopWatch, context, e);//.ConfigureAwait(false);

                context.Response = HttpResponse.CreateRawMessage(HttpResponseCode.InternalServerError,
                    JsonConvert.SerializeObject(new
                    {
                        Status = "Failed",
                        Data = "Error while handling your request",
                        e.Message
                    })
                    , "application/json; charset=utf-8", false);
            }
        }
        #endregion
    }
}
