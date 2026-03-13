using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Saves;

namespace AscensionTracker;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    private const string
        ModId = "AscensionTracker";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Harmony harmony = new(ModId);

        harmony.PatchAll();
    }
}


internal static class AscensionTracker
{
    private const string OverlayName = "HighestWin";

    private static readonly ConditionalWeakTable<NCard, Control> overlaysByCard = new();
    private static readonly ConditionalWeakTable<NCard, Label> labelsByCard = new();

    public static void EnsureOverlay(NCard? card)
    {
        if (card == null || !card.IsNodeReady())
            return;

        if (!overlaysByCard.TryGetValue(card, out _))
        {
            var (anchor, label) = CreateBackdropContainer();
            card.AddChild(anchor);
            overlaysByCard.Add(card, anchor);
            if (label != null)
                labelsByCard.Add(card, label);
        }

        Refresh(card);
    }
    
    public static void Refresh(NCard card)
    {
        if (!overlaysByCard.TryGetValue(card, out var container))
            return;

        if (card._model != null &&
            Runs.maxWinningAscensionByCard.TryGetValue(card._model.Id, out int ascension) &&
            labelsByCard.TryGetValue(card, out var label))
        {
            label.Text = ascension.ToString();
            container.Visible = true;
        }
        else
        {
            container.Visible = false;
        }
    }

    private static (Control anchor, Label? label) CreateBackdropContainer()
    {
        var anchor = new Control
        {
            Name = OverlayName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 0,
            Visible = false
        };

        anchor.AnchorLeft   = 1f;
        anchor.AnchorRight  = 1f;
        anchor.AnchorTop    = 0f;
        anchor.AnchorBottom = 0f;
        anchor.OffsetLeft   = -80f;
        anchor.OffsetRight  = 0f;
        anchor.OffsetTop    = 140f;
        anchor.OffsetBottom = 200f;

        var (backdrop, label) = LoadBackdropFromScene();
        if (backdrop != null)
        {
            backdrop.MouseFilter = Control.MouseFilterEnum.Ignore;
            backdrop.AnchorLeft   = 0f;
            backdrop.AnchorRight  = 5f;
            backdrop.AnchorTop    = 0f;
            backdrop.AnchorBottom = 1f;
            backdrop.Modulate = new Color(1f, 1f, 1f, 0.90f);
            anchor.AddChild(backdrop);
        }

        if (label != null)
        {
            label.AnchorBottom -= 0.1f;
            label.AnchorTop -= 0.1f;
            label.AddThemeFontSizeOverride("font_size", 20);
            label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0f));
            label.AddThemeConstantOverride("shadow_offset_x", 0);
            label.AddThemeConstantOverride("shadow_offset_y", 0);
            label.Modulate = new Color(1f, 1f, 1f, 0.90f);
        }

        return (anchor, label);
    }

    private static (Control? backdrop, Label? existingLabel) LoadBackdropFromScene()
    {
        const string scenePath = "res://scenes/screens/ascension_panel.tscn";
        const string nodePath  = "HBoxContainer/AscensionIconContainer/AscensionIcon";

        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        if (scene == null) return (null, null)!;

        var tempRoot = scene.Instantiate<Control>();
        var sourceNode = tempRoot.GetNodeOrNull<Control>(nodePath);

        if (sourceNode == null)
        {
            tempRoot.QueueFree();
            return (null, null)!;
        }

        var backdrop = sourceNode.Duplicate(8) as Control;
        var existingLabel = backdrop?.GetChildren().OfType<Label>().FirstOrDefault();

        tempRoot.QueueFree();

        return (backdrop, existingLabel);
    }

    public static void Hide(NCard? card)
    {
        if (card == null) return;

        if (overlaysByCard.TryGetValue(card, out var container))
            container.Visible = false;
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard._Ready))]
internal static class NCard_ReadyPatch
{
    private static void Postfix(NCard __instance)
    {
        if (CombatManager.Instance.IsInProgress != true)
            AscensionTracker.EnsureOverlay(__instance);
    }
}

[HarmonyPatch(typeof(NCard), "set_Model")]
internal static class NCard_SetModelPatch
{
    private static void Postfix(NCard __instance)
    {
        if (CombatManager.Instance.IsInProgress == true)
            AscensionTracker.Hide(__instance);
        else
            AscensionTracker.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.OnReturnedFromPool))]
internal static class NCard_OnReturnedFromPoolPatch
{
    private static void Postfix(NCard __instance)
    {
        AscensionTracker.Hide(__instance);
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class NMainMenu_ReadyPatch
{
    private static void Postfix()
    {
        Runs.RebuildFromCurrentProfile();
    }
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SwitchProfileId))]
internal static class SaveManager_SwitchProfileIdPatch
{
    private static void Postfix()
    {
        Runs.RebuildFromCurrentProfile();
    }
}

[HarmonyPatch(typeof(NCardFlyVfx), nameof(NCardFlyVfx._Ready))]
internal static class NCardFlyVfx_ReadyPatch
{
    private static void Postfix(NCardFlyVfx __instance)
    {
        AscensionTracker.Hide(__instance._card);
    }
}