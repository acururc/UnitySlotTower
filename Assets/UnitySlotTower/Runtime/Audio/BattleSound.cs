using UnityEngine;
using UnitySlotTower.Core.Audio;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Battle;
using UnitySlotTower.Slot;

namespace UnitySlotTower
{
    /// <summary>
    /// 戦闘とスロットの出来事に音を付ける（spec 014 FR-001〜FR-005）。既存のイベントを購読し、
    /// イベントのない変化（ウェーブ開始・ボスの登場・勝利・敗北・一時停止）は毎フレームの比較で拾う。
    /// </summary>
    public sealed class BattleSound : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [SerializeField] private SlotCabinet slotCabinet;
        [SerializeField] private BattleActorsView actorsView;

        private Battlefield _field;
        private int _startedWaves;
        private bool _bossSeen;
        private bool _wasVictory;
        private bool _wasGameOver;
        private int _reelsStopped;

        /// <summary>Editor ツールとテスト用。</summary>
        public void Bind(BattleController battleController, SlotCabinet cabinet, BattleActorsView actors)
        {
            battle = battleController;
            slotCabinet = cabinet;
            actorsView = actors;
        }

        private void OnEnable()
        {
            if (battle != null)
            {
                battle.NodeBegan += OnNodeBegan;
                battle.RedStruck += OnRed;
                battle.BlueFroze += OnBlue;
                battle.GreenHealed += OnGreen;
                battle.YellowBurst += OnYellow;
                battle.BarBurst += OnBar;
                battle.CoinGained += OnCoin;
                battle.UnitAttacked += OnUnitAttacked;
                battle.EnemyDefeated += OnEnemyDefeated;
            }

            if (slotCabinet != null)
            {
                slotCabinet.BetChanged += OnBet;
                slotCabinet.LeverPulled += OnLever;
                slotCabinet.LeverRefused += OnReject;
                slotCabinet.ReelStopped += OnReelStopped;
                slotCabinet.Settled += OnSettled;
                slotCabinet.Aligned += OnAligned;
            }

            if (actorsView != null)
            {
                actorsView.Impact += OnImpact;
            }

            // 有効になる前に始まっていたステージにもつなぐ。
            if (battle != null && battle.Field != null) OnNodeBegan();
        }

