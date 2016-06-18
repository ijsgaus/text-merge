using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextMerger;

namespace TextMerge
{
    class Program
    {
        static void Main(string[] args)
        {
            var oldv = new[] { "a", "b", "c", "a", "b", "b", "a" };
            var newv = new[] { "c", "b", "a", "b", "a", "c" };

            Action<IEnumerable<string>, IEnumerable<string>> p = (o, n) => Merger.ChangePath(n, o, StringComparer.InvariantCulture, Merger.TrimStripper);
            p(oldv, newv);
        }
    }
}
