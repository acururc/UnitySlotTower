using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Core.Guide
{
    /// <summary>文字なし説明（FR-004、T403）の題目。1回のプレイでそれぞれ一度だけ説明する。</summary>
    public enum GuideTopic
    {
        ReelCustom,
        /// <summary>召喚・配置・向き（最初の1体を置くまで）。</summary>
        Deploy,
        Slot,
        Bonus,
        Upgrade,
        Retreat,
    }

    /// <summary>いま見せる説明（対象と手の動き）。</summary>
    public enum GuideHint
    {
        None,
        ReelCustom,
        Summon,
        Place,
        Facing,
        Lever,
        Stop,
        Bonus,
        Upgrade,
        SelectUnit,
        Retreat,
    }

    /// <summary>説明の進行を決めるための、そのフレームのゲームの様子。</summary>
    public struct GuideObservation
    {
        public bool Customizing;
        public bool Paused;
        public bool Finished;
        /// <summary>ウェーブの準備中（▶ の前）。</summary>
        public bool Preparing;
        /// <summary>ステージ内のウェーブ番号（1 から）。</summary>
        public int Wave;
        public DeployPhase Deploy;
        /// <summary>操作バーの先頭の味方をいま召喚できる。</summary>
        public bool CanSummon;
        public int FieldUnits;
        /// <summary>撤退の対象として配置中の味方を選んでいる。</summary>
        public bool UnitSelected;
        public bool SlotSpinning;
        /// <summary>このプレイでリールを回した回数。</summary>
        public int SpinCount;
        /// <summary>レバーを引ける（コインが足りる）。</summary>
        public bool CanLever;
        public bool BonusActive;
        /// <summary>配置中の味方のうち、いま強化できる種類の操作バーの位置（0 始まり）。なければ -1。</summary>
        public int UpgradeSlot;
        /// <summary>全種類の強化段階の合計（強化したかを見る）。</summary>
        public int UpgradeLevels;
    }

    /// <summary>
    /// 文字なし説明の進行（FR-004、T403、plan 8章）。毎フレーム様子を受け取り、見せる説明を返す。
    /// 各題目は、その操作を初めて使う場面で一度だけ出し、示した操作を実際に行うと終わる。説明中は戦闘の時間を止める
    /// （<see cref="HoldsBattle"/>）。示した操作ができなくなったら（コイン不足など）終えずに引っ込め、また条件がそろえば出す。
    /// 一時停止中は隠すだけで進めない。ゲームを読み込み直す（リトライ）と最初から（ユーザー決定: 毎プレイ）。
    /// </summary>
    public sealed class GuideSequence
    {
        private readonly bool[] _done = new bool[6];
        private GuideTopic? _active;
        private GuideObservation _baseline;

        /// <summary>最後の <see cref="Update"/> で決めた説明。</summary>
        public GuideHint Current { get; private set; }

        /// <summary>強化の説明で示す操作バーの位置（0 始まり）。</summary>
        public int TargetSlot { get; private set; } = -1;

        /// <summary>説明を見せているあいだは戦闘の時間を止める（スロットは動かす）。</summary>
        public bool HoldsBattle => Current != GuideHint.None;

        public bool IsDone(GuideTopic topic) => _done[(int)topic];

        public GuideHint Update(in GuideObservation o)
        {
            if (o.Finished)
            {
                _active = null;
                return Current = GuideHint.None;
            }

            if (o.Paused)
            {
                return Current = GuideHint.None;
            }

            if (_active is GuideTopic active)
            {
                if (IsCompleted(active, o))
                {
                    _done[(int)active] = true;
                    _active = null;
                }
                else if (IsWithdrawn(active, o))
                {
                    _active = null;
                }
            }

            if (_active == null && Next(o) is GuideTopic next)
            {
                _active = next;
                _baseline = o;
                TargetSlot = o.UpgradeSlot;
            }

            return Current = _active is GuideTopic shown ? HintFor(shown, o) : GuideHint.None;
        }

        private bool IsCompleted(GuideTopic topic, in GuideObservation o)
        {
            switch (topic)
            {
                case GuideTopic.ReelCustom:
                    return !o.Customizing;
                case GuideTopic.Deploy:
                    return o.FieldUnits > _baseline.FieldUnits;
                case GuideTopic.Slot:
                    return o.SpinCount > _baseline.SpinCount && !o.SlotSpinning;
                case GuideTopic.Bonus:
                    return o.SpinCount > _baseline.SpinCount;
                case GuideTopic.Upgrade:
                    return o.UpgradeLevels > _baseline.UpgradeLevels;
                case GuideTopic.Retreat:
                    // ▶ で始めたら撤退しないことを選んだとみなす。
                    return o.FieldUnits < _baseline.FieldUnits || o.FieldUnits == 0 || !o.Preparing;
                default:
                    return true;
            }
        }

        private bool IsWithdrawn(GuideTopic topic, in GuideObservation o)
        {
            if (o.Customizing && topic != GuideTopic.ReelCustom)
            {
                return true;
            }

            switch (topic)
            {
                case GuideTopic.Deploy:
                    return o.Deploy == DeployPhase.Idle && !o.CanSummon;
                case GuideTopic.Slot:
                    return !o.SlotSpinning && !o.CanLever && o.SpinCount == _baseline.SpinCount;
                case GuideTopic.Bonus:
                    return !o.SlotSpinning && (!o.CanLever || !o.BonusActive);
                case GuideTopic.Upgrade:
                    return o.UpgradeSlot < 0;
                default:
                    return false;
            }
        }

        private GuideTopic? Next(in GuideObservation o)
        {
            if (o.Customizing)
            {
                return IsDone(GuideTopic.ReelCustom) ? (GuideTopic?)null : GuideTopic.ReelCustom;
            }

            var idle = o.Deploy == DeployPhase.Idle;
            if (!IsDone(GuideTopic.Deploy))
            {
                return o.CanSummon || !idle ? GuideTopic.Deploy : (GuideTopic?)null;
            }

            if (!IsDone(GuideTopic.Slot) && idle && (o.CanLever || o.SlotSpinning))
            {
                return GuideTopic.Slot;
            }

            if (!IsDone(GuideTopic.Bonus) && idle && o.BonusActive && !o.SlotSpinning && o.CanLever)
            {
                return GuideTopic.Bonus;
            }

            if (!IsDone(GuideTopic.Upgrade) && IsDone(GuideTopic.Slot) && idle && o.UpgradeSlot >= 0)
            {
                return GuideTopic.Upgrade;
            }

            if (!IsDone(GuideTopic.Retreat) && idle && o.Preparing && o.Wave >= 2 && o.FieldUnits > 0)
            {
                return GuideTopic.Retreat;
            }

            return null;
        }

        private static GuideHint HintFor(GuideTopic topic, in GuideObservation o)
        {
            switch (topic)
            {
                case GuideTopic.ReelCustom:
                    return GuideHint.ReelCustom;
                case GuideTopic.Deploy:
                    return o.Deploy == DeployPhase.ChoosingCell ? GuideHint.Place
                        : o.Deploy == DeployPhase.ChoosingFacing ? GuideHint.Facing
                        : GuideHint.Summon;
                case GuideTopic.Slot:
                    return o.SlotSpinning ? GuideHint.Stop : GuideHint.Lever;
                case GuideTopic.Bonus:
                    return GuideHint.Bonus;
                case GuideTopic.Upgrade:
                    return GuideHint.Upgrade;
                case GuideTopic.Retreat:
                    return o.UnitSelected ? GuideHint.Retreat : GuideHint.SelectUnit;
                default:
                    return GuideHint.None;
            }
        }
    }
}
