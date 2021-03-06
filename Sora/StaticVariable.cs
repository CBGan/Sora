using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using Fleck;
using Newtonsoft.Json.Linq;
using Sora.Entities.Info.InternalDataInfo;
using Sora.Enumeration;
using Websocket.Client;
using YukariToolBox.FormatLog;

namespace Sora
{
    /// <summary>
    /// 静态变量存放区
    /// </summary>
    internal static class StaticVariable
    {
        /// <summary>
        /// 连续对话匹配上下文
        /// Key:当前对话标识符
        /// </summary>
        internal static readonly ConcurrentDictionary<Guid, WaitingInfo> WaitingDict = new();

        /// <summary>
        /// 数据文本匹配正则
        /// </summary>
        internal static readonly Dictionary<CQFileType, Regex> FileRegices = new()
        {
            //绝对路径-linux/osx
            {
                CQFileType.UnixFile, new Regex(@"^(/[^/ ]*)+/?([a-zA-Z0-9]+\.[a-zA-Z0-9]+)$", RegexOptions.Compiled)
            },
            //绝对路径-win
            {
                CQFileType.WinFile,
                new Regex(@"^(?:[a-zA-Z]:\/)(?:[^\/|<>?*:""]*\/)*[^\/|<>?*:""]*$", RegexOptions.Compiled)
            },
            //base64
            {
                CQFileType.Base64, new Regex(@"^base64:\/\/[\/]?([\da-zA-Z]+[\/+]+)*[\da-zA-Z]+([+=]{1,2}|[\/])?$",
                                             RegexOptions.Compiled)
            },
            //网络图片链接
            {
                CQFileType.Url,
                new
                    Regex(@"^(http|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?$",
                          RegexOptions.Compiled)
            },
            //文件名
            {CQFileType.FileName, new Regex(@"^[\w,\s-]+\.[a-zA-Z0-9]+$", RegexOptions.Compiled)}
        };

        /// <summary>
        /// API响应被观察对象
        /// 结构:Tuple[echo标识符,响应json]
        /// </summary>
        internal static readonly Subject<Tuple<Guid, JObject>> ApiSubject = new();

        /// <summary>
        /// WS静态连接记录表
        /// Key:链接标识符
        /// </summary>
        internal static readonly ConcurrentDictionary<Guid, SoraConnectionInfo> ConnectionInfos = new();

        /// <summary>
        /// 服务信息
        /// Key:服务标识符
        /// </summary>
        internal static readonly ConcurrentDictionary<Guid, ServiceInfo> ServiceInfos = new();

        /// <summary>
        /// 清除服务数据
        /// </summary>
        /// <param name="serviceId">服务标识</param>
        internal static void CleanServiceInfo(Guid serviceId)
        {
            Log.Debug("Sora", "Detect service dispose, cleanup service info...");
            //清空服务信息
            ServiceInfos.TryRemove(serviceId, out _);
            //清空连接信息
            var removeConnList = ConnectionInfos.Where(i => i.Value.ServiceId == serviceId)
                                                .ToList();
            foreach (var (guid, conn) in removeConnList)
            {
                switch (conn.Connection)
                {
                    case IWebSocketConnection serverConn:
                        serverConn.Close();
                        break;
                    case WebsocketClient client:
                        client.Stop(WebSocketCloseStatus.Empty, "sora service dispose");
                        break;
                    default:
                        Log.Error("ConnectionManager", "unknown error when destory Connection instance");
                        break;
                }

                ConnectionInfos.TryRemove(guid, out _);
            }

            //清空等待信息
            var removeWaitList = WaitingDict.Where(i => i.Value.ServiceId == serviceId)
                                            .ToList();
            foreach (var (guid, waitingInfo) in removeWaitList)
            {
                waitingInfo.Semaphore.Set();
                WaitingDict.TryRemove(guid, out _);
            }

            Log.Debug("Sora", "Service info cleanup finished");
        }
    }
}