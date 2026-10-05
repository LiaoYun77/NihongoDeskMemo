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

            HashSet<string> existingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WordItem item in database.Words)
                if (!string.IsNullOrEmpty(item.SourceId)) existingIds.Add(item.SourceId);
            ImportResult result = new ImportResult();
            foreach (WordItem item in incoming.Words)
            {
                if (existingIds.Contains(item.SourceId))
                {
                    result.Unchanged++;
                    continue;
                }
                // Import vocabulary only. Existing progress and edits are never overwritten.
                database.Words.Add(new WordItem
                {
                    Japanese = item.Japanese, Pinyin = item.Pinyin, Chinese = item.Chinese,
                    Unit = item.Unit, SourceId = item.SourceId, SourceText = item.SourceText
                });
                existingIds.Add(item.SourceId);
                result.Added++;
            }
            return result;
        }
    }
}
