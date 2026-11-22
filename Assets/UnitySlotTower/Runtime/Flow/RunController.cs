using System;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// ゲーム1回分の進行（spec 002 FR-101）。<see cref="RunState"/> を作り、ステージ1から最後のステージまで順に
    /// <see cref="BattleController.BeginStage"/> で戦わせる。ステージをクリアすると勝利の帯の後で次のステージへ進む
    /// （<see cref="BattleController.NodeCompleted"/>）。最後のステージのクリアとライフ0はリザルト（<see cref="ResultScreen"/>）が拾う。
    /// リトライは Game の再読み込みで作り直す。
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [Tooltip("コインを RunState と共有するスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;

        /// <summary>ゲーム1回分の状態。Start まで null。</summary>
        public RunState State { get; private set; }

        /// <summary>ステージを始めた（引数はステージの番号）。</summary>
        public event Action<int> StageBegan;

        private void Start()
        {
            if (battle == null || slotCabinet == null || slotCabinet.Wallet == null)
            {
                Debug.LogError("戦闘の進行かスロット筐体がない", this);
                enabled = false;
                return;
            }

            try
            {
                battle.Initialize();
                // コインは筐体と共有し、召喚・強化は所持金で払う（plan 16.2）。
                State = new RunState(battle.Config, battle.UpgradeTable, slotCabinet.Rules);
                slotCabinet.UseWallet(State.Coins);
                battle.NodeCompleted += OnStageCompleted;
                BeginStage();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                battle.enabled = false;
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (battle != null)
            {
                battle.NodeCompleted -= OnStageCompleted;
            }
        }

        // 最後以外のステージをクリアして勝利の帯が消えた。持ち越す状態はそのままで次のステージへ（FR-105）。
        private void OnStageCompleted()
        {
            State.AdvanceStage();
            BeginStage();
        }

        private void BeginStage()
        {
            battle.BeginStage(State);
            StageBegan?.Invoke(State.StageIndex);
        }
    }
}
