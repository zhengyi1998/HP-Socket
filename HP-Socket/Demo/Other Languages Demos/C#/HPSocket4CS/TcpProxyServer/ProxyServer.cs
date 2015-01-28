﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPSocketCS;
using System.Runtime.InteropServices;

namespace TcpProxyServer
{
    public class ProxyServer
    {
        /// <summary>
        /// 绑定地址
        /// </summary>
        public string BindAddr { get; set; }

        /// <summary>
        /// 绑定端口
        /// </summary>
        public ushort BindPort { get; set; }

        /// <summary>
        /// 目标地址
        /// </summary>
        public string TargetAddr { get; set; }

        /// <summary>
        /// 目标端口
        /// </summary>
        public ushort TargetPort { get; set; }

        // 为了简单直接定义了一个支持log输出的委托
        public delegate void ShowMsg(string msg);
        /// <summary>
        /// 日志输出
        /// </summary>
        public ShowMsg AddMsgDelegate;

        protected TcpServer server = new TcpServer();
        protected TcpAgent agent = new TcpAgent();


        public ProxyServer()
        {
            // 设置服务器事件
            server.OnPrepareListen += new TcpServerEvent.OnPrepareListenEventHandler(OnServerPrepareListen);
            server.OnAccept += new TcpServerEvent.OnAcceptEventHandler(OnServerAccept);
            server.OnSend += new TcpServerEvent.OnSendEventHandler(OnServerSend);
            server.OnReceive += new TcpServerEvent.OnReceiveEventHandler(OnServerReceive);
            server.OnClose += new TcpServerEvent.OnCloseEventHandler(OnServerClose);
            server.OnError += new TcpServerEvent.OnErrorEventHandler(OnServerError);
            server.OnShutdown += new TcpServerEvent.OnShutdownEventHandler(OnServerShutdown);


            // 设置代理事件
            agent.OnPrepareConnect += new TcpAgentEvent.OnPrepareConnectEventHandler(OnAgentPrepareConnect);
            agent.OnConnect += new TcpAgentEvent.OnConnectEventHandler(OnAgentConnect);
            agent.OnSend += new TcpAgentEvent.OnSendEventHandler(OnAgentSend);
            agent.OnReceive += new TcpAgentEvent.OnReceiveEventHandler(OnAgentReceive);
            agent.OnClose += new TcpAgentEvent.OnCloseEventHandler(OnAgentClose);
            agent.OnError += new TcpAgentEvent.OnErrorEventHandler(OnAgentError);
            agent.OnShutdown += new TcpAgentEvent.OnShutdownEventHandler(OnAgentShutdown);

        }

        public bool Start()
        {
            if (string.IsNullOrEmpty(BindAddr) || string.IsNullOrEmpty(TargetAddr) ||
                BindPort == 0 || TargetPort == 0 || AddMsgDelegate == null)
            {
                throw new Exception("请先设置属性[BindAddr,TargetAddr,BindPort,TargetPort,AddMsgDelegate]");
            }

            server.IpAddress = BindAddr;
            server.Port = BindPort;
            bool isStart = server.Start();
            if (isStart == false)
            {
                AddMsg(string.Format(" > Server start fail -> {0}({1})", server.ErrorMessage, server.ErrorCode));
                return isStart;
            }

            isStart = agent.Start(BindAddr, false);
            if (isStart == false)
            {
                AddMsg(string.Format(" > Server start fail -> {0}({1})", agent.ErrorMessage, agent.ErrorCode));
                return isStart;
            }

            return isStart;
        }

        public bool Stop()
        {
            return server.Stop() && agent.Stop();
        }

        private void AddMsg(string msg)
        {
            AddMsgDelegate(msg);
        }


        public bool Disconnect(IntPtr connId, bool force = true)
        {
            return server.Disconnect(connId, force);
        }

        //////////////////////////////Agent//////////////////////////////////////////////////

        /// <summary>
        /// 准备连接了
        /// </summary>
        /// <param name="connId"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        protected virtual HandleResult OnAgentPrepareConnect(IntPtr connId, uint socket)
        {
            return HandleResult.Ok;
        }

        /// <summary>
        /// 已连接
        /// </summary>
        /// <param name="connId"></param>
        /// <returns></returns>
        protected virtual HandleResult OnAgentConnect(IntPtr connId)
        {
            AddMsg(string.Format(" > [{0},OnAgentConnect]", connId));
            return HandleResult.Ok;
        }

