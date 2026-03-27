using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using YongChuanTools.Data;
using YongChuanTools.Core;
using Serilog;

namespace YongChuanTools.Web
{
    /// <summary>
    /// SignalR Hub — 前端通过这里实时接收推送
    /// </summary>
    public class DashboardHub : Hub
    {
        private readonly EquipmentStore _store;

        public DashboardHub(EquipmentStore store)
        {
            _store = store;
        }

        /// <summary>前端主动获取当前状态</summary>
        public Task<DashboardSnapshot> GetSnapshot()
        {
            var (total, equip, lastId, sessions) = _store.GetStats();
            return Task.FromResult(new DashboardSnapshot
            {
                TotalMessages = total,
                EquipmentCount = equip,
                ConnectedSessions = sessions,
                LastMessageId = lastId,
                Equipment = new System.Collections.Generic.List<EquipmentSnapshot>(_store.GetAllEquipment()),
                Systems = new System.Collections.Generic.List<SystemSnapshot>(_store.GetAllSystems())
            });
        }

        public override async Task OnConnectedAsync()
        {
            Log.Information("Web仪表盘客户端连接: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            Log.Information("Web仪表盘客户端断开: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }

    public class DashboardSnapshot
    {
        public long TotalMessages { get; set; }
        public long EquipmentCount { get; set; }
        public int ConnectedSessions { get; set; }
        public long LastMessageId { get; set; }
        public System.Collections.Generic.List<EquipmentSnapshot> Equipment { get; set; } = new();
        public System.Collections.Generic.List<SystemSnapshot> Systems { get; set; } = new();
    }
}
