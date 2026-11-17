using UnityEditor;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Editor.Automation
{
    /// <summary>
    /// 戦闘画面の UI を撮るための下ごしらえ（T383）。Play 中の戦闘で、味方4種を置ける最初のマスへ1体ずつ置き、
    /// 最初の味方のスキルの色のゲージを満タンにする（頭上の印と札のゲージが見える）。置いた後は所持金を ¥110 にして札の状態（置ける・所持金不足・待ち時間）が分かれるようにする。
    /// </summary>
    internal static class BattlePreviewMenu
    {
        [MenuItem("Tools/UnitySlotTower/Automation/Stage Battle Preview (T383)")]
        private static void Stage()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[BattlePreview] Play 中に実行する。");
                return;
            }

            var battle = Object.FindAnyObjectByType<BattleController>();
            if (battle == null || battle.Run == null || battle.IsInIntro)
            {
                Debug.LogError("[BattlePreview] 戦闘が始まっていない（開始演出の後に実行する）。");
                return;
            }

            Application.runInBackground = true;
            battle.Wallet.Add(2000);
            var map = battle.Run.Map;
            var placed = 0;
            foreach (var unit in battle.Units.All)
            {
                if (battle.Deployment.Select(unit.Id) != SummonResult.Summoned)
                {
                    continue;
                }

                if (TryPlace(battle, map))
                {
                    placed++;
                }
                else
                {
                    battle.Deployment.Cancel();
                }
            }

            // 置いた後は ¥110 に減らす（値段の高い札が「所持金不足」になる）。
            battle.Wallet.TrySpend(Mathf.Max(0, battle.Wallet.Coins - 110));
            foreach (var unit in battle.Units.All)
            {
                if (battle.Skills != null && battle.Skills.TryGetColor(unit.Id, out var color))
                {
                    for (var i = 0; i < battle.Run.State.Gauges.Max; i++)
                    {
                        battle.Run.State.Gauges.Add(color);
                    }

                    break;
                }
            }

            // 説明の幕を止める（撮影の邪魔になる）。
            var guide = Object.FindAnyObjectByType<GuideOverlay>();
            if (guide != null)
            {
                guide.enabled = false;
                foreach (Transform child in guide.transform)
                {
                    child.gameObject.SetActive(false);
                }
            }

            Debug.Log($"[BattlePreview] placed={placed} coins={battle.Wallet.Coins}");
        }

        [MenuItem("Tools/UnitySlotTower/Automation/Toggle Text Mode (T383)")]
        private static void ToggleTextMode() => GameSession.SetTextMode(!GameSession.TextMode);

        private static bool TryPlace(BattleController battle, BattleMap map)
        {
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (!battle.Deployment.CanPlace(cell))
                    {
                        continue;
                    }

                    battle.Deployment.SetCursor(cell);
                    if (battle.Deployment.ChooseCell())
                    {
                        return battle.Deployment.Confirm() == SummonResult.Summoned;
                    }
                }
            }

            return false;
        }
    }
}