        /// <summary>
        /// 客户端发数据了
        /// </summary>
        /// <param name="connId"></param>
        /// <param name="pData"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        protected virtual HandleResult OnAgentSend(IntPtr connId, IntPtr pData, int length)
        {
            AddMsg(string.Format(" > [{0},OnAgentSend] -> ({1} bytes)", connId, length));
            return HandleResult.Ok;
        }

        /// <summary>
        /// 数据到达了
        /// </summary>
        /// <param name="connId"></param>
        /// <param name="pData"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        protected virtual HandleResult OnAgentReceive(IntPtr connId, IntPtr pData, int length)
        {
            // 获取附加数据
            IntPtr extraPtr = IntPtr.Zero;
            if (agent.GetConnectionExtra(connId, ref extraPtr) == false)
            {
                return HandleResult.Error;
            }
            
            ConnExtraData extra = (ConnExtraData)Marshal.PtrToStructure(extraPtr, typeof(ConnExtraData));
            AddMsg(string.Format(" > [{0},OnAgentReceive] -> ({1} bytes)", connId, length));
            if (extra.Server.Send(extra.ConnIdForServer, pData, length) == false)
            {
                return HandleResult.Error;
            }

            return HandleResult.Ok;
        }

        /// <summary>
        /// 连接关闭了
        /// </summary>
        /// <param name="connId"></param>
        /// <returns></returns>
        protected virtual HandleResult OnAgentClose(IntPtr connId)
        {
            AddMsg(string.Format(" > [{0},OnAgentClose]", connId));

            // 获取附加数据
            IntPtr extraPtr = IntPtr.Zero;
            if (agent.GetConnectionExtra(connId, ref extraPtr) == false)
            {
                return HandleResult.Error;
            }

            ConnExtraData extra = (ConnExtraData)Marshal.PtrToStructure(extraPtr, typeof(ConnExtraData));

            agent.SetConnectionExtra(connId, null);

            if (extra.FreeType == 0)
            {

                // 由Target断开连接,释放server连接
                extra.FreeType = 1;
                server.SetConnectionExtra(extra.ConnIdForServer, extra);
                extra.Server.Disconnect(extra.ConnIdForServer);
            }


            return HandleResult.Ok;
        }

        /// <summary>
        /// 出错了
        /// </summary>
        /// <param name="connId"></param>
        /// <param name="enOperation"></param>
        /// <param name="errorCode"></param>
        /// <returns></returns>
        protected virtual HandleResult OnAgentError(IntPtr connId, SocketOperation enOperation, int errorCode)
        {
            AddMsg(string.Format(" > [{0},OnAgentError] -> OP:{1},CODE:{2}", connId, enOperation, errorCode));
            // return HPSocketSdk.HandleResult.Ok;

            // 因为要释放附加数据,所以直接返回OnAgentClose()了
            return OnAgentClose(connId);
        }

        /// <summary>
        /// Agent关闭了
        /// </summary>
        /// <returns></returns>
        protected virtual HandleResult OnAgentShutdown()
        {
            AddMsg(" > [OnAgentShutdown]");
            return HandleResult.Ok;
        }

        //////////////////////////////Server//////////////////////////////////////////////////

        /// <summary>
        /// 监听事件
        /// </summary>
        /// <param name="soListen"></param>
        /// <returns></returns>
        protected virtual HandleResult OnServerPrepareListen(IntPtr soListen)
        {
            return HandleResult.Ok;
        }

        /// <summary>
        /// 客户进入
        /// </summary>
        /// <param name="connId"></param>
        /// <param name="pClient"></param>
        /// <returns></returns>
        protected virtual HandleResult OnServerAccept(IntPtr connId, IntPtr pClient)
        {
            // 获取客户端ip和端口
            string ip = string.Empty;
            ushort port = 0;
            if (server.GetRemoteAddress(connId, ref ip, ref port))
            {
                AddMsg(string.Format(" > [{0},OnServerAccept] -> PASS({1}:{2})", connId, ip.ToString(), port));
            }
            else
            {
                AddMsg(string.Format(" > [{0},OnServerAccept] -> Server_GetClientAddress() Error", connId));
                return HandleResult.Error;
            }

            IntPtr clientConnId = IntPtr.Zero;

            // 一次不成功的事偶尔可能发生,三次连接都不成功,那就真连不上了
            // 当server有连接进入,使用agent连接到目标服务器
            if (agent.Connect(TargetAddr, TargetPort, ref clientConnId) == false)
            {
                if (agent.Connect(TargetAddr, TargetPort, ref clientConnId) == false)
                {
                    if (agent.Connect(TargetAddr, TargetPort, ref clientConnId) == false)
                    {
                        AddMsg(string.Format(" > [Client->Connect] fail -> ID:{0}", clientConnId));
                        return HandleResult.Error;
                    }
                }
            }


            // 设置附加数据
            ConnExtraData extra = new ConnExtraData();
            extra.ConnIdForServer = connId;
            extra.ConnIdForClient = clientConnId;
            extra.Server = server;
            extra.FreeType = 0;
            if (server.SetConnectionExtra(connId, extra) == false)
            {
                AddMsg(string.Format(" > [{0},OnServerAccept] -> server.SetConnectionExtra fail", connId));
                return HandleResult.Error;
            }

            if (agent.SetConnectionExtra(clientConnId, extra) == false)
            {
                server.SetConnectionExtra(connId, null);
                AddMsg(string.Format(" > [{0}-{1},OnServerAccept] -> agent.SetConnectionExtra fail", connId, clientConnId));
                return HandleResult.Error;
            }

            return HandleResult.Ok;
        }

