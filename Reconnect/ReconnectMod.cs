using System.Threading.Tasks;

using MelonLoader;
using HarmonyLib;

using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.IO;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Networking.API;
using ABI_RC.Core.Networking.API.Responses;
using ABI_RC.Core.Networking.Guardian;
using ABI_RC.Core.Networking.IO.Instancing;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Core.Util;

using UnityEngine;

namespace Reconnect
{
    public class ReconnectMod : MelonMod
    {
        [HarmonyPatch(typeof(GuardianMessaging), "ViewGuardianDropText")]
        public class Fix
        {
            static void Postfix(string a, string h, string t)
            {
                if (t.StartsWith("Invalid join ticket"))
                    Reconnect();
            }
        }

        private static void Disconnect()
        {
            NetworkManager.Instance.OnDisconnectionRequested(0, false);
        }

        private static void Reconnect()
        {
            MelonLogger.Msg("Reconnecting...");

            Disconnect();
            Task.Run(() => GetInstanceJoinTokenTask(MetaPort.Instance.CurrentInstanceId));
        }

        private static async Task GetInstanceJoinTokenTask(string instanceId)
        {
            var response = await ApiConnection.MakeRequest<InstanceJoinResponse>(ApiConnection.ApiOperation.InstanceJoin, new { instanceID = instanceId });

            if (response?.Data != null)
            {
                CVRDownloadManager.Instance.RemoveJobOfType(DownloadTask.ObjectType.Avatar);
                CVRDownloadManager.Instance.RemoveJobOfType(DownloadTask.ObjectType.Prop);
                foreach (CVRPlayerEntity cvrplayerEntity in CVRPlayerManager.Instance.NetworkPlayers)
                {
                    Object.Destroy(cvrplayerEntity.PuppetMaster.avatarObject);
                }
                CVRSyncHelper.DeleteAllProps();
                AssetBundle.UnloadAllAssetBundles(false);
                CohtmlHud.Instance.SetDisplayChain(1);

                Instances.RequestedInstance = instanceId;
                Instances.InstanceJoinJWT = response.Data.Jwt;
                Instances.Fqdn = response.Data.Host.Fqdn;
                Instances.Port = response.Data.Host.Port;
                await NetworkManager.Instance.ConnectToGameServer();
            }
            else if (response != null) ViewManager.Instance.BufferMenuPopup(response.Message);
        }
    }
}
