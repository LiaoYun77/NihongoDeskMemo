using System;
using System.Collections.Generic;
using System.Globalization;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal sealed class WeakWordRow
    {
        public WordItem Word { get; set; }
        public string Japanese { get { return Word.Japanese; } }
        public string Reading { get { return Word.Pinyin; } }
        public string Chinese { get { return Word.Chinese; } }
        public string Unit { get { return Word.Unit; } }
        public double Difficulty { get; set; }
        public int WrongCount { get; set; }
        public int CorrectStreak { get; set; }
    }

    internal sealed class LearningStatistics
    {
        public int TodayRatings { get; private set; }
        public int DueCount { get; private set; }
        public int TotalWords { get; private set; }
        public readonly List<WeakWordRow> WeakWords = new List<WeakWordRow>();

        public static LearningStatistics Calculate(WordDatabase data, AppConfig config, int direction, DateTimeOffset now, TimeZoneInfo zone)
        {
            if (direction < 0 || direction > 3) throw new ArgumentOutOfRangeException("direction");
            LearningStatistics result = new LearningStatistics();
            DateTime today = TimeZoneInfo.ConvertTime(now, zone).Date;
            foreach (ReviewEvent entry in data.ReviewEvents)
            {
                DateTimeOffset reviewed;
                if (!entry.Undone && entry.Direction == direction && PracticeScope.Contains(config, entry.Unit) &&
                    ParseTime(entry.ReviewedAt, out reviewed) && TimeZoneInfo.ConvertTime(reviewed, zone).Date == today)
                    result.TodayRatings++;
            }
            foreach (WordItem word in data.Words)
            {
                if (!PracticeScope.Contains(config, word.Unit) || ((direction == 2 || direction == 3) && string.IsNullOrWhiteSpace(word.Pinyin))) continue;
                result.TotalWords++;
                DirectionProgress state = ReviewData.For(word, direction);
                DateTimeOffset next;
                if (!ParseTime(state.NextReviewTime, out next) || next <= now) result.DueCount++;
                if (word.Marked || (state.CorrectStreak < 3 && (state.Difficulty >= 6 || state.WrongCount > 0)))
                    result.WeakWords.Add(new WeakWordRow { Word = word, Difficulty = state.Difficulty,
                        WrongCount = state.WrongCount, CorrectStreak = state.CorrectStreak });
            }
            result.WeakWords.Sort(delegate(WeakWordRow a, WeakWordRow b)
            {
                int order = b.Difficulty.CompareTo(a.Difficulty);
                return order != 0 ? order : string.Compare(a.Japanese, b.Japanese, StringComparison.CurrentCulture);
            });
            return result;
        }

        private static bool ParseTime(string value, out DateTimeOffset time)
        { return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out time); }
    }
}
