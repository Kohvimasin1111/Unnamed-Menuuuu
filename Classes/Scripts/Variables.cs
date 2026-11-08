using Photon.Pun;
using StupidTemplate.Notifications;
using System;
using System.Collections.Generic;
using System.Text;

namespace Unnamed_Free.Classes.Scripts
{
    public class Variables
    {
        public static void RPCProtection()
        {
            if (!NetworkSystem.Instance.InRoom)
                return;

            try
            {
                MonkeAgent.instance.rpcErrorMax = int.MaxValue;
                MonkeAgent.instance.rpcCallLimit = int.MaxValue;
                MonkeAgent.instance.logErrorMax = int.MaxValue;

                PhotonNetwork.MaxResendsBeforeDisconnect = int.MaxValue;
                PhotonNetwork.QuickResends = int.MaxValue;

                PhotonNetwork.SendAllOutgoingCommands();
            }
            catch { NotifiLib.SendNotification("RPC protection failed, are you in a lobby?"); }
        }
    }
}
