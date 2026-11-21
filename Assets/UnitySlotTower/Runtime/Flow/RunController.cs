using System;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
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
        [SerializeField] private TextAsset routeRulesTable;
        [SerializeField] private TextAsset shopItemsTable;
        [SerializeField] private BattleController battle;
        [Tooltip("コインを RunState と共有するスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;
        [Tooltip("旧ルート画面（使わない。読み込み時に隠すだけで、T609 で画面ごと消す）。")]
        [SerializeField] private RouteScreen routeScreen;
        [Tooltip("旧ショップ・回復の画面（使わない。読み込み時に隠すだけで、T609 で画面ごと消す）。")]
        [SerializeField] private NodeScreen[] nodeScreens = Array.Empty<NodeScreen>();

        private IRandom _random;

        /// <summary>テスト用: 設定すると乱数をこのシードで作る（TearDown で null に戻す）。</summary>
        public static ulong? SeedOverride { get; set; }

        /// <summary>ゲーム1回分の状態。Start まで null。</summary>
        public RunState State { get; private set; }

        /// <summary>ステージを始めた（引数はステージの番号）。</summary>
        public event Action<int> StageBegan;

        // ここから下は T609 で消す旧ショップ・回復の画面がコンパイルできるようにだけ残す（画面は開かない）。
        public RouteRules Rules { get; private set; }
        public ShopTable ShopTable { get; private set; }
        public int LastHealed => 0;
        public Shop CreateShop() => new Shop(ShopTable, Rules.ShopSlots, State, _random);
        public Exchange CreateExchange() => new Exchange(State, slotCabinet.Bet, Rules.ExchangeMoneyPerCoin);

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
                if (routeRulesTable != null)
                {
                    Rules = RouteRules.Load(CsvParser.Parse(RouteRules.FileName, routeRulesTable.text));
                }

                if (shopItemsTable != null)
                {
                    ShopTable = ShopTable.Load(CsvParser.Parse(ShopTable.FileName, shopItemsTable.text));
                }

                HideOldScreens();
                _random = new SeededRandom(SeedOverride ?? (ulong)DateTime.UtcNow.Ticks);
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

        private void HideOldScreens()
        {
            if (routeScreen != null)
            {
                routeScreen.Hide();
            }

            foreach (var screen in nodeScreens)
            {
                if (screen != null)
                {
                    screen.HideImmediate();
                }
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
