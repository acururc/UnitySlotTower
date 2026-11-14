using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// ゲーム1回分の進行（plan 16.8）。<see cref="RunState"/> を作ってルートを生成し、ルート → 各マス → ルート を回す。
    /// 戦闘マスは <see cref="BattleController.BeginNode"/> に渡す。リトライは Game の再読み込みで作り直す。
    /// 戦闘マスが終わるとルート画面を開き、筐体のルート選びのモードで止めたリールの方向のマスへ進む（D-75）。
    /// 戦闘以外のマスの画面は T350c まで仮で、回復マスは入った時点で回復、ショップは何もせずにルート画面へ戻る。
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private TextAsset routeRulesTable;
        [SerializeField] private BattleController battle;
        [Tooltip("コインを RunState と共有し、ルート選びにも使うスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;
        [SerializeField] private RouteScreen routeScreen;

        private readonly List<RouteNode> _visited = new List<RouteNode>();
        private readonly bool[] _directions = new bool[SlotMachine.ReelCount];
        private IRandom _random;
        private bool _choosingRoute;

        /// <summary>テスト用: 設定するとルートの乱数をこのシードで作る（TearDown で null に戻す）。</summary>
        public static ulong? SeedOverride { get; set; }

        /// <summary>ゲーム1回分の状態。Start まで null。</summary>
        public RunState State { get; private set; }

        public RouteRules Rules { get; private set; }

        /// <summary>入ったマス（入った順、1階の最初の戦闘から）。</summary>
        public IReadOnlyList<RouteNode> Visited => _visited;

        /// <summary>マスに入った（戦闘以外も含む）。</summary>
        public event Action<RouteNode> NodeEntered;

        /// <summary>ルート画面で次のマスを選んでいる（行き先のリールを止めて進むまで）。</summary>
        public bool IsChoosingRoute => _choosingRoute;

        /// <summary>ルート画面を開いた。</summary>
        public event Action RouteShown;

        private void Start()
        {
            if (battle == null || slotCabinet == null || slotCabinet.Wallet == null || routeScreen == null)
            {
                Debug.LogError("戦闘の進行・スロット筐体・ルート画面のどれかがない", this);
                enabled = false;
                return;
            }

            try
            {
                battle.Initialize();
                if (routeRulesTable == null)
                {
                    throw new InvalidOperationException($"{RouteRules.FileName} が設定されていない");
                }

                Rules = RouteRules.Load(CsvParser.Parse(RouteRules.FileName, routeRulesTable.text));
                _random = new SeededRandom(SeedOverride ?? (ulong)DateTime.UtcNow.Ticks);
                // コインは筐体と共有し、召喚・強化は所持金で払う（plan 16.2）。
                State = new RunState(battle.Config, battle.UpgradeTable, slotCabinet.Rules, slotCabinet.Setting);
                slotCabinet.UseWallet(State.Coins);
                State.BeginRoute(RouteGenerator.Generate(Rules, _random));
                battle.NodeCompleted += OnNodeCompleted;
                slotCabinet.RouteReelChosen += OnRouteReelChosen;
                routeScreen.ArrowClicked += OnArrowClicked;
                routeScreen.Hide();
                Enter(State.Route.Start);
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
                battle.NodeCompleted -= OnNodeCompleted;
            }

            if (slotCabinet != null)
            {
                slotCabinet.RouteReelChosen -= OnRouteReelChosen;
            }

            if (routeScreen != null)
            {
                routeScreen.ArrowClicked -= OnArrowClicked;
            }
        }

        private void OnNodeCompleted() => ShowRoute();

        // 今のマスから先を、ルート画面と筐体のルート選びのモードで選ばせる。
        private void ShowRoute()
        {
            var current = State.Current;
            var any = false;
            for (var reel = 0; reel < _directions.Length; reel++)
            {
                _directions[reel] = current.NextOnReel(reel) != null;
                any |= _directions[reel];
            }

            if (!any)
            {
                Debug.LogError($"{current.Floor}階のマスから先がない", this);
                return;
            }

            _choosingRoute = true;
            routeScreen.Show(State, _visited);
            slotCabinet.BeginRouteChoice(_directions);
            RouteShown?.Invoke();
        }

        private void OnArrowClicked(int reel)
        {
            if (_choosingRoute)
            {
                slotCabinet.PickRouteReel(reel);
            }
        }

        private void OnRouteReelChosen(int reel)
        {
            // 筐体のルート選びはこの進行が始めたときだけ受け取る。
            if (!_choosingRoute)
            {
                return;
            }

            var next = State.Current.NextOnReel(reel);
            if (next == null)
            {
                Debug.LogError($"リール {reel} の先にマスがない", this);
                ShowRoute();
                return;
            }

            _choosingRoute = false;
            routeScreen.Hide();
            if (!Enter(next))
            {
                // 戦闘以外のマスの画面は T350c。今は済ませてすぐルート画面に戻る。
                ShowRoute();
            }
        }

        /// <summary>マスに入る。戦闘マスなら戦闘を始めて true、それ以外はその場で済ませて false。</summary>
        private bool Enter(RouteNode node)
        {
            if (!State.Enter(node))
            {
                throw new InvalidOperationException($"{node.Floor}階 {node.Column}列のマスには進めない");
            }

            _visited.Add(node);
            NodeEntered?.Invoke(node);
            var kind = RouteNodeKinds.Get(node.Kind);
            if (kind.IsBattle)
            {
                battle.BeginNode(State, node);
                return true;
            }

            if (kind.Id == RouteNodeKinds.Rest)
            {
                State.Heal(Rules.RestHeal);
            }

            return false;
        }
    }
}
