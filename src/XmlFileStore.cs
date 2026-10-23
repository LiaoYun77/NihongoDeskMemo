using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace NihongoDeskMemo
{
    internal static class XmlFileStore
    {
        private static readonly List<string> recoveries = new List<string>();
        public static string RecoveryMessage { get { return string.Join("\n", recoveries.ToArray()); } }

        public static T Load<T>(string path, T fallback)
        {
            string source;
            return Load(path, fallback, out source);
        }

        public static T Load<T>(string path, T fallback, out string source)
        {
            source = path;
            T value;
            if (TryRead(path, out value)) return value;
            if (TryRead(path + ".bak", out value))
            {
                source = path + ".bak";
                recoveries.Add(Path.GetFileName(path) + " 无法读取，已从上次成功保存的备份载入。原文件仍保留。");
                return value;
            }
            if (!File.Exists(path) && !File.Exists(path + ".bak")) { source = null; return fallback; }
            throw new InvalidDataException(Path.GetFileName(path) + " 与其备份均无法读取。请从 backups 恢复，避免覆盖原数据。");
        }

        private static bool TryRead<T>(string path, out T value)
        {
            value = default(T);
            if (!File.Exists(path)) return false;
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    value = (T)new XmlSerializer(typeof(T)).Deserialize(reader);
                return value != null;
            }
            catch (InvalidOperationException) { return false; }
            catch (XmlException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public static void Save<T>(string path, T value)
        {
            if (value == null) throw new ArgumentNullException("value");
            path = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { CheckCharacters = true, Indent = true, CloseOutput = false }))
                        new XmlSerializer(typeof(T)).Serialize(writer, value);
                    stream.Flush(true);
                }
                if (!File.Exists(path)) { File.Move(temporary, path); return; }
                T previous;
                bool valid = TryRead(path, out previous);
                string backup = path + ".bak";
                if (valid)
                {
                    string backups = Path.Combine(directory, "backups");
                    Directory.CreateDirectory(backups);
                    string daily = Path.Combine(backups, Path.GetFileNameWithoutExtension(path) + "-auto-" + DateTime.Now.ToString("yyyyMMdd") + ".xml");
                    if (!File.Exists(daily)) File.Copy(path, daily, false);
                }
                else backup = path + ".damaged-" + Guid.NewGuid().ToString("N") + ".xml";
                // Same-volume replacement keeps the previous valid file until the new one is ready.
                File.Replace(temporary, path, backup, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
