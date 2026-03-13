using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AscensionTracker;

public static class Runs
{
    public static readonly Dictionary<ModelId, int> maxWinningAscensionByCard = new();

    public static void RebuildFromCurrentProfile()
    {
        maxWinningAscensionByCard.Clear();

        SaveManager saveManager = SaveManager.Instance;
        List<string> runHistoryFileNames;

        try
        {
            runHistoryFileNames = saveManager.GetAllRunHistoryNames();
        }
        catch (Exception ex)
        {
            return;
        }

        foreach (var loadResult in runHistoryFileNames.Select(historyFileName => saveManager.LoadRunHistory(historyFileName)))
        {
            if (!loadResult.Success || loadResult.SaveData == null)
                continue;

            var runHistory = loadResult.SaveData;
            if (!runHistory.Win)
                continue;

            AddRunCards(runHistory);
        }

    }

    private static void AddRunCards(RunHistory runHistory)
    {
        foreach (var player in runHistory.Players)
        {
            if (player.Id != 1 && player.Id != PlatformUtil.GetLocalPlayerId(PlatformType.Steam))
                continue;

            IEnumerable<SerializableCard>? deck = player?.Deck;
            if (deck == null)
                continue;

            foreach (SerializableCard card in deck)
            {
                ModelId? cardId = card?.Id;
                if (cardId == null || cardId == ModelId.none)
                    continue;

                if (!maxWinningAscensionByCard.TryGetValue(cardId, out int existingAscension) ||
                    runHistory.Ascension > existingAscension)
                {
                    maxWinningAscensionByCard[cardId] = runHistory.Ascension;
                }
            }
        }
    }
}
