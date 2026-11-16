using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Core.Guide
{
    /// <summary>文字なし説明（FR-004、plan 16.10）の題目。1回のプレイでそれぞれ一度だけ説明する。</summary>
    public enum GuideTopic
    {
        /// <summary>召喚・配置・向き（最初の1体を置くまで）。</summary>
        Deploy,
        /// <summary>レバーを引いてリールが止まるまで。</summary>
        Slot,
        /// <summary>初めてゲージが満タンになり発動できるとき、そのゲージを押すまで。</summary>
        Skill,
        Upgrade,
        Retreat,
        /// <summary>初めてルート画面に戻ったとき、行き先を決めるまで。</summary>
        Route,
    }

    /// <summary>いま見せる説明（対象と手の動き）。</summary>
    public enum GuideHint
    {
        None,
        Summon,
        Place,
        Facing,
        Lever,
        /// <summary>リールが自動で止まるまでリール窓を見せる。</summary>
        Spinning,
        Skill,
        Upgrade,
        SelectUnit,
        Retreat,
        /// <summary>ルート選びでレバーを引く。</summary>
        RouteLever,
        /// <summary>回っているリールのうち行き先のあるものを押す。</summary>
        RoutePick,
    }

    /// <summary>説明の進行を決めるための、そのフレームのゲームの様子。</summary>
    public struct GuideObservation
    {
        public bool Paused;
        public bool Finished;
        /// <summary>戦闘以外のマスの画面（ショップ・回復）を開いている。説明は出さない。</summary>
        public bool OnScreen;
        /// <summary>ルート画面で行き先を選んでいる。</summary>
        public bool ChoosingRoute;
        /// <summary>ルート選びのリールが回っている。</summary>
        public bool RouteSpinning;
        /// <summary>戦闘マスの開始の演出中（plan 17.2）。説明は出さない。</summary>
        public bool Intro;
        /// <summary>このプレイで何番目の戦闘マスか（1 から）。</summary>
        public int BattleNode;
        public DeployPhase Deploy;
        /// <summary>操作バーの先頭の味方をいま召喚できる。</summary>
        public bool CanSummon;
        public int FieldUnits;
        /// <summary>撤退の対象として配置中の味方を選んでいる。</summary>
        public bool UnitSelected;
        public bool SlotSpinning;
        /// <summary>このプレイでリールを回した回数。</summary>
        public int SpinCount;
        /// <summary>レバーを引ける（コインか回数固定が足りる）。</summary>
        public bool CanLever;
        /// <summary>ゲージが満タンで、いま発動できる職の操作バーの位置（0 始まり）。なければ -1。</summary>
        public int SkillSlot;
        /// <summary>このプレイでスキルを発動した回数。</summary>
        public int SkillActivations;
        /// <summary>配置中の味方のうち、いま強化できる種類の操作バーの位置（0 始まり）。なければ -1。</summary>
        public int UpgradeSlot;
        /// <summary>全種類の強化段階の合計（強化したかを見る）。</summary>
        public int UpgradeLevels;
    }

    /// <summary>
    /// 文字なし説明の進行（FR-004、plan 16.10）。毎フレーム様子を受け取り、見せる説明を返す。
    /// 各題目は、その操作を初めて使う場面で一度だけ出し、示した操作を実際に行うと終わる。説明中は戦闘の時間を止める
    /// （<see cref="HoldsBattle"/>）。示した操作ができなくなったら（コイン不足など）終えずに引っ込め、また条件がそろえば出す。
    /// 一時停止中・ショップなどの画面中・戦闘マスの開始の演出中は隠すだけで進めない。ルート選びのあいだは戦闘の題目を出さない。
    /// ゲームを読み込み直す（リトライ）と最初から（ユーザー決定: 毎プレイ）。
    /// </summary>
    public sealed class GuideSequence
    {
        private readonly bool[] _done = new bool[6];
        private GuideTopic? _active;
        private GuideObservation _baseline;

        /// <summary>最後の <see cref="Update"/> で決めた説明。</summary>
        public GuideHint Current { get; private set; }

        /// <summary>スキル・強化の説明で示す操作バーの位置（0 始まり）。</summary>
        public int TargetSlot { get; private set; } = -1;

        /// <summary>説明を見せているあいだは戦闘の時間を止める（スロットは動かす）。</summary>
        public bool HoldsBattle => Current != GuideHint.None;

        /// <summary>撤退の題目を表示中（D-56: このあいだの撤退は全額返還）。</summary>
        public bool ShowsRetreat => _active == GuideTopic.Retreat && Current != GuideHint.None;

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

            // 画面中・開始の演出中は新しい題目を始めない（ルートで行き先を決めてショップに入った、は上でルートの完了になる）。
            if (o.OnScreen || o.Intro)
            {
                return Current = GuideHint.None;
            }

            if (_active == null && Next(o) is GuideTopic next)
            {
                _active = next;
                _baseline = o;
                TargetSlot = next == GuideTopic.Skill ? o.SkillSlot : o.UpgradeSlot;
            }

            return Current = _active is GuideTopic shown ? HintFor(shown, o) : GuideHint.None;
        }

        private bool IsCompleted(GuideTopic topic, in GuideObservation o)
        {
            switch (topic)
            {
                case GuideTopic.Deploy:
                    return o.FieldUnits > _baseline.FieldUnits;
                case GuideTopic.Slot:
                    return o.SpinCount > _baseline.SpinCount && !o.SlotSpinning;
                case GuideTopic.Skill:
                    return o.SkillActivations > _baseline.SkillActivations;
                case GuideTopic.Upgrade:
                    return o.UpgradeLevels > _baseline.UpgradeLevels;
                case GuideTopic.Retreat:
                    // 撤退せずにレバーを引くか次の召喚を始めたら、撤退しないことを選んだとみなす（▶ の代わり、plan 17.2）。
                    return o.FieldUnits < _baseline.FieldUnits || o.FieldUnits == 0 || o.SpinCount > _baseline.SpinCount
                        || o.Deploy != DeployPhase.Idle;
                case GuideTopic.Route:
                    return !o.ChoosingRoute;
                default:
                    return true;
            }
        }

        private bool IsWithdrawn(GuideTopic topic, in GuideObservation o)
        {
            if ((o.ChoosingRoute || o.OnScreen) && topic != GuideTopic.Route)
            {
                return true;
            }

            switch (topic)
            {
                case GuideTopic.Deploy:
                    return o.Deploy == DeployPhase.Idle && !o.CanSummon;
                case GuideTopic.Slot:
                    return !o.SlotSpinning && !o.CanLever && o.SpinCount == _baseline.SpinCount;
                case GuideTopic.Skill:
                    return o.SkillSlot < 0;
                case GuideTopic.Upgrade:
                    return o.UpgradeSlot < 0;
                default:
                    return false;
            }
        }

        private GuideTopic? Next(in GuideObservation o)
        {
            if (o.ChoosingRoute)
            {
                return IsDone(GuideTopic.Route) ? (GuideTopic?)null : GuideTopic.Route;
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

            if (!IsDone(GuideTopic.Skill) && idle && !o.SlotSpinning && o.SkillSlot >= 0)
            {
                return GuideTopic.Skill;
            }

            if (!IsDone(GuideTopic.Upgrade) && IsDone(GuideTopic.Slot) && idle && o.UpgradeSlot >= 0)
            {
                return GuideTopic.Upgrade;
            }

            // 2つ目の戦闘マスで、味方が配置されていて操作がないとき（plan 17.2）。
            if (!IsDone(GuideTopic.Retreat) && idle && !o.SlotSpinning && o.BattleNode >= 2 && o.FieldUnits > 0)
            {
                return GuideTopic.Retreat;
            }

            return null;
        }

        private static GuideHint HintFor(GuideTopic topic, in GuideObservation o)
        {
            switch (topic)
            {
                case GuideTopic.Deploy:
                    return o.Deploy == DeployPhase.ChoosingCell ? GuideHint.Place
                        : o.Deploy == DeployPhase.ChoosingFacing ? GuideHint.Facing
                        : GuideHint.Summon;
                case GuideTopic.Slot:
                    return o.SlotSpinning ? GuideHint.Spinning : GuideHint.Lever;
                case GuideTopic.Skill:
                    return GuideHint.Skill;
                case GuideTopic.Upgrade:
                    return GuideHint.Upgrade;
                case GuideTopic.Retreat:
                    return o.UnitSelected ? GuideHint.Retreat : GuideHint.SelectUnit;
                case GuideTopic.Route:
                    return o.RouteSpinning ? GuideHint.RoutePick : GuideHint.RouteLever;
                default:
                    return GuideHint.None;
            }
        }
    }
}