        /// <summary>
        /// 服务器发数据了
        /// </summary>
        /// <param name="connId"></param>
        /// <param name="pData"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        protected virtual HandleResult OnServerSend(IntPtr connId, IntPtr pData, int length)
        {
            AddMsg(string.Format(" > [Server->OnServerSend] -> ({0} bytes)", length));
            return HandleResult.Ok;
        }

        /// <summary>
        /// 数据到达了
        /// </summary>
        /// <param name="connId"></param>
        /// <param name="pData"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        protected virtual HandleResult OnServerReceive(IntPtr connId, IntPtr pData, int length)
        {
            try
            {
                // 获取附加数据
                IntPtr extraPtr = IntPtr.Zero;

                if (server.GetConnectionExtra(connId, ref extraPtr) == false)
                {
                    return HandleResult.Error;
                }

                // extra 就是accept里传入的附加数据了
                ConnExtraData extra = (ConnExtraData)Marshal.PtrToStructure(extraPtr, typeof(ConnExtraData));

                AddMsg(string.Format(" > [Server->OnServerReceive] -> ({0} bytes)", length));

                // 服务端收到数据了,应该调用agent发送到顶层服务器,实现 client(N)->server->targetServer 的中转
                if (agent.Send(extra.ConnIdForClient, pData, length) == false)
                {
                    return HandleResult.Error;
                }

                return HandleResult.Ok;

            }
            catch (Exception)
            {
                return HandleResult.Error;
            }
            
        }

        /// <summary>
        /// 客户离开了
        /// </summary>
        /// <param name="connId"></param>
        /// <returns></returns>
        protected virtual HandleResult OnServerClose(IntPtr connId)
        {
            // 获取附加数据
            IntPtr extraPtr = IntPtr.Zero;
            if (server.GetConnectionExtra(connId, ref extraPtr) == false)
            {
                return HandleResult.Error;
            }

            // extra 就是accept里传入的附加数据了
            ConnExtraData extra = (ConnExtraData)Marshal.PtrToStructure(extraPtr, typeof(ConnExtraData));
            if (extra.FreeType == 0)
            {
                // 由client(N)断开连接,释放agent数据
                agent.Disconnect(extra.ConnIdForClient);
                agent.SetConnectionExtra(extra.ConnIdForClient, null);
            }

            server.SetConnectionExtra(connId, null);

            AddMsg(string.Format(" > [{0},OnServerClose]", connId));
            return HandleResult.Ok;
        }

        /// <summary>
        /// 出错了
        /// </summary>
        /// <param name="connId"></param>
        /// <param name="enOperation"></param>
        /// <param name="errorCode"></param>
        /// <returns></returns>
        protected virtual HandleResult OnServerError(IntPtr connId, SocketOperation enOperation, int errorCode)
        {
            AddMsg(string.Format(" > [{0},OnServerError] -> OP:{1},CODE:{2}", connId, enOperation, errorCode));
            // return HPSocketSdk.HandleResult.Ok;

            // 因为要释放附加数据,所以直接返回OnServerClose()了
            return OnServerClose(connId);
        }

        /// <summary>
        /// 服务关闭了
        /// </summary>
        /// <returns></returns>
        protected virtual HandleResult OnServerShutdown()
        {
            AddMsg(" > [OnServerShutdown]");
            return HandleResult.Ok;
        }

        ////////////////////////////////////////////////////////////////////////////////
    }


    [StructLayout(LayoutKind.Sequential)]
    public class ConnExtraData
    {
        // server的CONNID
        public IntPtr ConnIdForServer;

        // client的CONNID
        public IntPtr ConnIdForClient;

        // 保存server端指针,方便在cclient里调用
        public TcpServer Server;

        // 释放方式
        public uint FreeType;
    }
}
