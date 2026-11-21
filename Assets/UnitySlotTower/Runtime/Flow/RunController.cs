using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
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
    /// 戦闘以外のマスはそのマスの処理（回復）を済ませてから、種類に合う画面（<see cref="NodeScreen"/>、Runtime の画面の登録表）を開き、
    /// ◀ で閉じたらルート画面に戻る。画面のない種類はすぐルート画面に戻る。
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private TextAsset routeRulesTable;
        [SerializeField] private TextAsset shopItemsTable;
        [SerializeField] private BattleController battle;
        [Tooltip("コインを RunState と共有し、ルート選びにも使うスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;
        [SerializeField] private RouteScreen routeScreen;
        [Tooltip("戦闘以外のマスの画面（種類ごとに1つ）。")]
        [SerializeField] private NodeScreen[] nodeScreens = Array.Empty<NodeScreen>();

        private readonly List<RouteNode> _visited = new List<RouteNode>();
        private readonly bool[] _directions = new bool[SlotMachine.ReelCount];
        private readonly Dictionary<string, NodeScreen> _screens = new Dictionary<string, NodeScreen>();
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

        public ShopTable ShopTable { get; private set; }

        /// <summary>スキルの表（ショップのゲージの品の色）。</summary>
        public SkillTable Skills => battle.Skills;

        /// <summary>直近の回復マスで実際に回復した量（最大ライフで止まった分は含まない）。</summary>
        public int LastHealed { get; private set; }

        /// <summary>開いている戦闘以外のマスの画面（なければ null）。</summary>
        public NodeScreen OpenScreen { get; private set; }

        /// <summary>このゲームのショップマス1回分を作る（並ぶ品はルートと同じ乱数で選ぶ）。</summary>
        public Shop CreateShop() => new Shop(ShopTable, Rules.ShopSlots, State, battle.Skills, _random);

        /// <summary>ショップの両替（掛け金単位、route_rules の exchange_money_per_coin）。</summary>
        public Exchange CreateExchange() => new Exchange(State, slotCabinet.Bet, Rules.ExchangeMoneyPerCoin);

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
                if (shopItemsTable == null)
                {
                    throw new InvalidOperationException($"{ShopTable.FileName} が設定されていない");
                }

                ShopTable = ShopTable.Load(CsvParser.Parse(ShopTable.FileName, shopItemsTable.text));
                RegisterScreens();
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

            foreach (var screen in _screens.Values)
            {
                if (screen != null)
                {
                    screen.Closed -= OnScreenClosed;
                }
            }
        }

        private void RegisterScreens()
        {
            foreach (var screen in nodeScreens)
            {
                if (screen == null)
                {
                    continue;
                }

                if (!RouteNodeKinds.Contains(screen.Kind))
                {
                    throw new InvalidOperationException($"画面 {screen.name} の種類 '{screen.Kind}' は登録表にない");
                }

                if (!_screens.TryAdd(screen.Kind, screen))
                {
                    throw new InvalidOperationException($"種類 '{screen.Kind}' の画面が2つある");
                }

                screen.HideImmediate();
                screen.Closed += OnScreenClosed;
            }
        }

        private void OnNodeCompleted()
        {
            // T607 でステージの流れに置き換えるまで、戦闘マスを1つ終えるごとに次のステージとして数える（FR-108 の無料はステージ1だけ）。
            State.AdvanceStage();
            ShowRoute();
        }

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

            // 移動のアニメ（D-90）を見せてからそのマスに入る。アニメ中は筐体のレバーも止める。
            _choosingRoute = false;
            slotCabinet.HeldByScreen = true;
            routeScreen.BeginMove(next, () =>
            {
                slotCabinet.HeldByScreen = false;
                routeScreen.Hide();
                if (!Enter(next))
                {
                    OpenNodeScreen(next);
                }
            });
        }

        // 戦闘以外のマスの画面を開く。画面のない種類はすぐルート画面に戻る。
        private void OpenNodeScreen(RouteNode node)
        {
            if (!_screens.TryGetValue(node.Kind, out var screen))
            {
                ShowRoute();
                return;
            }

            OpenScreen = screen;
            // 画面が筐体を覆うので、Space のレバーも止める。
            slotCabinet.HeldByScreen = true;
            screen.Open(this, node);
        }

        private void OnScreenClosed()
        {
            if (OpenScreen == null)
            {
                return;
            }

            OpenScreen = null;
            slotCabinet.HeldByScreen = false;
            ShowRoute();
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
                LastHealed = State.Heal(Rules.RestHeal);
            }

            return false;
        }
    }
}
