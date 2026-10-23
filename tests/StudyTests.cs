using System;
using System.Collections.Generic;
using System.IO;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal static class StudyTests
    {
        private static void Main()
        {
            try
            {
                AppConfig config = new AppConfig();
                WordDatabase database = new WordDatabase();
                for (int i = 0; i < 4; i++) database.Words.Add(new WordItem { Japanese = "word" + i, Unit = "A" });
                database.Words.Add(new WordItem { Japanese = "excluded", Unit = "B" });
                PracticeScope.SetUnits(config, new string[] { "A" });
                StudySession session = new StudySession(new Random(9));
                HashSet<WordItem> round = new HashSet<WordItem>();
                WordItem last = null;
                for (int i = 0; i < 4; i++) { last = session.Next(database, config); Check(round.Add(last), "no repetition within round"); }
                Check(session.Next(database, config) != last, "no immediate repetition across rounds");
                foreach (WordItem word in database.Words) ReviewData.For(word, 0).NextReviewTime = DateTime.UtcNow.AddDays(3).ToString("o");
                Check(session.Next(database, config) == null, "review does not use future or other-unit words");
                config.StudyMode = 1;
                WordItem browsed = session.Next(database, config);
                Check(browsed != null && browsed.Unit == "A", "browse ignores due time but respects scope");
                Check(browsed.ReviewCount == 0, "browsing is not a completed review");
                Check(!ReviewPresentation.CanRateWithKeyboard(config, true), "browse disables rating");
                WordItem target = database.Words[0];
                string before = target.NextReviewTime;
                ReviewSnapshot undo = new ReviewSnapshot(target);
                ReviewActions.Rate(target, 1, DateTime.UtcNow);
                Check(target.WrongCount == 1 && target.Marked && target.ReviewCount == 1, "rating records forgotten word");
                undo.Restore();
                Check(target.NextReviewTime == before && target.WrongCount == 0 && target.ReviewCount == 0 && !target.Marked, "undo restores complete scheduling state");
                DateTime now = DateTime.UtcNow;
                ReviewActions.Rate(target, 3, now);
                Check(target.IntervalDays == 3, "remember starts at three days");
                ReviewActions.Rate(target, 3, now.AddDays(3));
                Check(target.IntervalDays == 7, "remember advances interval");
                CheckStorage();
                Console.WriteLine("Study and atomic storage tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        }

        private static void CheckStorage()
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "words.xml");
            try
            {
                Check(Storage.LoadPath(path, "legacy") == "legacy", "missing local files allow legacy fallback");
                File.WriteAllText(path + ".bak", "backup");
                Check(Storage.LoadPath(path, "legacy") == path, "local backup is preferred over legacy fallback when primary is missing");
                File.Delete(path + ".bak");
                XmlFileStore.Save(path, new AppConfig { RefreshSeconds = 31 });
                XmlFileStore.Save(path, new AppConfig { RefreshSeconds = 42 });
                Check(XmlFileStore.Load(path, new AppConfig()).RefreshSeconds == 42, "atomic save reads new content");
                Check(XmlFileStore.Load(path + ".bak", new AppConfig()).RefreshSeconds == 31, "last good version retained");
                string original = File.ReadAllText(path);
                bool writeFailed = false;
                try { XmlFileStore.Save(path, new AppConfig { PracticeUnit = "bad\u0000xml" }); }
                catch (InvalidOperationException) { writeFailed = true; }
                Check(writeFailed && File.ReadAllText(path) == original, "serialization failure cannot destroy previous save");
                Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "failed write cleans up temporary file");
                File.WriteAllText(path, "<broken>");
                Check(XmlFileStore.Load(path, new AppConfig()).RefreshSeconds == 31, "corrupt primary recovers from backup");
                XmlFileStore.Save(path, new AppConfig { RefreshSeconds = 55 });
                Check(XmlFileStore.Load(path + ".bak", new AppConfig()).RefreshSeconds == 31, "corruption never replaces valid backup");
                File.WriteAllText(path, "<broken>");
                File.WriteAllText(path + ".bak", "<broken>");
                bool rejected = false;
                try { XmlFileStore.Load(path, new AppConfig()); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "two corrupt files never silently become empty data");
            }
            finally { Directory.Delete(directory, true); }
        }

        private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    }
}
