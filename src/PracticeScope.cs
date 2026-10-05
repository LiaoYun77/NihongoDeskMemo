using System;
using System.Collections.Generic;

namespace NihongoDeskMemoWpf
{
    internal static class PracticeScope
    {
        public static List<string> GetUnits(NihongoDeskMemo.AppConfig config)
        {
            // Missing collection means an old single-unit config; empty means all units.
            IEnumerable<string> values = config.PracticeUnits ??
                (IEnumerable<string>)new string[] { config.PracticeUnit };
            List<string> units = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values)
            {
                string unit = NihongoDeskMemo.Storage.SafeTrim(value);
                if (unit.Length > 0 && seen.Add(unit)) units.Add(unit);
            }
            return units;
        }

        public static void SetUnits(NihongoDeskMemo.AppConfig config, IEnumerable<string> units)
        {
            config.PracticeUnits = new List<string>(units).ToArray();
            config.PracticeUnits = GetUnits(config).ToArray();
            config.PracticeUnit = config.PracticeUnits.Length == 1 ? config.PracticeUnits[0] : string.Empty;
        }

        public static bool Contains(NihongoDeskMemo.AppConfig config, string unit)
        {
            List<string> units = GetUnits(config);
            return units.Count == 0 || units.Exists(delegate(string selected)
            {
                return string.Equals(selected, NihongoDeskMemo.Storage.SafeTrim(unit), StringComparison.OrdinalIgnoreCase);
            });
        }

        public static string Summary(NihongoDeskMemo.AppConfig config)
        {
            List<string> units = GetUnits(config);
            if (units.Count == 0) return "全部词库";
            if (units.Count <= 3) return string.Join("、", units.ToArray());
            return "已选 " + units.Count + " 个单元";
        }
    }
}
