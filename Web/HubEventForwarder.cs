using Microsoft.AspNetCore.SignalR;
using Serilog;
using System;
using YongChuanTools.Core;
using YongChuanTools.Data;

namespace YongChuanTools.Web
{
    /// <summary>
    /// SignalR 事件转发器 — 订阅 EventAggregator，实时推送给所有前端客户端
    /// </summary>
    public class HubEventForwarder : IDisposable
    {
        private readonly IHubContext<DashboardHub> _hub;
        private readonly EventAggregator _events;
        private bool _disposed = false;

        public HubEventForwarder(IHubContext<DashboardHub> hub, EventAggregator events)
        {
            _hub = hub;
            _events = events;
            Subscribe();
        }

        private void Subscribe()
        {
            _events.Subscribe<EquipmentStateChangedEvent>(OnEquipmentChanged);
            _events.Subscribe<SystemStateChangedEvent>(OnSystemChanged);
            _events.Subscribe<SessionConnectedEvent>(OnSessionConnected);
            _events.Subscribe<SessionDisconnectedEvent>(OnSessionDisconnected);
            _events.Subscribe<RawMessageReceivedEvent>(OnRawReceived);
        }

        private unsafe void OnEquipmentChanged(EquipmentStateChangedEvent e)
        {
            try
            {
                var state = e.State;
                var data = new
                {
                    type = "equip",
                    timestamp = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    sessionId = e.SessionId,
                    remoteEndpoint = e.RemoteEndpoint,
                    seq = state.header.seq,
                    sysType = (EnumEquipStateSysType)state.sysType,
                    sysTypeName = ((EnumEquipStateSysType)state.sysType).ToString(),
                    sysAddr = state.sysAddr,
                    equipType = (EnumEquipStateEquipType)state.equipType,
                    equipTypeName = ((EnumEquipStateEquipType)state.equipType).ToString(),
                    equipAddr = state.GetEquipAddr(),
                    equipState = state.equipState,
                    stateName = DecodeState(state.equipState),
                    rawHex = e.RawHex
                };

                Log.Information("HubEventForwarder: 推送 ReceiveEquipUpdate equipAddr={EquipAddr} state={State}",
                    state.GetEquipAddr(), state.equipState);
                _hub.Clients.All.SendAsync("ReceiveEquipUpdate", data);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "HubEventForwarder.OnEquipmentChanged failed");
            }
        }

        private unsafe void OnSystemChanged(SystemStateChangedEvent e)
        {
            try
            {
                var state = e.State;
                var data = new
                {
                    type = "sys",
                    timestamp = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    sessionId = e.SessionId,
                    remoteEndpoint = e.RemoteEndpoint,
                    seq = state.header.seq,
                    sysType = (EnumEquipStateSysType)state.sysType,
                    sysTypeName = ((EnumEquipStateSysType)state.sysType).ToString(),
                    sysAddr = state.sysAddr,
                    sysState = state.sysState,
                    stateName = DecodeSysState(state.sysState),
                    rawHex = e.RawHex
                };

                _hub.Clients.All.SendAsync("ReceiveSysUpdate", data);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "HubEventForwarder.OnSystemChanged failed");
            }
        }

        private void OnSessionConnected(SessionConnectedEvent e)
        {
            try
            {
                _hub.Clients.All.SendAsync("ReceiveSessionUpdate", new
                {
                    type = "connected",
                    sessionId = e.SessionId,
                    remoteEndpoint = e.RemoteEndpoint,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "HubEventForwarder.OnSessionConnected failed");
            }
        }

        private void OnSessionDisconnected(SessionDisconnectedEvent e)
        {
            try
            {
                _hub.Clients.All.SendAsync("ReceiveSessionUpdate", new
                {
                    type = "disconnected",
                    sessionId = e.SessionId,
                    remoteEndpoint = e.RemoteEndpoint,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "HubEventForwarder.OnSessionDisconnected failed");
            }
        }

        private void OnRawReceived(RawMessageReceivedEvent e)
        {
            try
            {
                _hub.Clients.All.SendAsync("ReceiveRawMessage", new
                {
                    type = "raw",
                    timestamp = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    remoteEndpoint = e.RemoteEndpoint,
                    hex = BitConverter.ToString(e.Buffer).Replace("-", " "),
                    length = e.Buffer.Length
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "HubEventForwarder.OnRawReceived failed");
            }
        }

        private string DecodeState(ushort state) => UTProtocol.FormatEquipStateName(state);

        private string DecodeSysState(ushort state) => UTProtocol.FormatSysStateName(state);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
