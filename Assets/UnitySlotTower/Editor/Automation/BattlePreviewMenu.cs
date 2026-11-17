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

        /// <summary>
        /// 札の4状態と詳細の板を撮るための下ごしらえ（T384）。Stage Battle Preview の後に実行する。待ち時間を消し、
        /// 2番目の職を1体下げ、3番目の職をもう1体置いて待ち時間にし、4番目の職に無料ストックを1つ足し、所持金を ¥110 にして、最初の味方を選ぶ。
        /// 札は「1番目: 所持金不足、2番目: 置ける、3番目: 待ち時間、4番目: 無料ストック」になる（units.csv の値段による）。
        /// </summary>
        [MenuItem("Tools/UnitySlotTower/Automation/Stage Card States (T384)")]
        private static void StageCardStates()
        {
            var battle = Object.FindAnyObjectByType<BattleController>();
            if (!EditorApplication.isPlaying || battle == null || battle.Run == null || battle.Field.Units.Count == 0)
            {
                Debug.LogError("[BattlePreview] Play 中に Stage Battle Preview (T383) の後で実行する。");
                return;
            }

            var units = battle.Units.All;
            battle.Summoner.Advance(100000);
            battle.Wallet.Add(2000);
            // 配置上限で札が暗くならないよう、2番目の職の味方を1体下げてから3番目の職をもう1体置く。
            foreach (var placed in battle.Field.Units)
            {
                if (units.Count > 1 && placed.Definition.Id == units[1].Id)
                {
                    battle.Deployment.Retreat(placed);
                    break;
                }
            }

            if (units.Count > 2 && battle.Deployment.Select(units[2].Id) == SummonResult.Summoned && !TryPlace(battle, battle.Run.Map))
            {
                battle.Deployment.Cancel();
            }

            if (units.Count > 3)
            {
                battle.Run.State.Stock.Add(units[3].Id, 1);
            }

            battle.Wallet.TrySpend(Mathf.Max(0, battle.Wallet.Coins - 110));
            var input = Object.FindAnyObjectByType<UnitPlacementInput>();
            typeof(UnitPlacementInput).GetProperty(nameof(UnitPlacementInput.SelectedUnit))
                .SetValue(input, battle.Field.Units[0]);
            Debug.Log($"[BattlePreview] card states coins={battle.Wallet.Coins} selected={battle.Field.Units[0].Definition.Id}");
        }

        private static int _helpSlot;

        /// <summary>
        /// スキルの説明を撮る下ごしらえ（T385）。一時停止して、札の順に1つずつ（実行するたびに次の職）ゲージを押したときと同じ説明を開く。
        /// </summary>
        [MenuItem("Tools/UnitySlotTower/Automation/Stage Skill Help (T385)")]
        private static void StageSkillHelp()
        {
            var battle = Object.FindAnyObjectByType<BattleController>();
            if (!EditorApplication.isPlaying || battle == null || battle.Run == null)
            {
                Debug.LogError("[BattlePreview] Play 中の戦闘で実行する。");
                return;
            }

            battle.SetPaused(true);
            var slot = _helpSlot++ % 4;
            var icon = GameObject.Find($"Unit Icon {slot + 1}")?.GetComponent<UnitBarIcon>();
            typeof(UnitBarIcon).GetMethod("ActivateSkill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(icon, null);
            Debug.Log($"[BattlePreview] skill help slot={slot + 1} open={Object.FindAnyObjectByType<SkillHelpPanel>()?.IsOpen}");
        }

        /// <summary>向きを選んでいる所（ブロック数の印）を撮る下ごしらえ（T385）。一時停止を解き、最初の職を選んで置けるマスで向き選びにする。</summary>
        [MenuItem("Tools/UnitySlotTower/Automation/Stage Facing Badge (T385)")]
        private static void StageFacingBadge()
        {
            var battle = Object.FindAnyObjectByType<BattleController>();
            if (!EditorApplication.isPlaying || battle == null || battle.Run == null)
            {
                Debug.LogError("[BattlePreview] Play 中の戦闘で実行する。");
                return;
            }

            Object.FindAnyObjectByType<SkillHelpPanel>()?.Close();
            battle.SetPaused(false);
            battle.Summoner.Advance(100000);
            battle.Wallet.Add(2000);
            var map = battle.Run.Map;
            if (battle.Deployment.Select(battle.Units.All[0].Id) != SummonResult.Summoned)
            {
                Debug.LogError("[BattlePreview] 最初の職を選べない。");
                return;
            }

            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (battle.Deployment.CanPlace(cell))
                    {
                        battle.Deployment.SetCursor(cell);
                        Debug.Log($"[BattlePreview] facing badge chosen={battle.Deployment.ChooseCell()} cell={x},{y}");
                        return;
                    }
                }
            }
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
