﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Net.Sockets;
using System.IO.Compression;

namespace StaticHtml
{
    /// <summary>
    /// 依靠发送Http请求生成HttpRequest的Html内容，理论上适用与所有站点包括mvc等经过urlrewrite的站点  系统默认
    /// </summary>
    public class WebRequestGenHtml : IGenHtml
    {
        #region IGenHtml 成员

        const String CONNECTION_CLOSE = "Connection: close\r\n";
        const String HEADERFORMAT = "{0}: {1}\r\n";
        const int TIMEOUT=6000;


        public Stream GenHTML(HttpRequest req)
        {
            using (var client = new TcpClient())
            {
                client.ReceiveTimeout = TIMEOUT;
                client.Connect(req.Url.Host, req.Url.Port);
                using (var stream = client.GetStream())
                {
                    OutHeader(req, stream);
                    return HttpParseHelp.ToGzip(stream);
                }
            }

        }

        
        /// <summary>
        /// 发送所有请求头到服务器
        /// </summary>
        /// <param name="req"></param>
        /// <param name="_stream"></param>
        private void OutHeader(HttpRequest req, Stream _stream)
        {
            var _out = new StreamWriter(_stream);
            _out.Write(String.Format("{0} {1} {2}\r\n", req.HttpMethod, req.RawUrl, req.ServerVariables["SERVER_PROTOCOL"]));//req.ServerVariables["SERVER_PROTOCOL"]
            _out.Write(String.Format(HEADERFORMAT, "HOST", req.Url.Authority));
            _out.Write(String.Format(HEADERFORMAT, HtmlStaticCore.SKIPMARKHEAD, 1));
            foreach (String key in req.Headers.Keys)
            {
                var lowerKey = key.ToLower();
                if (lowerKey != "connection" && lowerKey != "host")
                {
                    var val = req.Headers[key];
                    _out.Write(String.Format(HEADERFORMAT, key, val));
                }
            }
            _out.Write(CONNECTION_CLOSE);
            _out.Write("\r\n");
            //_out.Write("\r\n");
            _out.Flush();
        }

        /* asp.net bug。当cookie value里面有逗号会出错
        public string GenHTML(HttpRequest req)
        {
            var url = req.Url.ToString();
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add(HtmlStaticCore.SKIPMARKHEAD, "1");
            request.CookieContainer = new CookieContainer();
            for (var i = 0; i < req.Cookies.Count; i++)
            {
                System.Web.HttpCookie item = req.Cookies[i];
                var cookie = new Cookie(item.Name, item.Value, item.Path);
                cookie.Expires = item.Expires;
                request.CookieContainer.Add(req.Url,cookie);
            }
            using (StreamReader reader = new StreamReader(request.GetResponse().GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }*/

        #endregion
    }
}
