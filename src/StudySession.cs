using System;
using System.Collections.Generic;
using System.Globalization;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal sealed class StudySession
    {
        private readonly Random random;
        private readonly HashSet<WordItem> visited = new HashSet<WordItem>();
        private WordItem last;
        private string scope;

        public StudySession(Random source) { random = source; }
        public void Reset() { visited.Clear(); last = null; scope = null; }

        public WordItem Next(WordDatabase database, AppConfig config)
        {
            string key = config.StudyMode + ":" + config.ReviewMode + ":" + string.Join("\n", PracticeScope.GetUnits(config).ToArray());
            if (key != scope) { Reset(); scope = key; }
            List<WordItem> eligible = database.Words.FindAll(delegate(WordItem word) { return Eligible(word, config); });
            List<WordItem> candidates = eligible.FindAll(delegate(WordItem word) { return !visited.Contains(word); });
            if (candidates.Count == 0) { visited.Clear(); candidates = eligible; }
            if (candidates.Count == 0) return null;
            if (candidates.Count > 1) candidates.Remove(last);
            int total = 0;
            foreach (WordItem word in candidates) total += Priority(word, config);
            int roll = random.Next(total);
            foreach (WordItem word in candidates)
            {
                roll -= Priority(word, config);
                if (roll < 0) { visited.Add(word); last = word; return word; }
            }
            throw new InvalidOperationException("No study candidate selected.");
        }

        private static int Priority(WordItem word, AppConfig config)
        {
            return config.StudyMode == 1 ? 1 : Math.Max(1, (int)Math.Round(Math.Min(10, ReviewData.For(word, config.ReviewMode).Difficulty) * 2));
        }

        public static bool Eligible(WordItem word, AppConfig config)
        {
            if (!PracticeScope.Contains(config, word.Unit)) return false;
            if ((config.ReviewMode == 2 || config.ReviewMode == 3) && string.IsNullOrWhiteSpace(word.Pinyin)) return false;
            DateTime next;
            return config.StudyMode == 1 || !DateTime.TryParse(ReviewData.For(word, config.ReviewMode).NextReviewTime, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out next) || next.ToUniversalTime() <= DateTime.UtcNow;
        }
    }

    internal sealed class ReviewSnapshot
    {
        public readonly WordItem Word;
        private readonly string lastReview, nextReview;
        private readonly int count, wrong, streak, interval;
        private readonly double difficulty;
        private readonly bool marked;
        private readonly List<DirectionProgress> directions;
        private readonly WordDatabase database;
        private readonly int eventCount;
        private readonly int schema;
        private readonly ReviewEvent latestEvent;
        private readonly bool latestUndone;

        public ReviewSnapshot(WordItem word) : this(null, word) { }

        public ReviewSnapshot(WordDatabase source, WordItem word)
        {
            Word = word; lastReview = word.LastReviewTime; nextReview = word.NextReviewTime;
            count = word.ReviewCount; wrong = word.WrongCount; streak = word.CorrectStreak;
            interval = word.IntervalDays; difficulty = word.Difficulty; marked = word.Marked;
            directions = ReviewData.CopyStates(word.DirectionStates);
            database = source;
            if (database != null)
            {
                eventCount = database.ReviewEvents.Count;
                schema = database.SchemaVersion;
                if (eventCount > 0) { latestEvent = database.ReviewEvents[eventCount - 1]; latestUndone = latestEvent.Undone; }
            }
        }

        public void Restore()
        {
            RestoreWord();
            if (database != null && database.ReviewEvents.Count > eventCount)
                database.ReviewEvents.RemoveRange(eventCount, database.ReviewEvents.Count - eventCount);
            if (latestEvent != null) latestEvent.Undone = latestUndone;
            if (database != null) database.SchemaVersion = schema;
        }

        public void Undo()
        {
            RestoreWord();
            if (database != null)
                for (int i = eventCount; i < database.ReviewEvents.Count; i++) database.ReviewEvents[i].Undone = true;
        }

        private void RestoreWord()
        {
            Word.LastReviewTime = lastReview; Word.NextReviewTime = nextReview; Word.ReviewCount = count;
            Word.WrongCount = wrong; Word.CorrectStreak = streak; Word.IntervalDays = interval;
            Word.Difficulty = difficulty; Word.Marked = marked;
            Word.DirectionStates = ReviewData.CopyStates(directions);
        }
    }

    internal static class ReviewActions
    {
        public static void Rate(WordDatabase database, WordItem word, int direction, int rating, DateTime now)
        {
            DirectionProgress state = ReviewData.For(word, direction);
            Rate(state, rating, now);
            int total = word.ReviewCount + 1, wrong = word.WrongCount + (rating == 1 ? 1 : 0);
            ReviewData.Copy(state, word);
            word.ReviewCount = total; word.WrongCount = wrong;
            if (rating == 1) word.Marked = true;
            database.ReviewEvents.Add(new ReviewEvent { EventId = Guid.NewGuid().ToString("N"), WordId = word.WordId,
                Japanese = word.Japanese, Unit = word.Unit, Direction = direction, Rating = rating,
                ReviewedAt = now.ToUniversalTime().ToString("o"), NextReviewTime = state.NextReviewTime, IntervalDays = state.IntervalDays });
        }

        public static void Rate(ReviewState word, int rating, DateTime now)
        {
            if (rating < 1 || rating > 4) throw new ArgumentOutOfRangeException("rating");
            word.LastReviewTime = now.ToUniversalTime().ToString("o");
            word.ReviewCount++;
            if (rating <= 2)
            {
                word.CorrectStreak = 0;
                word.Difficulty = Math.Min(10, word.Difficulty + (rating == 1 ? 1 : 0.3));
                word.IntervalDays = rating == 1 ? 0 : 1;
                word.NextReviewTime = (rating == 1 ? now.AddMinutes(5) : now.AddDays(1)).ToUniversalTime().ToString("o");
                if (rating == 1) { word.WrongCount++; if (word is WordItem) ((WordItem)word).Marked = true; }
                return;
            }
            word.CorrectStreak++;
            word.Difficulty = Math.Max(1, word.Difficulty - (rating == 4 ? 0.5 : 0.25));
            int next = 60;
            foreach (int days in new int[] { 3, 7, 15, 30, 60 })
                if (days > word.IntervalDays && days >= (rating == 4 ? 7 : 3)) { next = days; break; }
            word.IntervalDays = next;
            word.NextReviewTime = now.AddDays(next).ToUniversalTime().ToString("o");
        }
    }
}
