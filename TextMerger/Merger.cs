using System;
using System.Collections.Generic;
using System.Linq;

namespace TextMerger
{
    public enum ChangeType
    {
        Inserted,
        Deleted,
        Changed,
        Unmodified
    }

    public class CompareResult
    {
        public ChangeType ChangeType { get; private set; }
        public int? OldLineNum { get; private set; }
        public string OldLine { get; private set; }
        public int? NewLineNum { get; private set; }
        public string NewLine { get; private set; }

        public static CompareResult Inserted(int newLineNum, string newLine)
        {
            return new CompareResult
            {
                ChangeType = ChangeType.Inserted,
                NewLineNum = newLineNum,
                NewLine = newLine
            };
        }

        public static CompareResult Deleted(int oldLineNum, string oldLine)
        {
            return new CompareResult
            {
                ChangeType = ChangeType.Deleted,
                OldLineNum = oldLineNum,
                OldLine = oldLine
            };
        }

        public static CompareResult Changed(int oldLineNum, string oldLine, int newLineNum, string newLine)
        {
            return new CompareResult
            {
                ChangeType = ChangeType.Changed,
                OldLineNum = oldLineNum,
                OldLine = oldLine,
                NewLineNum = newLineNum,
                NewLine = newLine
            };
        }

        public static CompareResult Unmodified(int oldLineNum, int newLineNum, string line)
        {
            return new CompareResult
            {
                ChangeType = ChangeType.Unmodified,
                OldLineNum = oldLineNum,
                NewLineNum = newLineNum,
                OldLine = line,
                NewLine = line
            };
        }
    }

    public static class Merger
    {
        public static IEnumerable<Tuple<string, string>> TrimStripper(IEnumerable<string> input)
        {
            return input.Select(p => Tuple.Create(p, p.Trim()));
        }


        /// <summary>
        /// Implements primitive, not optimized algorithm from http://www.xmailserver.org/diff2.pdf
        /// Occupy O((N+M)^2) space where N - count of lines in old text, M - count of lines in new text
        /// </summary>
        /// <param name="oldText"></param>
        /// <param name="newText"></param>
        /// <param name="comparer"></param>
        /// <param name="stripper"></param>
        /// <returns></returns>
        public static void //IEnumerable<CompareResult> 
            ChangePath(IEnumerable<string> oldText, IEnumerable<string> newText, IComparer<string> comparer, Func<IEnumerable<string>, IEnumerable<Tuple<string, string>>> stripper)
        {
            var oldList = stripper(oldText).ToList();
            var newList = stripper(newText).ToList();
            var n = oldList.Count;
            var m = newList.Count;
            var matrix = new Tuple<int, bool?>[oldList.Count, newList.Count];

            Console.Write("   ");
            for (var j = 0; j < newList.Count; j++)
                Console.Write($" {newList[j].Item1}  ");
            Console.WriteLine();
            for (var i = 0; i < oldList.Count ; i++)
            {
                Console.Write($" {oldList[i].Item1} ");
                for (var j = 0; j < newList.Count ; j ++)
                {
                    if (comparer.Compare(oldList[i].Item2, newList[j].Item2) == 0)
                    {
                        if (i == 0 || j == 0)
                            matrix[i, j] = Tuple.Create(1, (bool?) null);
                        else
                            matrix[i, j] = Tuple.Create(matrix[i - 1, j - 1].Item1 + 1, (bool?) null);
                    }
                    else
                    {
                        var ov = i == 0 ? 0 : matrix[i - 1, j].Item1;
                        var nv = j == 0 ? 0 : matrix[i, j - 1].Item1;
                        if (ov == nv)
                            matrix[i, j] = Tuple.Create(ov, (bool?) true);
                        else
                            matrix[i, j] = Tuple.Create(ov > nv ? ov : nv, (bool?) (ov > nv));
                    }
                    var c = !matrix[i, j].Item2.HasValue ? "\\" : matrix[i, j].Item2.Value ? "|" : "-";
                Console.Write($" {matrix[i, j].Item1}{c} ");
                }
                Console.WriteLine();
                
            }
            var i1 = oldList.Count - 1;
            var j1 = newList.Count - 1;
            var lst = new List<string>();
            do
            {
                if (matrix[i1, j1].Item2 == null)
                {
                    lst.Add(oldList[i1].Item2);
                    i1--;
                    j1--;
                }
                else if (matrix[i1, j1].Item2 == true)
                    i1--;
                else
                    j1--;
            } while (i1 >= 0 && j1 >= 0);

            lst.Reverse();
            foreach (var s in lst)
            {
                Console.WriteLine(s);
            }
            Console.WriteLine("Old:");
            i1 = 0;
            foreach (var oi in oldList)
            {
                if(i1 >= lst.Count)
                    Console.WriteLine($"{oi.Item1} -" );
                else
                if (oi.Item1 == lst[i1])
                {
                    Console.WriteLine($"{oi.Item1}");
                    i1++;
                }
                else
                    Console.WriteLine($"{oi.Item1} -");
            }

            Console.WriteLine("New:");
            i1 = 0;
            foreach (var oi in newList)
            {
                if (i1 >= lst.Count)
                    Console.WriteLine($"{oi.Item1} +");
                else
                if (oi.Item1 == lst[i1])
                {
                    Console.WriteLine($"{oi.Item1}");
                    i1++;
                }
                else
                    Console.WriteLine($"{oi.Item1} +");
            }
        }

        

    }
}