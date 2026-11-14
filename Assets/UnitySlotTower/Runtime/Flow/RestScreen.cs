using TMPro;
using UnityEngine;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 回復マスの画面（plan 16.7、FR-017、AC-012）。入った時点で <see cref="RunController"/> が回復を済ませ、
    /// この画面は回復した量と今のライフを見せて ◀ だけを置く。
    /// </summary>
    public sealed class RestScreen : NodeScreen
    {
        [SerializeField] private TMP_Text healedText;
        [SerializeField] private TMP_Text lifeText;

        public override string Kind => RouteNodeKinds.Rest;

        protected override void OnOpen(RunController run, RouteNode node)
        {
            healedText.SetText("+{0}", run.LastHealed);
            lifeText.SetText("{0}", run.State.Life);
        }
    }
}
