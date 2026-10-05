using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal static class ImportTests
    {
        private static void Main(string[] args)
        {
            string temp = Path.Combine(Path.GetTempPath(), "NihongoImport-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                WordDatabase source = new WordDatabase();
                source.Words.Add(Item("source:1", "lesson1"));
                source.Words.Add(Item("source:2", "lesson2"));
                Write(temp, source);
                WordDatabase target = new WordDatabase();
                WordItem existing = new WordItem { Japanese = "same", Chinese = "old meaning", ReviewCount = 9 };
                target.Words.Add(existing);
                ImportResult first = WordImporter.ImportFile(temp, target);
                Check(first.Added == 2 && target.Words.Count == 3, "same written form in different lessons retained");
                Check(existing.Chinese == "old meaning" && existing.ReviewCount == 9, "existing words and progress untouched");
                target.Words[1].ReviewCount = 7;
                target.Words[1].Chinese = "user edit";
                ImportResult second = WordImporter.ImportFile(temp, target);
                Check(second.Added == 0 && second.Unchanged == 2 && target.Words.Count == 3, "repeat import is idempotent");
                Check(target.Words[1].ReviewCount == 7 && target.Words[1].Chinese == "user edit", "repeat import preserves edits and progress");
                Write(temp, target);
                WordDatabase restored = Read(temp);
                Check(restored.Words[1].SourceText == source.Words[0].SourceText, "raw source survives XML roundtrip");

                source.Words[1].SourceId = string.Empty;
                Write(temp, source);
                MustFail(temp, target, "invalid last row leaves database unchanged");
                File.WriteAllText(temp, "<!DOCTYPE WordDatabase [<!ENTITY x SYSTEM 'file:///missing'>]><WordDatabase><Words /></WordDatabase>");
                MustFail(temp, target, "DTD is rejected");
                File.WriteAllText(temp, "<WordDatabase><Words/><Unexpected>data</Unexpected></WordDatabase>");
                MustFail(temp, target, "unknown fields are rejected rather than discarded");
                if (args.Length > 0) VerifyTextbook(args[0], temp);
                Console.WriteLine("Import tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private static WordItem Item(string id, string unit)
        {
            return new WordItem { Japanese = "same", Pinyin = "reading", Chinese = "meaning, with punctuation",
                Unit = unit, SourceId = id, SourceText = "original line 1\noriginal line 2 <&>" };
        }

        private static void VerifyTextbook(string path, string temp)
        {
            WordDatabase original = Read(path);
            WordDatabase imported = new WordDatabase();
            ImportResult result = WordImporter.ImportFile(path, imported);
            Check(result.Added == 2142 && imported.Words.Count == original.Words.Count, "all 2142 textbook entries imported");
            Write(temp, imported);
            WordDatabase saved = Read(temp);
            HashSet<string> units = new HashSet<string>();
            for (int i = 0; i < original.Words.Count; i++)
            {
                WordItem a = original.Words[i], b = saved.Words[i];
                Check(a.Japanese == b.Japanese && a.Pinyin == b.Pinyin && a.Chinese == b.Chinese &&
                    a.Unit == b.Unit && a.SourceId == b.SourceId && a.SourceText == b.SourceText,
                    "textbook entry roundtrip " + i);
                units.Add(b.Unit);
            }
            Check(units.Count == 48, "all 48 lessons retained");
            Check(WordImporter.ImportFile(path, imported).Unchanged == 2142, "textbook repeat import adds no duplicates");
            Console.WriteLine("Textbook: 2142 entries, 48 lessons, every field verified after save/load.");
        }

        private static void MustFail(string path, WordDatabase target, string message)
        {
            int count = target.Words.Count;
            bool failed = false;
            try { WordImporter.ImportFile(path, target); }
            catch (Exception) { failed = true; }
            Check(failed && target.Words.Count == count, message);
        }

        private static void Write(string path, WordDatabase database)
        {
            using (Stream stream = File.Create(path)) new XmlSerializer(typeof(WordDatabase)).Serialize(stream, database);
        }

        private static WordDatabase Read(string path)
        {
            using (Stream stream = File.OpenRead(path)) return (WordDatabase)new XmlSerializer(typeof(WordDatabase)).Deserialize(stream);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
