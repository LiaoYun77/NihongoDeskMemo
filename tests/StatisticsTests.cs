using System;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal static class StatisticsTests
    {
        private static void Main()
        {
            try
            {
                DateTimeOffset now = new DateTimeOffset(2026, 10, 24, 0, 30, 0, TimeSpan.FromHours(8));
                TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("test+8", TimeSpan.FromHours(8), "test+8", "test+8");
                WordDatabase data = new WordDatabase();
                WordItem weak = new WordItem { Japanese = "会社員", Pinyin = "かいしゃいん", Chinese = "职员", Unit = "01" };
                WordItem strong = new WordItem { Japanese = "本", Pinyin = "ほん", Chinese = "书", Unit = "02" };
                WordItem missing = new WordItem { Japanese = "空", Chinese = "空", Unit = "01" };
                WordItem other = new WordItem { Japanese = "水", Chinese = "水", Unit = "03" };
                data.Words.AddRange(new WordItem[] { weak, strong, missing, other }); ReviewData.Normalize(data);
                ReviewData.For(weak, 0).WrongCount = 1; ReviewData.For(weak, 0).Difficulty = 7;
                ReviewData.For(strong, 0).NextReviewTime = now.AddDays(3).ToString("o");
                ReviewData.For(weak, 1).NextReviewTime = now.AddDays(1).ToString("o");
                ReviewActions.Rate(data, weak, 0, 1, now.AddMinutes(-20).UtcDateTime);
                ReviewActions.Rate(data, weak, 1, 3, now.AddMinutes(-10).UtcDateTime);
                ReviewActions.Rate(data, weak, 0, 3, now.AddMinutes(-40).UtcDateTime);
                ReviewActions.Rate(data, strong, 0, 4, now.UtcDateTime);
                ReviewActions.Rate(data, other, 0, 3, now.UtcDateTime);
                ReviewSnapshot undo = new ReviewSnapshot(data, weak);
                ReviewActions.Rate(data, weak, 0, 4, now.UtcDateTime); undo.Undo();
                AppConfig scope = new AppConfig { PracticeUnits = new string[] { "01", "02" } };
                LearningStatistics stats = LearningStatistics.Calculate(data, scope, 0, now, zone);
                Check(stats.TodayRatings == 2, "local midnight, direction, multi-unit and undo filtering");
                Check(stats.DueCount == 1 && stats.TotalWords == 3, "due includes new cards, excludes future and other units");
                Check(stats.WeakWords.Count == 1 && stats.WeakWords[0].Word == weak, "weak view uses selected direction");
                stats = LearningStatistics.Calculate(data, scope, 1, now, zone);
                Check(stats.TodayRatings == 1 && stats.DueCount == 2, "independent direction statistics");
                weak.Marked = false;
                Check(stats.WeakWords.Count == 1, "marked word appears across directions");
                stats = LearningStatistics.Calculate(data, scope, 1, now, zone);
                Check(stats.WeakWords.Count == 0, "unmarked successful direction not weak because another direction failed");
                stats = LearningStatistics.Calculate(data, scope, 2, now, zone);
                Check(stats.TotalWords == 2 && stats.DueCount == 2, "kana-only modes exclude missing readings");
                weak.Unit = "03";
                stats = LearningStatistics.Calculate(data, scope, 0, now, zone);
                Check(stats.TodayRatings == 2 && stats.TotalWords == 2, "history uses unit snapshot but due follows current scope");
                data.Words.Remove(weak);
                Check(LearningStatistics.Calculate(data, scope, 0, now, zone).TodayRatings == 2, "deleted-word history remains counted");
                Console.WriteLine("Learning statistics tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
