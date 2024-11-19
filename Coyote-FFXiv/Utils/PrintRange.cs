using System;
using System.Collections.Generic;
using System.Linq;

namespace Coyote.Utils
{
    public static class EnumerableExtensions
    {
        public static string PrintRange(this IEnumerable<string> input, out string fullList, string noneStr = "Any")
        {
            fullList = string.Empty;
            var list = input?.ToArray() ?? Array.Empty<string>();

            if (list.Length == 0)
            {
                return noneStr;
            }

            if (list.Length == 1)
            {
                return list[0];
            }

            fullList = string.Join("\n", list);
            return $"{list.Length} selected";
        }
    }
}
