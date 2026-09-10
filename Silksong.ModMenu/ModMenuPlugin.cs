using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using MonoDetour;
using MonoDetour.HookGen;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Internal;
using Silksong.ModMenu.Screens;
using Silksong.UnityHelper.Extensions;
using UnityEngine;

namespace Silksong.ModMenu;

/// <summary>
/// Plugin implementation for ModMenu.
/// </summary>
[MonoDetourTargets(typeof(UIManager), GenerateControlFlowVariants = true)] // We don't need these but another class does :')
[BepInAutoPlugin(id: "org.silksong-modding.modmenu")]
public partial class ModMenuPlugin : BaseUnityPlugin
{
    private static ModMenuPlugin? instance;
    private static TextButton? modOptionsButton;
    private static MenuButtonList? modOptionsButtonList;

    private void Awake()
    {
        MonoDetourManager.InvokeHookInitializers(typeof(ModMenuPlugin).Assembly);
        instance = this;

        // Hot reload after UIManager.Awake has already been called
        var uiManager = UIManager._instance;
        if (uiManager != null)
            ModifyUICanvas(uiManager);
    }

    private void OnDestroy()
    {
        RemoveModsButton();
        MenuScreenNavigation.CloseAll();

        instance = null;

        DefaultMonoDetourManager.Instance.Dispose();
    }

    private static void RemoveModsButton()
    {
        if (modOptionsButton == null)
            return;

        if (modOptionsButtonList != null)
            modOptionsButtonList.entries = modOptionsButtonList
                .entries.Where(e => e.selectable != modOptionsButton.MenuButton)
                .ToArray();

        modOptionsButton.Dispose();
        modOptionsButton = null;
        modOptionsButtonList = null;
    }

    internal static void LogWarning(string message)
    {
        if (instance != null)
            instance.Logger.LogWarning(message);
        else
            Debug.LogWarning(message);
    }

    internal static void LogError(string message)
    {
        if (instance != null)
            instance.Logger.LogError(message);
        else
            Debug.LogError(message);
    }

    // BepInEx logging has a 2000ms flush delay which can make debugging crashes difficult.
    // If debugging a crash, use this method after logging diagnostics to ensure they're persisted.
    internal static void FlushLogs()
    {
        foreach (var listener in BepInEx.Logging.Logger.Listeners)
        {
            if (listener is DiskLogListener disk)
                disk.LogWriter.Flush();
        }
    }

    private static void ModifyUICanvas(UIManager self)
    {
        MenuPrefabs.Load(self);

        var optionsScreen = self.optionsMenuScreen;

        // Insert the button at the desired index.
        TextButton modOptions = new("Mods") // TODO: Support localization.
        {
            OnSubmit = () => MenuScreenNavigation.Show(GetModsMenu()),
        };
        modOptions.SetGameObjectParent(optionsScreen.gameObject.FindChild("Content")!);
        modOptionsButton = modOptions;

        // Track the selectable at the correct index (BackButton is on the end of the list from a separate container).
        var mbl = optionsScreen.gameObject.GetComponent<MenuButtonList>();
        modOptionsButtonList = mbl;
        List<MenuButtonList.Entry> entries = [.. mbl.entries];
        entries.Insert(5, new() { selectable = modOptions.MenuButton });
        mbl.entries = [.. entries];
    }

    private static AbstractMenuScreen? modsMenu;
    private static (string Guid, BaseUnityPlugin Instance)[]? modsMenuIdentity;

    private static AbstractMenuScreen GetModsMenu()
    {
        var identity = Chainloader
            .PluginInfos.Select(info => (info.Key, info.Value.Instance))
            .ToArray();
        if (modsMenu != null)
        {
            if (identity.SequenceEqual(modsMenuIdentity ?? []))
                return modsMenu;

            Destroy(modsMenu.Container);
        }

        PaginatedMenuScreenBuilder builder = new("Mods");
        builder.AddRange(Registry.GenerateAllMenuElements());
        var menu = builder.Build();

        modsMenu = menu;
        modsMenuIdentity = identity;
        menu.OnDispose += () =>
        {
            if (modsMenu == menu)
            {
                modsMenu = null;
                modsMenuIdentity = null;
            }
        };
        return menu;
    }

    [MonoDetourHookInitialize]
    private static void Hook() => Md.UIManager.Awake.Postfix(ModifyUICanvas);
}
