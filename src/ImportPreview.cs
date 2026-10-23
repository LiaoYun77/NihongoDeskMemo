using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace NihongoDeskMemo
{
    internal sealed class ImportPreviewRow
    {
        public int Line { get; set; }
        public string Japanese { get; set; }
        public string Reading { get; set; }
        public string Chinese { get; set; }
        public string Unit { get; set; }
        public string Status { get; set; }
        public string Original { get; set; }
        public string ExampleJapanese { get; set; }
        public string ExampleChinese { get; set; }
        public string SourceLabel { get; set; }
    }

    internal sealed class ImportPreview
    {
        private WordDatabase candidate;
        private string originalHash;
        public readonly List<ImportPreviewRow> Rows = new List<ImportPreviewRow>();
        public ImportResult Result { get; private set; }
        public int Errors { get; private set; }
        public bool CanApply { get { return Errors == 0 && Result.Added + Result.Updated > 0; } }

        public static ImportPreview Prepare(string path, WordDatabase database, bool updateExisting)
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("词表超过 16 MB，请拆分后导入。");
            ImportPreview plan = new ImportPreview { originalHash = Fingerprint(database), Result = new ImportResult() };
            using (MemoryStream stream = new MemoryStream(Serialize(database)))
                plan.candidate = (WordDatabase)new XmlSerializer(typeof(WordDatabase)).Deserialize(stream);
            if (string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase))
            {
                WordDatabase incoming = StructuredWordImporter.ReadFile(path);
                HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
                foreach (WordItem word in database.Words) existing.Add(StructuredWordImporter.SourceKey(word));
                int line = 0;
                foreach (WordItem word in incoming.Words)
                    plan.AddRow(++line, word, existing.Contains(word.SourceId) ? (updateExisting ? "更新词条内容（保留进度）" : "已有来源（跳过）") : "新增", word.SourceText);
                plan.Result = StructuredWordImporter.Import(incoming, plan.candidate, updateExisting);
                return plan;
            }
            Encoding encoding = (Encoding)WordImporter.DetectEncoding(path).Clone();
            encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
            string[] lines = File.ReadAllLines(path, encoding);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith("#")) continue;
                WordItem word = WordImporter.ParseLine(raw);
                if (word == null)
                {
                    plan.AddRow(i + 1, new WordItem(), "错误：缺少单词、意思或分隔符", raw);
                    plan.Errors++; continue;
                }
                if (!seen.Add(word.Japanese))
                {
                    plan.AddRow(i + 1, word, "文件内重复（跳过）", raw);
                    plan.Result.Skipped++; continue;
                }
                List<WordItem> matches = plan.candidate.Words.FindAll(w => string.Equals(w.Japanese.Trim(), word.Japanese, StringComparison.OrdinalIgnoreCase));
                string status;
                if (matches.Count == 0)
                {
                    word.SourceText = raw;
                    plan.candidate.Words.Add(word); plan.Result.Added++;
                    status = string.IsNullOrEmpty(word.Pinyin) ? "新增（无假名）" : "新增";
                }
                else if (!updateExisting) { plan.Result.Unchanged++; status = "已有单词（跳过）"; }
                else if (matches.Count != 1)
                {
                    plan.Errors++; status = "错误：同词多条，无法确定更新对象";
                }
                else
                {
                    matches[0].Pinyin = word.Pinyin; matches[0].Chinese = word.Chinese;
                    plan.Result.Updated++; status = "更新读音和意思";
                }
                plan.AddRow(i + 1, word, status, raw);
            }
            return plan;
        }

        private void AddRow(int line, WordItem word, string status, string raw)
        {
            Rows.Add(new ImportPreviewRow { Line = line, Japanese = word.Japanese, Reading = word.Pinyin,
                Chinese = word.Chinese, Unit = word.Unit, Status = status, Original = raw,
                ExampleJapanese = word.ExampleJapanese, ExampleChinese = word.ExampleChinese, SourceLabel = word.SourceLabel });
        }

        public void Apply(WordDatabase database, Action<WordDatabase> save)
        {
            if (!CanApply) throw new InvalidOperationException("预览含有错误或没有可导入的更改。");
            if (Fingerprint(database) != originalHash) throw new InvalidOperationException("词库已变化，请重新预览。");
            List<WordItem> original = database.Words;
            int schema = database.SchemaVersion;
            database.Words = candidate.Words;
            try { save(database); }
            catch { database.Words = original; database.SchemaVersion = schema; throw; }
        }

        private static byte[] Serialize(WordDatabase database)
        {
            using (MemoryStream stream = new MemoryStream())
            { new XmlSerializer(typeof(WordDatabase)).Serialize(stream, database); return stream.ToArray(); }
        }

        private static string Fingerprint(WordDatabase database)
        {
            using (SHA256 hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Serialize(database)));
        }
    }
}
