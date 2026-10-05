using System;

namespace NihongoDeskMemoWpf
{
    internal static class FeatureTests
    {
        private static int failures;

        private static void Main()
        {
            NihongoDeskMemo.AppConfig config = new NihongoDeskMemo.AppConfig();

            Check(config.ShowRatingButtons, "评级按钮默认显示");
            Check(ReviewPresentation.ShouldShowRatingButtons(config, true), "显示答案后可显示评级按钮");

            config.ShowRatingButtons = false;
            Check(!ReviewPresentation.ShouldShowRatingButtons(config, true), "关闭设置后隐藏评级按钮");
            Check(!ReviewPresentation.CanRateWithKeyboard(config, true), "关闭设置后禁用数字键评级");
            Check(!ReviewPresentation.ShouldShowRatingButtons(config, false), "答案未显示时评级按钮保持隐藏");

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
