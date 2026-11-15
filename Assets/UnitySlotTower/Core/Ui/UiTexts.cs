using System.Collections.Generic;
using System.Text;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Ui
{
    /// <summary>
    /// 文字あり版の文言（<c>ui_texts.csv</c>: <c>id, ja</c>、FR-006・plan 16.9）。id は重複なし、文言は空にしない。
    /// </summary>
    public sealed class UiTexts
    {
        public const string FileName = "ui_texts.csv";

        private readonly Dictionary<string, string> _texts;

        private UiTexts(Dictionary<string, string> texts) => _texts = texts;

        public IEnumerable<string> Ids => _texts.Keys;

        public int Count => _texts.Count;

        /// <summary>CSV の文字列から読む。形式の誤りは <see cref="CsvFormatException"/>。</summary>
        public static UiTexts Parse(string text) => Load(CsvParser.Parse(FileName, text));

        public static UiTexts Load(CsvTable table)
        {
            var texts = new Dictionary<string, string>();
            foreach (var row in table.Rows)
            {
                var id = row.Get("id");
                if (id.Length == 0)
                {
                    throw row.Error("id", "ID が空");
                }

                if (texts.ContainsKey(id))
                {
                    throw row.Error("id", $"ID '{id}' が重複している");
                }

                var ja = row.Get("ja");
                if (ja.Trim().Length == 0)
                {
                    throw row.Error("ja", $"'{id}' の文言が空");
                }

                texts.Add(id, ja);
            }

            return new UiTexts(texts);
        }

        public bool TryGet(string id, out string text) => _texts.TryGetValue(id ?? string.Empty, out text);

        /// <summary>id の文言。ない id は KeyNotFoundException。</summary>
        public string Get(string id)
        {
            if (TryGet(id, out var text))
            {
                return text;
            }

            throw new KeyNotFoundException($"{FileName} に id '{id}' がない");
        }

        /// <summary>全文言に出てくる文字（重複なし、改行などの制御文字は除く）。フォントの字形の作成と確認に使う。</summary>
        public string Characters()
        {
            var seen = new HashSet<char>();
            var result = new StringBuilder();
            foreach (var text in _texts.Values)
            {
                foreach (var c in text)
                {
                    if (!char.IsControl(c) && seen.Add(c))
                    {
                        result.Append(c);
                    }
                }
            }

            return result.ToString();
        }
    }
}
