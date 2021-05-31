using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Soap;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using uhttpsharp;
using uhttpsharp.Headers;

namespace WebProt.WebHttp.Provider.Extensions
{
    public static class Core
    {
        #region OutputUtf8
        public static Task OutputUtf8(this IHttpContext ctx, string html, string contentType = "text/html", Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            return OutputBinary(ctx, encoding.GetBytes(html), $"{contentType}; charset={encoding.WebName}");
        }

        public static Task OutputText(this IHttpContext ctx, string text, string contentType = "text/plain", Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.Default;
            return OutputBinary(ctx, encoding.GetBytes(text), $"{contentType}; charset={encoding.WebName}");
        }

        public static Task OutputBinary(this IHttpContext ctx, byte[] content, string contentType = "application/octet-stream")
        {
            //ctx.Response.AddHeader("Access-Control-Allow-Origin","*");
            //ctx.Response.ContentType = contentType;
            //ctx.Response.ContentLength64 = content.Length;
            //ctx.Response.OutputStream.Write(content, 0, content.Length);
            ctx.Response = HttpResponse.CreateRawMessage(HttpResponseCode.Ok, content, contentType, ctx.Request.Headers.KeepAliveConnection());
            ((ListHttpHeaders)ctx.Response.Headers).Add("Access-Control-Allow-Origin", "*");
            ((ListHttpHeaders)ctx.Response.Headers).Add("Access-Control-Allow-Headers", "*");
            ((ListHttpHeaders)ctx.Response.Headers).Add("Access-Control-Allow-Methods", "*");
            ((ListHttpHeaders)ctx.Response.Headers).Add("Content-Length", content.Length.ToString());
            return Task.Factory.GetCompleted();
        }
        #endregion

        #region Beautify
        public static string Beautify(this string xml)
        {
            var doc = new XmlDocument();
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace
            };

            doc.LoadXml(xml);
            using (XmlWriter writer = XmlWriter.Create(sb, settings))
            {
                doc.Save(writer);
            }
            return sb.ToString();
        }
        #endregion

        #region FromSoap
        public static T FromSoap<T>(string SOAP)
        {
            using (MemoryStream Stream = new MemoryStream(UTF8Encoding.UTF8.GetBytes(SOAP)))
            {
                SoapFormatter Formatter = new SoapFormatter();
                return (T)Formatter.Deserialize(Stream);
            }
        }
        #endregion

        #region ToSoap
        public static string ToSoap(object Object)
        {
            if (Object == null) throw new ArgumentException("Object can not be null");

            using (MemoryStream Stream = new MemoryStream())
            {
                SoapFormatter Serializer = new SoapFormatter();
                Serializer.Serialize(Stream, Object);
                Stream.Flush();
                return UTF8Encoding.UTF8.GetString(Stream.GetBuffer(), 0, (int)Stream.Position);
            }
        }
        #endregion

        #region WithTimeout
        /// <summary>Creates a new Task that mirrors the supplied task but that will be canceled after the specified timeout.</summary>
        /// <typeparam name="TResult">Specifies the type of data contained in the task.</typeparam>
        /// <param name="task">The task.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns>The new Task that may time out.</returns>
        public static Task WithTimeout(this Task task, TimeSpan timeout)
        {
            var result = new TaskCompletionSource<object>(task.AsyncState);
            var timer = new System.Threading.Timer(state =>
            {
                ((TaskCompletionSource<object>)state).TrySetCanceled();
            }
            , result, timeout, TimeSpan.FromMilliseconds(-1));

            task.ContinueWith(t =>
            {
                timer.Dispose();
                result.TrySetResult(t);
            }
            , TaskContinuationOptions.ExecuteSynchronously);

            return result.Task;
        }
        #endregion

        #region ToArguments
        public static string[] ToArguments(this string commandLine)
        {
            char[] parmChars = commandLine.ToCharArray();
            bool inQuote = false;
            for (int index = 0; index < parmChars.Length; index++)
            {
                if (parmChars[index] == '"')
                    inQuote = !inQuote;
                if (!inQuote && parmChars[index] == ' ')
                    parmChars[index] = '\n';
            }
            return (new string(parmChars)).Split('\n');
        }
        #endregion
    }
}
