using TMPro;
using UnityEngine;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 回復マスの画面（plan 16.7、FR-017、AC-012）。入った時点で <see cref="RunController"/> が回復を済ませ、
    /// この画面は回復した量と今のライフを見せて ◀ だけを置く。文字あり版では回復量を文でも出す（plan 16.13）。
    /// </summary>
    public sealed class RestScreen : NodeScreen
    {
        [SerializeField] private TMP_Text healedText;
        [SerializeField] private TMP_Text lifeText;
        [Tooltip("文字あり版で回復量を文で出すラベル（FR-006、なくてもよい）。")]
        [SerializeField] private TextModeLabel healedLabel;

        public override string Kind => RouteNodeKinds.Rest;

        /// <summary>回復量の文（文字あり版、テスト用）。</summary>
        public TextModeLabel HealedLabel => healedLabel;

        protected override void OnOpen(RunController run, RouteNode node)
        {
            healedText.SetText("+{0}", run.LastHealed);
            lifeText.SetText("{0}", run.State.Life);
            if (healedLabel == null)
            {
                return;
            }

            if (run.LastHealed > 0)
            {
                healedLabel.SetFormat("rest_healed", run.LastHealed);
            }
            else
            {
                healedLabel.SetIds("rest_full");
            }
        }
    }
}
