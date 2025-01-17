using Microsoft.AspNetCore.SignalR;
using System;

namespace backupdataBase.Hubs
{
    public class BackupHub:Hub
    {
        public async Task BackUp(double progress)
        {
            await Clients.All.SendAsync("GetBackUpProcess",   progress);
        }
        public async Task Restore(double progress)
        {
            await Clients.All.SendAsync("GetRestoreProcess",   progress);
        }
    }
}
