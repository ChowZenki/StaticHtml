﻿using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
namespace StaticHtml
{
    /// <summary>
    /// Rule
    /// </summary>
    public class Rule
    {
        /// <summary>
        /// 名字
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// Html生存器
        /// </summary>
        public IGenHtml GenHTML { get; set; }

        /// <summary>
        /// Html缓存储存器
        /// </summary>
        public IStore Store { get; set; }

        /// <summary>
        /// HttpRequest匹配器
        /// </summary>
        public IPatten Patten { get; set; }

        /// <summary>
        /// Html缓存是否过期判断器
        /// </summary>
        public IExpire Expire { get; set; }

        /// <summary>
        /// HttpRequest生成唯一key生成器
        /// </summary>
        public IGenKey GenKey { get; set; }

        /// <summary>
        /// 判断HttpRequest 是否匹配该Rule
        /// </summary>
        /// <param name="request">HttpRequest</param>
        /// <returns>是否品牌</returns>
        public bool IsPatten(HttpRequest request)
        {
            return Patten.IsPatten(request);
        }


        /// <summary>
        /// 强制刷新缓存标识
        /// </summary>
        public const String REFRESH = "__forced__refresh__";

        /// <summary>
        /// 全局信息，标识该Request/key 是否正在生成html
        /// </summary>
        HashSet<string> GlobalGenHtmlState = new HashSet<string>();

        /// <summary>
        /// 应用该规则
        /// </summary>
        /// <param name="context">HttpContext</param>
        public void Apply(HttpContext context)
        {
            var req = context.Request;
            var key = GenKey.GenKey(req);
            var info = Store.Query(key);
            if (info != null)
            {
                info.Key = key;
            }
            if (req.RawUrl.Contains(REFRESH) || Expire.IsExpire(req, info))
            {
                GenHtmlAndSave(context, key, info);
            }
            else
            {
                ResponceCache(context, key, info);
            }
        }

        /// <summary>
        /// 容许缓存并且转发的各个客户的http头
        /// </summary>
        HashSet<string> alowHeader = new HashSet<string>() { "Content-Type", "Vary" };

        /// <summary>
        /// 输出Http流
        /// </summary>
        /// <param name="rep"></param>
        /// <param name="info"></param>
        private void OutResponse(HttpResponse rep, HttpInfo info)
        {
            rep.AppendHeader("Content-Encoding", "gzip");
            foreach (var item in info.Headers)
            {
                if (alowHeader.Contains(item.Name))
                {
                    rep.AppendHeader(item.Name, item.Value.TrimEnd());
                }
            }
            var buff = new byte[2048];
            var len = 0;
            using (info.Content)
            {
                while ((len = info.Content.Read(buff, 0, 2048)) != 0)
                {
                    rep.OutputStream.Write(buff, 0, len);
                }
            }
        }


        /// <summary>
        /// 缓存没过期，直接输出缓存
        /// </summary>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <param name="info"></param>
        private void ResponceCache(HttpContext context, string key, CacheInfo info)
        {
            var req = context.Request;
            var rep = context.Response;
            string time = info.StoreTime.ToString("r");
            rep.AppendHeader("Last-Modified", time);
            if (req.Headers["If-Modified-Since"] == time)
            {
                rep.StatusCode = (int)System.Net.HttpStatusCode.NotModified;
                LogHelp.Info("cache hit 304 " + req.RawUrl);
            }
            else
            {
                var httpInfo = new HttpInfo();
                var _stream = Store.Get(key);
                int headEndPosition = 0;
                httpInfo.Headers = HttpParseHelp.ParseHeader(_stream, ref headEndPosition);
                httpInfo.Content = _stream;
                OutResponse(rep, httpInfo);
                LogHelp.Info("cache hit html " + req.RawUrl);
            }
            context.ApplicationInstance.CompleteRequest();
        }

        /// <summary>
        /// 生成缓存保存，并输出
        /// </summary>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <param name="info"></param>
        private void GenHtmlAndSave(HttpContext context, string key, CacheInfo info)
        {
            var req = context.Request;
            var rep = context.Response;
            if (!GlobalGenHtmlState.Contains(key))
            {
                try
                {
                    GlobalGenHtmlState.Add(key);
                    var html = GenHTML.GenHTML(req);
                    if (html != null)
                    {
                        var httpInfo = HttpParseHelp.Parse(html);
                        Store.Save(key, html);
                        DateTime lastModifyed = info != null ? info.StoreTime : Store.Query(key).StoreTime;
                        rep.AppendHeader("Last-Modified", lastModifyed.ToString("r"));
                        OutResponse(rep, httpInfo);
                        context.ApplicationInstance.CompleteRequest();
                        LogHelp.Info("genHtml response success " + req.RawUrl);
                    }
                    else
                    {
                        LogHelp.Info("getHtml is null " + req.RawUrl);
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    GlobalGenHtmlState.Remove(key);
                }
            }
        }

    }
}
