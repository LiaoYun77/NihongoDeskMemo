using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace NihongoDeskMemo
{
    public class ReviewState
    {
        public string LastReviewTime { get; set; }
        public string NextReviewTime { get; set; }
        public int ReviewCount { get; set; }
        public int WrongCount { get; set; }
        public int CorrectStreak { get; set; }
        public double Difficulty { get; set; }
        public int IntervalDays { get; set; }
        public ReviewState() { LastReviewTime = string.Empty; NextReviewTime = string.Empty; Difficulty = 5; }
    }

    public sealed class DirectionProgress : ReviewState
    {
        public int Direction { get; set; }
    }

    public sealed class ReviewEvent
    {
        public string EventId { get; set; }
        public string WordId { get; set; }
        public string Japanese { get; set; }
        public string Unit { get; set; }
        public int Direction { get; set; }
        public int Rating { get; set; }
        public string ReviewedAt { get; set; }
        public string NextReviewTime { get; set; }
        public int IntervalDays { get; set; }
        public bool Undone { get; set; }
    }

    internal static class ReviewData
    {
        public static readonly string[] DirectionNames = { "日语认中文", "中文回忆日语", "假名回忆汉字", "汉字回忆读音" };
        public static readonly string[] RatingNames = { "", "不会", "模糊", "记得", "熟练" };

        public static string BackupLegacy(string source, string directory)
        {
            string digest;
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(source)) digest = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
            string backups = Path.Combine(directory, "backups");
            Directory.CreateDirectory(backups);
            string path = Path.Combine(backups, "words-pre-v0.5-" + digest + ".xml");
            if (!File.Exists(path)) File.Copy(source, path, false);
            return path;
        }

        public static void Normalize(WordDatabase database)
        {
            if (database.SchemaVersion > 1) throw new InvalidDataException("词库格式较新，请使用对应新版软件，避免丢失数据。");
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WordItem word in database.Words)
            {
                EnsureWord(word);
                if (!ids.Add(word.WordId)) throw new InvalidDataException("词库含有重复词条编号，已停止保存。");
            }
            if (database.ReviewEvents == null) database.ReviewEvents = new List<ReviewEvent>();
            ids.Clear();
            foreach (ReviewEvent entry in database.ReviewEvents)
                if (entry == null || string.IsNullOrWhiteSpace(entry.EventId) || string.IsNullOrWhiteSpace(entry.WordId) ||
                    !ids.Add(entry.EventId) || entry.Direction < 0 || entry.Direction > 3 || entry.Rating < 1 || entry.Rating > 4)
                    throw new InvalidDataException("复习记录格式异常，已停止保存，请保留原文件与备份。");
            database.SchemaVersion = 1;
        }

        public static void EnsureWord(WordItem word)
        {
            if (string.IsNullOrWhiteSpace(word.WordId)) word.WordId = Guid.NewGuid().ToString("N");
            if (word.DirectionStates == null) word.DirectionStates = new List<DirectionProgress>();
            HashSet<int> directions = new HashSet<int>();
            foreach (DirectionProgress state in word.DirectionStates)
                if (state == null || state.Direction < 0 || state.Direction > 3 || !directions.Add(state.Direction))
                    throw new InvalidDataException("词条的分方向复习状态重复或无效。");
            // Legacy state is a shared starting baseline, not fabricated past review events.
            for (int mode = 0; mode < 4; mode++)
                if (!directions.Contains(mode))
                {
                    DirectionProgress state = new DirectionProgress { Direction = mode };
                    Copy(word, state); word.DirectionStates.Add(state);
                }
        }

        public static DirectionProgress For(WordItem word, int direction)
        {
            if (direction < 0 || direction > 3) throw new ArgumentOutOfRangeException("direction");
            EnsureWord(word);
            return word.DirectionStates.Find(s => s.Direction == direction);
        }

        public static void Copy(ReviewState from, ReviewState to)
        {
            to.LastReviewTime = from.LastReviewTime; to.NextReviewTime = from.NextReviewTime;
            to.ReviewCount = from.ReviewCount; to.WrongCount = from.WrongCount; to.CorrectStreak = from.CorrectStreak;
            to.Difficulty = from.Difficulty; to.IntervalDays = from.IntervalDays;
        }

        public static List<DirectionProgress> CopyStates(List<DirectionProgress> states)
        {
            List<DirectionProgress> copy = new List<DirectionProgress>();
            if (states != null) foreach (DirectionProgress state in states)
            { DirectionProgress item = new DirectionProgress { Direction = state.Direction }; Copy(state, item); copy.Add(item); }
            return copy;
        }
    }
}
