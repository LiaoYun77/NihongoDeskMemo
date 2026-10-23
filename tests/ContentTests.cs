using System;
using System.IO;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal static class ContentTests
    {
        private static void Main()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NihongoContent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "source.xml");
                WordDatabase source = new WordDatabase();
                WordItem word = new WordItem { Japanese = "会社員", Pinyin = "かいしゃいん", Chinese = "公司职员", Unit = "第01课", SourceId = "sample-1",
                    ExampleJapanese = "私は会社員です。", ExampleChinese = "我是公司职员。", SourceLabel = "自编", SourceText = "original raw text" };
                source.Words.Add(word); ReviewData.Normalize(source);
                ReviewActions.Rate(source, word, 1, 3, DateTime.UtcNow);
                string id = word.WordId, due = ReviewData.For(word, 1).NextReviewTime;
                source.SchemaVersion = 1;
                XmlFileStore.Save(path, source);
                string old = File.ReadAllText(path);
                string backup = ReviewData.BackupLegacy(path, directory, "v0.6");
                WordDatabase loaded = XmlFileStore.Load<WordDatabase>(path, null); ReviewData.Normalize(loaded);
                Check(loaded.SchemaVersion == 2 && loaded.Words[0].WordId == id && loaded.ReviewEvents.Count == 1 &&
                    ReviewData.For(loaded.Words[0], 1).NextReviewTime == due, "v0.5 migration preserves IDs, states and events");
                Check(File.ReadAllText(backup) == old && Path.GetFileName(backup).StartsWith("words-pre-v0.6-"), "permanent v0.6 preimage");
                WordDatabase target = new WordDatabase();
                ImportPreview plan = ImportPreview.Prepare(path, target, false); plan.Apply(target, delegate { });
                Check(target.Words[0].ExampleJapanese == word.ExampleJapanese && target.Words[0].ExampleChinese == word.ExampleChinese &&
                    target.Words[0].SourceLabel == word.SourceLabel && target.Words[0].SourceText == word.SourceText, "all content imported losslessly");
                Check(target.Words[0].ReviewCount == 0 && target.ReviewEvents.Count == 0, "source progress is not imported");
                WordItem local = target.Words[0]; ReviewData.Normalize(target);
                ReviewActions.Rate(target, local, 0, 4, DateTime.UtcNow);
                string localId = local.WordId, localDue = ReviewData.For(local, 0).NextReviewTime;
                word.ExampleChinese = "更新后的例句"; word.SourceLabel = "修订来源"; XmlFileStore.Save(path, source);
                plan = ImportPreview.Prepare(path, target, false);
                Check(!plan.CanApply && local.SourceLabel == "自编", "default duplicate skip preserves edits");
                plan = ImportPreview.Prepare(path, target, true); Check(plan.Result.Updated == 1, "explicit SourceId update preview");
                plan.Apply(target, delegate { }); local = target.Words[0];
                Check(local.WordId == localId && local.ReviewCount == 1 && ReviewData.For(local, 0).NextReviewTime == localDue && target.ReviewEvents.Count == 1,
                    "content update cannot replace local scheduling or history");
                Check(local.ExampleChinese == word.ExampleChinese && local.SourceLabel == word.SourceLabel, "confirmed content update");
                XmlFileStore.Save(path, target); loaded = XmlFileStore.Load<WordDatabase>(path, null);
                Check(loaded.Words[0].ExampleJapanese == word.ExampleJapanese && loaded.Words[0].SourceText == word.SourceText, "save/load retains examples and original source");
                string exported = Path.Combine(directory, "exported.xml");
                target.Words.Add(new WordItem { Japanese = "本", Chinese = "书", ExampleJapanese = "本を読みます。" });
                StructuredWordImporter.ExportFile(exported, target);
                WordDatabase roundtrip = new WordDatabase(); StructuredWordImporter.ImportFile(exported, roundtrip);
                Check(roundtrip.Words.Count == 2 && roundtrip.Words[1].ExampleJapanese == "本を読みます。" &&
                    roundtrip.Words[0].ReviewCount == 0 && roundtrip.Words[0].SourceText == word.SourceText,
                    "export includes all content, assigns missing source identity and excludes review progress");
                target.Words.Add(new WordItem { Japanese = "duplicate", Chinese = "重复", SourceId = local.SourceId });
                bool rejected = false;
                try { ImportPreview.Prepare(path, target, true); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "ambiguous source identities block explicit update");
                Console.WriteLine("Examples, source and schema migration tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { Directory.Delete(directory, true); }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
