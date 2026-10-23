using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace NihongoDeskMemo
{
    internal static class StructuredWordImporter
    {
        public static ImportResult ImportFile(string path, WordDatabase database)
        {
            return Import(ReadFile(path), database);
        }

        internal static WordDatabase ReadFile(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;
            settings.MaxCharactersInDocument = 16 * 1024 * 1024;
            XmlSerializer serializer = new XmlSerializer(typeof(WordDatabase));
            serializer.UnknownElement += delegate(object sender, XmlElementEventArgs e)
            {
                throw new InvalidDataException("词库包含不支持的字段：" + e.Element.Name);
            };
            WordDatabase incoming;
            using (XmlReader reader = XmlReader.Create(path, settings))
                incoming = (WordDatabase)serializer.Deserialize(reader);
            if (incoming == null || incoming.Words == null || incoming.Words.Count == 0)
                throw new InvalidDataException("XML 词库为空或格式不正确。");
            if (incoming.SchemaVersion > ReviewData.CurrentSchemaVersion) throw new InvalidDataException("不支持该词库版本，请使用新版软件。");

            // Validate every row before touching the current database.
            HashSet<string> incomingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WordItem item in incoming.Words)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Japanese) ||
                    string.IsNullOrWhiteSpace(item.Chinese) || string.IsNullOrWhiteSpace(item.SourceId))
                    throw new InvalidDataException("XML 词条缺少单词、意思或来源编号，请使用完整词库文件。");
                if (!incomingIds.Add(item.SourceId))
                    throw new InvalidDataException("XML 词库包含重复来源编号：" + item.SourceId);
            }
            return incoming;
        }

        internal static ImportResult Import(WordDatabase incoming, WordDatabase database)
        { return Import(incoming, database, false); }

        internal static ImportResult Import(WordDatabase incoming, WordDatabase database, bool updateExisting)
        {
            Dictionary<string, WordItem> existingIds = new Dictionary<string, WordItem>(StringComparer.Ordinal);
            foreach (WordItem item in database.Words)
                if (!string.IsNullOrEmpty(item.SourceId))
                {
                    if (updateExisting && existingIds.ContainsKey(item.SourceId))
                        throw new InvalidDataException("本地词库来源编号重复，无法确定更新对象：" + item.SourceId);
                    existingIds[item.SourceId] = item;
                }
            ImportResult result = new ImportResult();
            foreach (WordItem item in incoming.Words)
            {
                if (existingIds.ContainsKey(item.SourceId))
                {
                    if (updateExisting)
                    {
                        CopyContent(item, existingIds[item.SourceId]);
                        result.Updated++; continue;
                    }
                    result.Unchanged++;
                    continue;
                }
                WordItem added = new WordItem();
                CopyContent(item, added); database.Words.Add(added);
                existingIds.Add(item.SourceId, added);
                result.Added++;
            }
            return result;
        }

        private static void CopyContent(WordItem from, WordItem to)
        {
            to.Japanese = from.Japanese; to.Pinyin = from.Pinyin; to.Chinese = from.Chinese;
            to.Unit = from.Unit; to.SourceId = from.SourceId; to.SourceText = from.SourceText;
            to.ExampleJapanese = from.ExampleJapanese; to.ExampleChinese = from.ExampleChinese; to.SourceLabel = from.SourceLabel;
        }

        public static void ExportFile(string path, WordDatabase database)
        {
            WordDatabase exported = new WordDatabase { SchemaVersion = ReviewData.CurrentSchemaVersion };
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WordItem word in database.Words)
            {
                WordItem copy = new WordItem(); CopyContent(word, copy);
                if (string.IsNullOrWhiteSpace(copy.SourceId)) copy.SourceId = "local:" + word.WordId;
                if (!ids.Add(copy.SourceId)) throw new InvalidDataException("来源编号重复，无法无歧义导出：" + copy.SourceId);
                exported.Words.Add(copy);
            }
            XmlFileStore.Save(path, exported);
        }
    }
}
