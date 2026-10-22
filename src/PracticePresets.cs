using System.Collections.Generic;

namespace NihongoDeskMemo
{
    public sealed class PracticePreset
    {
        public string Name { get; set; }
        public string[] Units { get; set; }

        public PracticePreset() { Name = string.Empty; Units = new string[0]; }
    }

    internal static class PracticePresets
    {
        public static List<PracticePreset> Copy(IEnumerable<PracticePreset> source)
        {
            List<PracticePreset> result = new List<PracticePreset>();
            if (source != null)
                foreach (PracticePreset item in source)
                    if (item != null) result.Add(new PracticePreset { Name = item.Name,
                        Units = item.Units == null ? new string[0] : (string[])item.Units.Clone() });
            return result;
        }
    }
}
