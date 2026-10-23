using System;
using System.IO;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal static class ReviewRecordTests
    {
        private static void Main()
        {
            try
            {
                WordDatabase database = new WordDatabase();
                WordItem word = new WordItem { Japanese = "会社員", Pinyin = "かいしゃいん", Chinese = "公司职员",
                    ReviewCount = 6, WrongCount = 2, IntervalDays = 7, NextReviewTime = DateTime.UtcNow.AddDays(7).ToString("o") };
                database.Words.Add(word);
                Check(typeof(WordItem).GetProperty("WordId") != null, "words have a stable identity independent of source IDs");
                ReviewData.Normalize(database);
                string id = word.WordId, future = word.NextReviewTime;
                Check(database.SchemaVersion == 1 && word.DirectionStates.Count == 4 && database.ReviewEvents.Count == 0,
                    "legacy migration creates four baseline states without fabricated history");
                Check(ReviewData.For(word, 3).ReviewCount == 6 && ReviewData.For(word, 3).NextReviewTime == future,
                    "legacy counts and due time retained");
                ReviewSnapshot before = new ReviewSnapshot(database, word);
                ReviewActions.Rate(database, word, 1, 1, DateTime.UtcNow);
                Check(word.WordId == id && word.ReviewCount == 7 && word.WrongCount == 3 && database.ReviewEvents.Count == 1,
                    "rating logs stable identity and aggregate counts");
                Check(ReviewData.For(word, 1).IntervalDays == 0 && ReviewData.For(word, 0).NextReviewTime == future &&
                    ReviewData.For(word, 0).ReviewCount == 6 && ReviewData.For(word, 2).WrongCount == 2,
                    "rating changes only the selected direction");
                ReviewSnapshot after = new ReviewSnapshot(database, word);
                before.Undo();
                Check(word.ReviewCount == 6 && database.ReviewEvents[0].Undone && ReviewData.For(word, 1).NextReviewTime == future,
                    "undo restores all states but keeps event marked undone");
                after.Restore();
                Check(word.ReviewCount == 7 && !database.ReviewEvents[0].Undone, "failed undo rollback restores event and scheduling");
                before.Restore();
                Check(word.ReviewCount == 6 && database.ReviewEvents.Count == 0, "failed rating rollback removes unsaved event");
                WordItem fresh = new WordItem { Japanese = "会議", Pinyin = "かいぎ", Chinese = "会议" };
                database.Words.Add(fresh);
                ReviewData.EnsureWord(fresh);
                ReviewActions.Rate(database, fresh, 1, 4, DateTime.UtcNow);
                AppConfig scope = new AppConfig { ReviewMode = 0 };
                Check(StudySession.Eligible(fresh, scope), "unreviewed recognition remains due after recall rating");
                scope.ReviewMode = 1;
                Check(!StudySession.Eligible(fresh, scope), "reviewed recall follows its own due time");
                VerifyRoundTrip(database, word);
                bool rejected = false;
                database.SchemaVersion = 2;
                try { ReviewData.Normalize(database); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "unsupported future schema rejected");
                database.SchemaVersion = 1;
                fresh.WordId = word.WordId;
                rejected = false;
                try { ReviewData.Normalize(database); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "duplicate stable identities rejected instead of silently merging history");
                Console.WriteLine("Review record tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        }
        private static void VerifyRoundTrip(WordDatabase database, WordItem word)
        {
            string directory = Path.Combine(Path.GetTempPath(), "NihongoReviewRecords-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "words.xml");
            try
            {
                const string legacy = "<WordDatabase><Words><WordItem><Japanese>old</Japanese><Chinese>旧词</Chinese><ReviewCount>8</ReviewCount><IntervalDays>7</IntervalDays></WordItem></Words></WordDatabase>";
                File.WriteAllText(path, legacy);
                string backup = ReviewData.BackupLegacy(path, directory);
                Check(File.ReadAllText(backup) == legacy && ReviewData.BackupLegacy(path, directory) == backup,
                    "permanent migration backup preserves exact bytes and is idempotent");
                WordDatabase migrated = XmlFileStore.Load<WordDatabase>(path, null);
                ReviewData.Normalize(migrated);
                Check(File.ReadAllText(path) == legacy && ReviewData.For(migrated.Words[0], 2).ReviewCount == 8,
                    "migration preserves legacy XML until explicit save");
                XmlFileStore.Save(path, migrated);
                Check(File.ReadAllText(path + ".bak") == legacy, "first new-schema save retains exact pre-upgrade backup");
                XmlFileStore.Save(path, database);
                Check(File.ReadAllText(backup) == legacy, "later saves cannot overwrite the pre-upgrade backup");
                WordDatabase restored = XmlFileStore.Load<WordDatabase>(path, null);
                ReviewData.Normalize(restored);
                Check(restored.Words[0].WordId == word.WordId && restored.Words[0].DirectionStates.Count == 4 && restored.ReviewEvents.Count == 1,
                    "identities, direction progress and events survive XML round trip");
            }
            finally { Directory.Delete(directory, true); }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
