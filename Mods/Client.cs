using GorillaLocomotion;
using GorillaNetworking;
using GorillaTag;
using GorillaTagScripts;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static StupidTemplate.Menu.Main;

namespace Unnamed_Free.Mods
{
    public class Client
    {




        public static void UnlockFanClub()
        {
            SubscriptionPatches.enabled = !SubscriptionPatches.enabled;
        }


        public static CosmeticsController GetCosmetics()
        {
            return CosmeticsController.instance;
        }
        public static void UnlockAllCosmetics()
        {
            var cosmetics = GetCosmetics();
            if (cosmetics == null) return;
            foreach (var item in cosmetics.allCosmetics)
            {
                if (!item.isNullItem && !cosmetics.unlockedCosmetics.Contains(item))
                {
                    cosmetics.unlockedCosmetics.Add(item);
                    switch (item.itemCategory)
                    {
                        case CosmeticsController.CosmeticCategory.Hat:
                            if (!cosmetics.unlockedHats.Contains(item))
                                cosmetics.unlockedHats.Add(item);
                            break;
                        case CosmeticsController.CosmeticCategory.Face:
                            if (!cosmetics.unlockedFaces.Contains(item))
                                cosmetics.unlockedFaces.Add(item);
                            break;
                        case CosmeticsController.CosmeticCategory.Badge:
                            if (!cosmetics.unlockedBadges.Contains(item))
                                cosmetics.unlockedBadges.Add(item);
                            break;
                        case CosmeticsController.CosmeticCategory.Paw:
                            if (!item.isThrowable)
                            {
                                if (!cosmetics.unlockedPaws.Contains(item))
                                    cosmetics.unlockedPaws.Add(item);
                            }
                            else
                            {
                                if (!cosmetics.unlockedThrowables.Contains(item))
                                    cosmetics.unlockedThrowables.Add(item);
                            }
                            break;
                        case CosmeticsController.CosmeticCategory.Fur:
                            if (!cosmetics.unlockedFurs.Contains(item))
                                cosmetics.unlockedFurs.Add(item);
                            break;
                        case CosmeticsController.CosmeticCategory.Shirt:
                            if (!cosmetics.unlockedShirts.Contains(item))
                                cosmetics.unlockedShirts.Add(item);
                            break;
                        case CosmeticsController.CosmeticCategory.Back:
                            if (!cosmetics.unlockedBacks.Contains(item))
                                cosmetics.unlockedBacks.Add(item);
                            break;
                        case CosmeticsController.CosmeticCategory.Arms:
                            if (!cosmetics.unlockedArms.Contains(item))
                                cosmetics.unlockedArms.Add(item);
                            break;
                        case CosmeticsController.CosmeticCategory.Chest:
                            if (!cosmetics.unlockedChests.Contains(item))
                                cosmetics.unlockedChests.Add(item);
                            break;
                        case CosmeticsController.CosmeticCategory.Pants:
                            if (!cosmetics.unlockedPants.Contains(item))
                                cosmetics.unlockedPants.Add(item);
                            break;
                        case CosmeticsController.CosmeticCategory.TagEffect:
                            if (!cosmetics.unlockedTagFX.Contains(item))
                                cosmetics.unlockedTagFX.Add(item);
                            break;
                    }
                }
            }
            cosmetics.UpdateWardrobeModelsAndButtons();
            cosmetics.OnCosmeticsUpdated?.Invoke();
        }

        






        public class SubscriptionPatches
        {
            public static bool enabled;

            [HarmonyPatch(typeof(SubscriptionManager), nameof(SubscriptionManager.IsLocalSubscribed))]
            public class IsLocalSubscribed
            {
                private static bool Prefix(ref bool __result)
                {
                    if (!enabled)
                        return true;

                    __result = true;
                    return false;
                }
            }

            [HarmonyPatch(typeof(SubscriptionManager), nameof(SubscriptionManager.LocalSubscriptionStatus))]
            public class LocalSubscriptionStatus
            {
                private static bool Prefix(ref SubscriptionManager.SubscriptionStatus __result)
                {
                    if (!enabled)
                        return true;

                    __result = SubscriptionManager.SubscriptionStatus.Active;
                    return false;
                }
            }

            [HarmonyPatch(typeof(SubscriptionManager), nameof(SubscriptionManager.LocalSubscriptionDetails))]
            public class LocalSubscriptionDetails
            {
                private static bool Prefix(ref SubscriptionManager.SubscriptionDetails __result)
                {
                    if (!enabled)
                        return true;

                    __result = new SubscriptionManager.SubscriptionDetails
                    {
                        active = true,
                        daysAccrued = int.MaxValue,
                        subscriptionFeatureSettings = new[] { true, true },
                        tier = int.MaxValue,
                        subscriptionActiveUntilDate = DateTime.MaxValue,
                        autoRenew = true,
                        autoRenewMonths = int.MaxValue
                    };
                    return false;
                }
            }
        }
    }
}