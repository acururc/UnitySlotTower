using System.Collections.Generic;
using System.IO;

namespace UnitySlotTower.Tests.EditMode.Data
{
    /// <summary>プロジェクトの CSV 正本を読む（EditMode テストの作業ディレクトリはプロジェクトルート）。</summary>
    internal static class TablePaths
    {
        public const string Root = "Assets/UnitySlotTower/Data/Tables";

        /// <summary>Root からの相対パス（区切りは '/'）→ 内容。改行は LF にそろえる（core.autocrlf で作業ツリーが CRLF になっても、テストの "\n…\n" の置換が効くように）。</summary>
        public static Dictionary<string, string> ReadAll()
        {
            var files = new Dictionary<string, string>();
            foreach (var path in Directory.GetFiles(Root, "*.csv", SearchOption.AllDirectories))
            {
                var relative = path.Substring(Root.Length + 1).Replace('\\', '/');
                files[relative] = File.ReadAllText(path).Replace("\r\n", "\n");
            }

            return files;
        }
    }
}
