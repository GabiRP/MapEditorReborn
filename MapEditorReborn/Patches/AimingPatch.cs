﻿namespace MapEditorReborn.Patches
{
    using API;
    using Exiled.API.Features;
    using HarmonyLib;
#pragma warning disable SA1313

    /// <summary>
    /// Pathches the <see cref="WeaponManager.NetworksyncZoomed"/> for the interface use of the ToolGun.
    /// </summary>
    [HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.NetworksyncZoomed), MethodType.Setter)]
    internal static class AimingPatch
    {
        private static void Postfix(WeaponManager __instance, ref bool value)
        {
            Player player = Player.Get(__instance.gameObject);

            if (!player.CurrentItem.IsToolGun() || player.SessionVariables.ContainsKey(Handler.SelectedObjectSessionVarName))
                return;

            if (value)
            {
                if (player.HasFlashlightEnabled())
                {
                    player.ShowHint(Translation.ModeSelecting, 1f);
                }
                else
                {
                    player.ShowHint(Translation.ModeCopying, 1f);
                }
            }
            else
            {
                if (player.HasFlashlightEnabled())
                {
                    player.ShowHint(Translation.ModeCreating, 1f);
                }
                else
                {
                    player.ShowHint(Translation.ModeDeleting, 1f);
                }
            }
        }

        private static readonly Translation Translation = MapEditorReborn.Singleton.Translation;
    }
}
