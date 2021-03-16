using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Fleck;
using Newtonsoft.Json.Linq;
using Sora.Exceptions;
using Sora.Interfaces;
using Sora.OnebotInterface;
using Sora.OnebotModel;
using YukariToolBox.FormatLog;
using LogLevel = Fleck.LogLevel;

namespace Sora.Net
{
    /// <summary>
    /// Sora服务器实例
    /// </summary>
    public sealed class SoraWebsocketServer : IDisposable, ISoraService
    {
        #region 属性

        /// <summary>
        /// 服务器配置类
        /// </summary>
        private ServerConfig Config { get; set; }

        /// <summary>
        /// WS服务器
        /// </summary>
        private WebSocketServer Server { get; set; }

        /// <summary>
        /// 心跳包检查计时器
        /// </summary>
        private Timer HeartBeatTimer { get; set; }

        /// <summary>
        /// 事件接口
        /// </summary>
        public EventInterface Event { get; }

        /// <summary>
        /// 服务器连接管理器
        /// </summary>
        public ConnectionManager ConnManager { get; }

        #endregion

        #region 私有字段

        /// <summary>
        /// 服务器已准备启动标识
        /// </summary>
        private readonly bool serverReady;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建一个反向WS服务端
        /// </summary>
        /// <param name="config">服务器配置</param>
        /// <param name="crashAction">发生未处理异常时的回调</param>
        /// <exception cref="ArgumentNullException">读取到了空配置文件</exception>
        /// <exception cref="ArgumentOutOfRangeException">服务器启动参数错误</exception>
        public SoraWebsocketServer(ServerConfig config, Action<Exception> crashAction = null)
        {
            Log.Info("Sora", $"Sora 框架版本:{Assembly.GetExecutingAssembly().GetName().Version}");
            Log.Debug("Sora", "开发交流群：1081190562");
            //检查端口占用
            if (NetUtils.IsPortInUse(config.Port))
            {
                Log.Fatal("Sora", $"端口{config.Port}已被占用，请更换其他端口");
                Log.Warning("Sora", "将在5s后自动退出");
                Thread.Sleep(5000);
                Environment.Exit(0);
            }

            serverReady = false;
            Log.Info("Sora", "Sora WebSocket服务器初始化...");
            Log.Debug("System", Environment.OSVersion);
            //检查参数
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.Port == 0 || config.Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(config.Port), "Port out of range");
            //初始化连接管理器
            ConnManager = new ConnectionManager(config);
            this.Config = config;
            //API超时
            ReactiveApiManager.TimeOut = config.ApiTimeOut;
            //实例化事件接口
            this.Event = new EventInterface(config.EnableSoraCommandManager);
            //禁用原log
            FleckLog.Level = (LogLevel) 4;
            this.Server = new WebSocketServer($"ws://{config.Host}:{config.Port}")
            {
                //出错后进行重启
                RestartAfterListenError = true
            };
            //全局异常事件
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                                                          {
                                                              if (crashAction == null)
                                                                  Helper.FriendlyException(args);
                                                              else
                                                                  crashAction(args.ExceptionObject as Exception);
                                                          };
            serverReady = true;
        }

        #endregion

        #region 服务端启动

        /// <summary>
        /// 启动 Sora 服务
        /// </summary>
        /// <exception cref="SoraServerIsRuningException">已有服务器在运行</exception>
        public ValueTask StartService() => StartServer();

        /// <summary>
        /// 启动WS服务端
        /// </summary>
        /// <exception cref="SoraServerIsRuningException">已有服务器在运行</exception>
        public async ValueTask StartServer()
        {
            if (!serverReady) return;
            //检查是否已有服务器被启动
            if (NetUtils.serviceExitis) throw new SoraServerIsRuningException();
            //启动服务器
            Server.Start(socket =>
                         {
                             //接收事件处理
                             //获取请求头数据
                             if (!socket.ConnectionInfo.Headers.TryGetValue("X-Self-ID",
                                                                            out var selfId) || //bot UID
                                 !socket.ConnectionInfo.Headers.TryGetValue("X-Client-Role",
                                                                            out var role)) //Client Type
                             {
                                 return;
                             }

                             //请求路径检查
                             var isLost = role switch
                             {
                                 "Universal" => !socket.ConnectionInfo.Path.Trim('/')
                                                       .Equals(Config.UniversalPath.Trim('/')),
                                 _ => true
                             };
                             if (isLost)
                             {
                                 socket.Close();
                                 Log.Warning("Sora",
                                             $"关闭与未知客户端的连接[{socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}]，请检查是否设置正确的监听地址");
                                 return;
                             }

                             //打开连接
                             socket.OnOpen = () =>
                                             {
                                                 //获取Token
                                                 if (socket.ConnectionInfo.Headers.TryGetValue("Authorization",
                                                     out var headerValue))
                                                 {
                                                     var token = headerValue.Split(' ')[1];
                                                     Log.Debug("Server", $"get token = {token}");
                                                     //验证Token
                                                     if (!token.Equals(this.Config.AccessToken)) return;
                                                 }

                                                 //向客户端发送Ping
                                                 socket.SendPing(new byte[] {1, 2, 5});
                                                 //事件回调
                                                 ConnManager.OpenConnection(role, selfId, socket,
                                                                            socket.ConnectionInfo.Id);
                                                 Log.Info("Sora",
                                                          $"已连接客户端[{socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}]");
                                             };
                             //关闭连接
                             socket.OnClose = () =>
                                              {
                                                  //移除原连接信息
                                                  if (ConnectionManager.ConnectionExitis(socket.ConnectionInfo.Id))
                                                      ConnManager.CloseConnection(role, selfId,
                                                          socket.ConnectionInfo.Id);

                                                  Log.Info("Sora",
                                                           $"客户端连接被关闭[{socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}]");
                                              };
                             //上报接收
                             socket.OnMessage = (message) =>
                                                {
                                                    //进入事件处理和分发
                                                    Task.Run(() =>
                                                             {
                                                                 this.Event
                                                                     .Adapter(JObject.Parse(message),
                                                                              socket.ConnectionInfo.Id);
                                                             });
                                                };
                         });
            Log.Info("Sora", $"Sora WebSocket服务器正在运行[{Config.Host}:{Config.Port}]");
            //启动心跳包超时检查计时器
            this.HeartBeatTimer = new Timer(ConnManager.HeartBeatCheck, null,
                                            Config.HeartBeatTimeOut, Config.HeartBeatTimeOut);
            NetUtils.serviceExitis = true;

            await Task.Delay(-1);
        }

        /// <summary>
        /// GC析构函数
        /// </summary>
        ~SoraWebsocketServer()
        {
            Dispose();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}