using System.Collections.Generic;

namespace UnitySlotTower.Core.Run
{
    /// <summary>
    /// ステージの開始の演出に出す印の種類（旧ルートのマスの種類の名残。ルートは spec 002 FR-117 で削除）。
    /// id は <c>ui_texts.csv</c> の <c>node_&lt;id&gt;</c> と Runtime の絵（<c>RouteNodeIcons</c>）のキー。
    /// </summary>
    public static class RouteNodeKinds
    {
        public const string First = "first";
        public const string Battle = "battle";
        public const string Elite = "elite";
        public const string Boss = "boss";

        private static readonly string[] Kinds = { First, Battle, Elite, Boss };

        /// <summary>登録順。</summary>
        public static IReadOnlyList<string> All => Kinds;
    }
}
