using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// ゲーム1回分の進行（plan 16.8）。<see cref="RunState"/> を作ってルートを生成し、ルート → 各マス → ルート を回す。
    /// 戦闘マスは <see cref="BattleController.BeginNode"/> に渡す。リトライは Game の再読み込みで作り直す。
    /// T350a の段階ではルート画面がまだないため、戦闘マスが終わると次のマスへ自動で進む（戦闘マスを優先し、
    /// 回復マスは入った時点で回復、ショップは何もせず通り過ぎる）。ルート画面とリール選びは T350b。
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private TextAsset routeRulesTable;
        [SerializeField] private BattleController battle;
        [Tooltip("コインを RunState と共有するスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;

        private readonly List<RouteNode> _visited = new List<RouteNode>();
        private IRandom _random;

        /// <summary>テスト用: 設定するとルートの乱数をこのシードで作る（TearDown で null に戻す）。</summary>
        public static ulong? SeedOverride { get; set; }

        /// <summary>ゲーム1回分の状態。Start まで null。</summary>
        public RunState State { get; private set; }

        public RouteRules Rules { get; private set; }

        /// <summary>入ったマス（入った順、1階の最初の戦闘から）。</summary>
        public IReadOnlyList<RouteNode> Visited => _visited;

        /// <summary>マスに入った（戦闘以外も含む）。</summary>
        public event Action<RouteNode> NodeEntered;

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
        }

        // T350a の仮: 行き先を自動で選ぶ。戦闘マスがあればその最初（列の順）、なければ最初の行き先。
        private void OnNodeCompleted()
        {
            while (true)
            {
                var next = ChooseNext(State.Current);
                if (next == null)
                {
                    Debug.LogError($"{State.Current.Floor}階のマスから先がない", this);
                    return;
                }

                if (Enter(next))
                {
                    return;
                }
            }
        }

        private static RouteNode ChooseNext(RouteNode current)
        {
            foreach (var next in current.Next)
            {
                if (RouteNodeKinds.Get(next.Kind).IsBattle)
                {
                    return next;
                }
            }

            return current.Next.Count > 0 ? current.Next[0] : null;
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
