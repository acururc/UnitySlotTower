namespace UnitySlotTower.Core.Ui
{
    /// <summary>
    /// Title 以外で表示してよい文字（FR-003、plan 8章。小数点は T316 のカスタム画面の%用、「?」は T404 の説明ボタン用、
    /// 「¥」は T360 の所持金の記号。FR-007、D-77）。数字専用フォントの文字集合もこれに合わせる。
    /// </summary>
    public static class NumericText
    {
        public const string AllowedCharacters = "0123456789.+-−×/%?¥";

        /// <summary>所持金（円）の数字の書式。TMP の SetText に渡す（FR-007、plan 17.1）。</summary>
        public const string YenFormat = "¥{0}";

        /// <summary>許可されない最初の文字の位置。すべて許可されていれば -1。</summary>
        public static int FindInvalid(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            for (var i = 0; i < text.Length; i++)
            {
                if (AllowedCharacters.IndexOf(text[i]) < 0)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
