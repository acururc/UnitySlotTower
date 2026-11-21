using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// シーンをまたいで持つ選択（plan 4章）。文字あり版かどうか。難易度の選択はない（spec 002 E-18）。セーブはしない（FR-005）。
    /// </summary>
    public static class GameSession
    {
        public const string TitleScene = "Title";
        public const string GameScene = "Game";
        /// <summary>文字あり版か（FR-006、D-74）。起動ごとに false（文字なし版）で始まり、タイトルの隠しボタンで切り替える。</summary>
        public static bool TextMode { get; private set; }

        /// <summary>文字あり／なしが切り替わったとき（引数は切り替え後の <see cref="TextMode"/>）。</summary>
        public static event Action<bool> TextModeChanged;

        // ドメインの再読み込みを切っているので、Play のたびに戻す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
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

        /// <summary>Game シーンを読み込む（ステージ1から始まる）。</summary>
        public static void StartGame()
        {
            SceneManager.LoadScene(GameScene);
        }
    }
}
