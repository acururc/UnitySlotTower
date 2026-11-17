using System;

namespace UnitySlotTower.Core.Layout
{
    /// <summary>画面に対する割合（0〜1、左下原点）。Unity の Camera.rect と同じ向き。</summary>
    public readonly struct NormalizedRect
    {
        public NormalizedRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
    }

    /// <summary>
    /// 縦長 3:4（12:16）の画面配置（plan 2章）。どの解像度でも 3:4 の領域を中央に置き、余白は黒帯にする。
    /// 3:4 領域の上半分が戦闘カメラ、下半分が操作バーとスロット筐体（2026-11-17 D-87・T368 で 6:4 から変更）。
    /// </summary>
    public static class PortraitLayout
    {
        public const int ReferenceWidth = 1440;
        public const int ReferenceHeight = 1920;

        /// <summary>戦闘の高さの割合（上から。基準解像度で 960 px）。</summary>
        public const double BattleHeightRatio = 0.50;

        /// <summary>戦闘の領域の上端の帯（ライフ・所持金などの UI）の割合（T383、基準解像度で 96 px）。戦闘カメラはこの下だけを映す。</summary>
        public const double TopBandRatio = 0.10;

        /// <summary>スロット筐体の上端（下からの割合。基準解像度で 720 px）。ここから戦闘の下端までが操作バー（240 px）。</summary>
        public const float SlotAreaTop = 0.375f;

        private const double Aspect = (double)ReferenceWidth / ReferenceHeight;

        public static NormalizedRect Letterbox(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return new NormalizedRect(0, 0, 1, 1);
            }

            if (MatchHeight(screenWidth, screenHeight))
            {
                var width = screenHeight * Aspect / screenWidth;
                return new NormalizedRect((1 - width) / 2, 0, width, 1);
            }

            var height = screenWidth / Aspect / screenHeight;
            return new NormalizedRect(0, (1 - height) / 2, 1, height);
        }

        public static NormalizedRect Battle(int screenWidth, int screenHeight)
        {
            var box = Letterbox(screenWidth, screenHeight);
            var height = box.Height * BattleHeightRatio;
            return new NormalizedRect(box.X, box.Y + box.Height - height, box.Width, height);
        }

        /// <summary>3:4 より横長なら高さ基準（CanvasScaler の match = 1）、縦長なら幅基準。</summary>
        public static bool MatchHeight(int screenWidth, int screenHeight) =>
            (double)screenWidth / Math.Max(1, screenHeight) >= Aspect;

        /// <summary>戦闘カメラの領域（戦闘の領域から上端の帯を除く。下端は同じ）。</summary>
        public static NormalizedRect BattleView(int screenWidth, int screenHeight)
        {
            var battle = Battle(screenWidth, screenHeight);
            return new NormalizedRect(battle.X, battle.Y, battle.Width, battle.Height * (1 - TopBandRatio));
        }
    }
}
