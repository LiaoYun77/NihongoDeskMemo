using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace NihongoDeskMemoWpf
{
    internal static class FeatureTests
    {
        private static int failures;

        private static void Main()
        {
            NihongoDeskMemo.AppConfig config = new NihongoDeskMemo.AppConfig();

            Check(config.ShowRatingButtons, "评级按钮默认显示");
            Check(config.HideHotkey == 10, "旧配置默认使用 F10");
            Check(GlobalHideHotkey.NormalizeKey(12) == 12 && GlobalHideHotkey.NormalizeKey(11) == 11,
                "支持 F11 和 F12");
            Check(GlobalHideHotkey.NormalizeKey(8) == 10, "无效隐藏键恢复默认，避免占用答案键");
            Check(ReviewPresentation.ShouldShowRatingButtons(config, true), "显示答案后可显示评级按钮");

            config.ShowRatingButtons = false;
            Check(!ReviewPresentation.ShouldShowRatingButtons(config, true), "关闭设置后隐藏评级按钮");
            Check(!ReviewPresentation.CanRateWithKeyboard(config, true), "关闭设置后禁用数字键评级");
            Check(!ReviewPresentation.ShouldShowRatingButtons(config, false), "答案未显示时评级按钮保持隐藏");

            XmlSerializer serializer = new XmlSerializer(typeof(NihongoDeskMemo.AppConfig));
            config = (NihongoDeskMemo.AppConfig)serializer.Deserialize(new StringReader(
                "<AppConfig><PracticeUnit>第01课</PracticeUnit></AppConfig>"));
            Check(PracticeScope.Contains(config, "第01课"), "旧单课设置迁移");
            Check(config.HideHotkey == 10, "缺少快捷键字段的 XML 兼容 F10");
            config.HideHotkey = 12;
            Check(!PracticeScope.Contains(config, "第02课"), "旧单课不扩大为全部");
            PracticeScope.SetUnits(config, new string[] { "第01课", "第03课", " 第03课 " });
            Check(PracticeScope.GetUnits(config).Count == 2, "多课设置去除重复项");
            Check(PracticeScope.Contains(config, "第01课") && PracticeScope.Contains(config, "第03课"), "包含多个选定单元");
            Check(!PracticeScope.Contains(config, "第02课") && !PracticeScope.Contains(config, ""), "排除未选及未分组词条");
            StringWriter saved = new StringWriter();
            serializer.Serialize(saved, config);
            config = (NihongoDeskMemo.AppConfig)serializer.Deserialize(new StringReader(saved.ToString()));
            Check(PracticeScope.GetUnits(config).Count == 2 && !PracticeScope.Contains(config, "第02课"), "多选配置保存重读一致");
            Check(config.HideHotkey == 12, "F12 设置序列化后保留");
            PracticeScope.SetUnits(config, new string[0]);
            Check(PracticeScope.Contains(config, "第02课") && PracticeScope.Contains(config, ""), "全部范围包含未分组词条");
            config.PracticeUnits = new string[] { "Missing unit" };
            Check(!PracticeScope.Contains(config, "第01课"), "已删除的课次不回退为全部");

            if (failures > 0)
            {
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine("Feature tests passed.");
        }

        private static void Check(bool condition, string name)
        {
            if (condition)
            {
                return;
            }

            failures++;
            Console.Error.WriteLine("FAILED: " + name);
        }
    }
}