        private void OnDisable()
        {
            if (battle != null)
            {
                battle.NodeBegan -= OnNodeBegan;
                battle.RedStruck -= OnRed;
                battle.BlueFroze -= OnBlue;
                battle.GreenHealed -= OnGreen;
                battle.YellowBurst -= OnYellow;
                battle.BarBurst -= OnBar;
                battle.CoinGained -= OnCoin;
                battle.UnitAttacked -= OnUnitAttacked;
                battle.EnemyDefeated -= OnEnemyDefeated;
            }

            if (slotCabinet != null)
            {
                slotCabinet.BetChanged -= OnBet;
                slotCabinet.LeverPulled -= OnLever;
                slotCabinet.LeverRefused -= OnReject;
                slotCabinet.ReelStopped -= OnReelStopped;
                slotCabinet.Settled -= OnSettled;
                slotCabinet.Aligned -= OnAligned;
            }

            if (actorsView != null)
            {
                actorsView.Impact -= OnImpact;
            }

            ObserveField(null);
            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.StopLoop();
                GameAudio.Instance.SetSePaused(false);
            }
        }

        private void OnNodeBegan()
        {
            ObserveField(battle.Field);
            _startedWaves = 0;
            _bossSeen = false;
            _wasVictory = false;
            _wasGameOver = false;
            var boss = battle.NodeKind == RouteNodeKinds.Elite || battle.NodeKind == RouteNodeKinds.Boss;
            GameAudio.PlayBgm(boss ? BgmTrack.Boss : BgmTrack.Battle);
        }

        private void ObserveField(Battlefield field)
        {
            if (_field != null) _field.BaseReached -= OnBaseReached;
            _field = field;
            if (_field != null) _field.BaseReached += OnBaseReached;
        }

        private void Update()
        {
            if (battle == null || battle.Run == null)
            {
                return;
            }

            var audio = GameAudio.Instance;
            if (audio != null && audio.IsSePaused != battle.IsPaused)
            {
                audio.SetSePaused(battle.IsPaused);
            }

            var field = battle.Field;
            if (field.Waves.StartedWaves > _startedWaves)
            {
                _startedWaves = field.Waves.StartedWaves;
                GameAudio.Play(SoundId.WaveStart);
            }

            if (!_bossSeen)
            {
                foreach (var enemy in field.Enemies)
                {
                    if (enemy.IsBoss)
                    {
                        _bossSeen = true;
                        GameAudio.Play(SoundId.BossAppear);
                        break;
                    }
                }
            }

            var victory = battle.IsShowingVictory;
            if (victory && !_wasVictory) EndStage(SoundId.Victory);
            _wasVictory = victory;

            var gameOver = battle.Run.Phase == WavePhase.GameOver;
            if (gameOver && !_wasGameOver) EndStage(SoundId.Defeat);
            _wasGameOver = gameOver;
        }

        // 勝利・敗北: BGM を止めてジングル（FR-004）。
        private static void EndStage(SoundId jingle)
        {
            if (GameAudio.Instance != null) GameAudio.Instance.StopLoop();
            GameAudio.PlayBgm(BgmTrack.None);
            GameAudio.Play(jingle);
        }

        private static void OnBet() => GameAudio.Play(SoundId.Bet);

        private void OnLever()
        {
            _reelsStopped = 0;
            GameAudio.Play(SoundId.Lever);
            if (GameAudio.Instance != null) GameAudio.Instance.StartLoop(SoundId.ReelSpin);
        }

        private static void OnReject() => GameAudio.Play(SoundId.Reject);

        private void OnReelStopped(int reel)
        {
            GameAudio.Play(SoundId.ReelStop);
            if (++_reelsStopped >= SlotMachine.ReelCount && GameAudio.Instance != null)
            {
                GameAudio.Instance.StopLoop();
            }
        }

        private static void OnSettled(int hits)
        {
            if (GameAudio.Instance != null) GameAudio.Instance.StopLoop();
            if (hits == 0) GameAudio.Play(SoundId.Miss);
        }

        // 揃いの電子音に上昇音を重ね、同じ回転の2本目・3本目ほど高くする（spec 015 FR-023、全音ずつ）。
        private void OnAligned(SlotSymbol symbol)
        {
            var index = slotCabinet != null ? Mathf.Clamp(slotCabinet.LastAlignedIndex, 1, 3) : 1;
            var pitch = Mathf.Pow(2f, (index - 1) * 2f / 12f);
            GameAudio.Play(SoundId.LineHit, pitch);
            GameAudio.Play(SoundId.LineFanfare, pitch);
        }

        private static void OnRed() => GameAudio.Play(SoundId.Red7);
        private static void OnBlue() => GameAudio.Play(SoundId.Blue7);
        private static void OnGreen() => GameAudio.Play(SoundId.Green7);
        private static void OnYellow() => GameAudio.Play(SoundId.Yellow7);
        private static void OnBar() => GameAudio.Play(SoundId.Bar);
        private static void OnCoin() => GameAudio.Play(SoundId.CoinSymbol);

        private static void OnUnitAttacked(BattleUnit unit, BattleEnemy enemy)
        {
            switch (unit.Definition.Id)
            {
                case "sword":
                    GameAudio.Play(SoundId.SwordAttack);
                    GameAudio.Play(SoundId.Hit);
                    break;
                case "archer":
                    GameAudio.Play(SoundId.BowAttack);
                    break;
            }
        }

        private static void OnImpact(Vector3 position, bool arrow)
        {
            if (arrow) GameAudio.Play(SoundId.Hit);
        }

        private static void OnEnemyDefeated(BattleEnemy enemy, int money, int coins)
        {
            GameAudio.Play(enemy.IsBoss ? SoundId.BossDefeat : SoundId.EnemyDefeat);
            if (coins > 0) GameAudio.Play(SoundId.CoinGet);
        }

        private static void OnBaseReached(GridPoint cell) => GameAudio.Play(SoundId.BaseHit);
    }
}
