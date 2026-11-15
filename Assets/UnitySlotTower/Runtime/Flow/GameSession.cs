using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// シーンをまたいで持つ1プレイ分の選択（plan 4章）。難易度（設定1〜6）と文字あり版かどうか。セーブはしない（FR-005）。
    /// </summary>
    public static class GameSession
    {
        public const string TitleScene = "Title";
        public const string GameScene = "Game";
        public const int MinSetting = 1;
        public const int MaxSetting = 6;

        /// <summary>難易度選択で選んだ設定。Game シーンを直接開いたときは null（筐体の既定値を使う）。</summary>
        public static int? SelectedSetting { get; private set; }

        /// <summary>文字あり版か（FR-006、D-74）。起動ごとに false（文字なし版）で始まり、タイトルの隠しボタンで切り替える。</summary>
        public static bool TextMode { get; private set; }

        /// <summary>文字あり／なしが切り替わったとき（引数は切り替え後の <see cref="TextMode"/>）。</summary>
        public static event Action<bool> TextModeChanged;

        // ドメインの再読み込みを切っているので、Play のたびに戻す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            SelectedSetting = null;
            TextMode = false;
            TextModeChanged = null;
        }

        /// <summary>文字あり／なしを決める。変わったときだけ <see cref="TextModeChanged"/> を出す。</summary>
        public static void SetTextMode(bool on)
        {
            if (TextMode == on)
            {
                return;
            }

            TextMode = on;
            TextModeChanged?.Invoke(on);
        }

        /// <summary>設定を選んで Game シーンを読み込む。</summary>
        public static void StartGame(int setting)
        {
            if (setting < MinSetting || setting > MaxSetting)
            {
                throw new ArgumentOutOfRangeException(nameof(setting), setting, "設定は1〜6");
            }

            SelectedSetting = setting;
            SceneManager.LoadScene(GameScene);
        }
    }
}
