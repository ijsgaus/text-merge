using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TextMerger
{
    public enum DiffType
    {
        Inserted,
        Deleted,
        Unmodified
    }

    public class DiffOp
    {
        public DiffType DiffType { get; private set; }
        public int? OldLineNum { get; private set; }
        public Tuple<string, string> OldLine { get; private set; }
        public int? NewLineNum { get; private set; }
        public Tuple<string, string> NewLine { get; private set; }

        public static DiffOp Inserted(int newLineNum, Tuple<string, string> newLine)
        {
            return new DiffOp
            {
                DiffType = DiffType.Inserted,
                NewLineNum = newLineNum,
                NewLine = newLine
            };
        }

        public static DiffOp Deleted(int oldLineNum, Tuple<string, string> oldLine)
        {
            return new DiffOp
            {
                DiffType = DiffType.Deleted,
                OldLineNum = oldLineNum,
                OldLine = oldLine
            };
        }

        public static DiffOp Unmodified(int oldLineNum, Tuple<string, string> oldLine, int newLineNum, Tuple<string, string> newLine)
        {
            return new DiffOp
            {
                DiffType = DiffType.Unmodified,
                OldLineNum = oldLineNum,
                NewLineNum = newLineNum,
                OldLine = oldLine,
                NewLine = newLine
            };
        }
    }

    public enum MergeKind
    {
        FromO,
        FromA,
        FromB,
        Conflict
    }

    public class MergeConflict
    {
        public List<DiffOp> DiffA { get; set; }
        public List<DiffOp> DiffB { get; set; }
    }

    public class MergeChunk
    {
        public MergeKind Kind { get; set; }
        public DiffOp DiffA { get; set; }
        public DiffOp DiffB { get; set; }
        public MergeConflict Conflict { get; set; }
    }

    public static class Merger
    {


        public static IEnumerable<Tuple<string, string>> TrimStripper(IEnumerable<string> input)
        {
            return input.Select(p => Tuple.Create(p, p.Trim()));
        }

        public static IEnumerable<MergeChunk> Merge(IEnumerable<string> textO, IEnumerable<string> textA, IEnumerable<string> textB,
            IComparer<string> comparer, Func<IEnumerable<string>, IEnumerable<Tuple<string, string>>> stripper)
        {
            // Take a A diff
            var diffA = Diff(textO, textA, comparer, stripper).ToList();
            // Take a B diff
            var diffB = Diff(textO, textB, comparer, stripper).ToList();

            // Take A diff stable part
            var stableA = diffA.Select((p, i) => new { Diff = p, Pos = i } )
                .Where(p => p.Diff.DiffType == DiffType.Unmodified).ToList();
            // Take B diff stable part
            var stableB = diffB.Select((p, i) => new { Diff = p, Pos = i })
                .Where(p => p.Diff.DiffType == DiffType.Unmodified).ToList();

            // Take common stable part
            var stableDiff = Diff(stableA.Select(p => p.Diff.OldLine.Item1), stableB.Select(p => p.Diff.OldLine.Item1), comparer,
                stripper).ToList();

            // Chankify merge
            var cda = 0;
            var cdb = 0;
            var merge = new List<MergeChunk>();
            foreach (var anchor in stableDiff.Where(p => p.DiffType == DiffType.Unmodified))
            {
                var aapos = stableA[anchor.OldLineNum.Value].Pos;
                var abpos = stableB[anchor.NewLineNum.Value].Pos;
                
                if (cda == aapos)
                {
                    // A do not have changes in chunk
                    while (cdb < abpos)
                    {
                        merge.Add(new MergeChunk
                        {
                            Kind = MergeKind.FromB,
                            DiffB = diffB[cdb]
                        });
                        cdb++;
                    }
                }
                else if (cdb == abpos)
                {
                    // B do not have changes in chunk
                    while (cda < aapos)
                    {
                        merge.Add(new MergeChunk
                        {
                            Kind = MergeKind.FromA,
                            DiffA = diffA[cda]
                        });
                        cda++;
                    }
                }
                else
                {
                    // A and B have changes in chunk - conflict
                    var conflict = new MergeConflict
                    {
                        DiffA = new List<DiffOp>(),
                        DiffB = new List<DiffOp>()
                    };
                    merge.Add(new MergeChunk { Conflict = conflict, Kind = MergeKind.Conflict });
                    while (cdb < abpos)
                    {
                        conflict.DiffB.Add(diffB[cdb]);
                        cdb++;
                    }
                    while (cda < aapos)
                    {
                        conflict.DiffA.Add(diffA[cda]);
                        cda++;
                    }
                }
                // add common stable chunk
                merge.Add(new MergeChunk
                {
                    Kind = MergeKind.FromO,
                    DiffA = diffA[cda],
                    DiffB = diffB[cdb]
                });
                cda++;
                cdb++;
            }

            
            if (cda >= diffA.Count)
            {
                // A don't have rest
                while (cdb < diffB.Count)
                {
                    merge.Add(new MergeChunk
                    {
                        Kind = MergeKind.FromB,
                        DiffB = diffB[cdb]
                    });
                    cdb++;
                }
            }
            else if (cdb >= diffB.Count)
            {
                // B don't have rest
                while (cda < diffA.Count)
                {
                    merge.Add(new MergeChunk
                    {
                        Kind = MergeKind.FromA,
                        DiffA = diffA[cda]
                    });
                    cda++;
                }
            }
            else
            {
                // A and B have a rest - Conflict
                var conflict = new MergeConflict
                {
                    DiffA = new List<DiffOp>(),
                    DiffB = new List<DiffOp>()
                };
                merge.Add(new MergeChunk { Conflict = conflict, Kind = MergeKind.Conflict});
                while (cdb < diffB.Count)
                {
                    conflict.DiffB.Add(diffB[cdb]);
                    cdb++;
                }
                while (cda < diffA.Count)
                {
                    conflict.DiffA.Add(diffA[cda]);
                    cda++;
                }
            }

            WriteMerges(merge);

            var resolved = merge;
            do
            {

                merge = resolved;
                resolved = new List<MergeChunk>();
                foreach(var chunk in merge) 
                {
                    if(chunk.Kind != MergeKind.Conflict)
                        resolved.Add(chunk);
                    else
                        resolved.AddRange(TryResolveConflict(chunk.Conflict, comparer));
                }
            } while (resolved.Count != merge.Count);
            return resolved;

        }


        private static IEnumerable<MergeChunk> TryResolveConflict(MergeConflict conflict, IComparer<string> comparer)
        {
            var resultA = conflict.DiffA.Where(p => p.DiffType != DiffType.Deleted).Select(p => p.NewLine).ToList();
            var resultB = conflict.DiffB.Where(p => p.DiffType != DiffType.Deleted).Select(p => p.NewLine).ToList();
            // Empty changes
            if ((resultA.Count == 0 && resultB.Count == 0) ||
                (resultA.All(p => string.IsNullOrWhiteSpace(p.Item2)) &&
                 resultB.All(p => string.IsNullOrWhiteSpace(p.Item2))
                    ))
                return Enumerable.Empty<MergeChunk>();
            // Full same
            if (resultB
                .Where(p => !string.IsNullOrWhiteSpace(p.Item2)).Select(p => p.Item2)
                .SequenceEqual(resultA.Where(p => !string.IsNullOrWhiteSpace(p.Item2)).Select(p => p.Item2),
                    EqualityComparer<string>((a, b) => comparer.Compare(a, b) == 0)))
            {
                return conflict.DiffA.Select(p => new MergeChunk {Kind = MergeKind.FromA, DiffA = p});
            }
            // Only in A
            if(conflict.DiffB.All(p => p.DiffType == DiffType.Unmodified))
                return conflict.DiffA.Select(p => new MergeChunk { Kind = MergeKind.FromA, DiffA = p });
            // Only in B
            if (conflict.DiffA.All(p => p.DiffType == DiffType.Unmodified))
                return conflict.DiffB.Select(p => new MergeChunk { Kind = MergeKind.FromB, DiffB = p });

            
            // All changes in A is the same as in B
            if(conflict.DiffA.All(p => conflict.DiffB.Any(t =>
            {
                switch (p.DiffType)
                {
                    case DiffType.Deleted:
                        return t.DiffType == p.DiffType && t.OldLineNum == p.OldLineNum;
                    case DiffType.Inserted:
                        return t.DiffType == p.DiffType && comparer.Compare(p.NewLine.Item2, t.NewLine.Item2) == 0;
                    case DiffType.Unmodified:
                        return t.DiffType == p.DiffType && t.OldLineNum == p.OldLineNum;
                    default:
                        return false;
                }
                
            })))
                return conflict.DiffB.Select(p => new MergeChunk {Kind = MergeKind.FromB, DiffB = p});

            // All changes in B is the same as in A
            if (conflict.DiffB.All(p => conflict.DiffA.Any(t =>
            {
                switch (p.DiffType)
                {
                    case DiffType.Deleted:
                        return t.DiffType == p.DiffType && t.OldLineNum == p.OldLineNum;
                    case DiffType.Inserted:
                        return t.DiffType == p.DiffType && comparer.Compare(p.NewLine.Item2, t.NewLine.Item2) == 0;
                    case DiffType.Unmodified:
                        return t.DiffType == p.DiffType && t.OldLineNum == p.OldLineNum;
                    default:
                        return false;
                }

            })))
                return conflict.DiffA.Select(p => new MergeChunk { Kind = MergeKind.FromA, DiffA = p });


            if (resultA.Count != 0 && resultB.Count != 0)
            {
                var matrix = MakeMatrix(comparer, resultB, resultA);

                var lcs = CreateLCS(resultB, resultA, matrix);
                // All apply in changes in B is in A
                if (lcs.Count == resultB.Count)
                    return conflict.DiffA.Select(p => new MergeChunk {Kind = MergeKind.FromA, DiffA = p});
                // All apply in changes in A is in B
                if (lcs.Count == resultA.Count)
                    return conflict.DiffB.Select(p => new MergeChunk {Kind = MergeKind.FromB, DiffB = p});
            }
            return Enumerable.Repeat(new MergeChunk() {Kind = MergeKind.Conflict, Conflict = conflict}, 1);
        }

        private class DelegateComparer<T> : IEqualityComparer<T>
        {
            private Func<T, T, bool> _compare;

            public DelegateComparer(Func<T, T, bool> compare)
            {
                _compare = compare;
            }


            public bool Equals(T x, T y)
            {
                return _compare(x, y);
            }

            public int GetHashCode(T obj)
            {
                return obj == null ? 0 : obj.GetHashCode();
            }
        }


        public static IEqualityComparer<T> EqualityComparer<T>(Func<T, T, bool> compare)
        {
            return new DelegateComparer<T>(compare);
        }

        /// <summary>
        /// Implements primitive, not optimized algorithm LCS generation 
        /// Occupy O(N*M) space where N - count of lines in old text, M - count of lines in new text
        /// </summary>
        /// <param name="oldText"></param>
        /// <param name="newText"></param>
        /// <param name="comparer"></param>
        /// <param name="stripper"></param>
        /// <returns></returns>
        public static IEnumerable<DiffOp> Diff(IEnumerable<string> oldText, IEnumerable<string> newText, IComparer<string> comparer, Func<IEnumerable<string>, IEnumerable<Tuple<string, string>>> stripper)
        {
            var oldList = stripper(oldText).ToList();
            var newList = stripper(newText).ToList();
            Tuple<int, BackRef>[,] matrix = MakeMatrix(comparer, oldList, newList);

            WriteMatrix(oldList, newList, matrix);

            var lst = CreateLCS(oldList, newList, matrix);

            WriteLCS(lst);
            var diff = CreateDiff(comparer, lst, oldList, newList);

            WriteDiff(diff);
            return diff;
        }

        private static List<DiffOp> CreateDiff(IComparer<string> comparer, List<string> lcs, List<Tuple<string, string>> oldList, List<Tuple<string, string>> newList)
        {
            var diff = new List<DiffOp>();
            var indO = 0;
            var indN = 0;
            foreach (var line in lcs)
            {
                while (comparer.Compare(oldList[indO].Item2, line) != 0)
                {
                    diff.Add(DiffOp.Deleted(indO, oldList[indO]));
                    indO++;
                }
                while (comparer.Compare(newList[indN].Item2, line) != 0)
                {
                    diff.Add(DiffOp.Inserted(indN, newList[indN]));
                    indN++;
                }
                diff.Add(DiffOp.Unmodified(indO, oldList[indO], indN, newList[indN]));
                indN++;
                indO++;
            }
            while (indO < oldList.Count)
            {
                diff.Add(DiffOp.Deleted(indO, oldList[indO]));
                indO++;
            }
            while (indN < newList.Count)
            {
                diff.Add(DiffOp.Inserted(indN, newList[indN]));
                indN++;
            }
            return diff;
        }

        private enum BackRef
        {
            V,
            G,
            D
        }

        private static List<string> CreateLCS(List<Tuple<string, string>> oldList, List<Tuple<string, string>> newList, Tuple<int, BackRef>[,] matrix)
        {
            var i1 = oldList.Count - 1;
            var j1 = newList.Count - 1;

            var lst = new List<string>();
            do
            {
                switch (matrix[i1, j1].Item2)
                {
                    case BackRef.D:
                        lst.Add(oldList[i1].Item2);
                        i1--;
                        j1--;
                        break;
                    case BackRef.V:
                        i1--;
                        break;
                    case BackRef.G:
                        j1--;
                        break;
                }
            } while (i1 >= 0 && j1 >= 0);

            lst.Reverse();
            return lst;
        }

        private static Tuple<int, BackRef>[,] MakeMatrix(IComparer<string> comparer, List<Tuple<string, string>> oldList, List<Tuple<string, string>> newList)
        {
            var matrix = new Tuple<int, BackRef>[oldList.Count, newList.Count];

            for (var i = 0; i < oldList.Count; i++)
            {
                for (var j = 0; j < newList.Count; j++)
                {
                    if (comparer.Compare(oldList[i].Item2, newList[j].Item2) == 0)
                    {
                        if (i == 0 || j == 0)
                            matrix[i, j] = Tuple.Create(1, BackRef.D);
                        else
                            matrix[i, j] = Tuple.Create(matrix[i - 1, j - 1].Item1 + 1, BackRef.D);
                    }
                    else
                    {
                        var ov = i == 0 ? 0 : matrix[i - 1, j].Item1;
                        var nv = j == 0 ? 0 : matrix[i, j - 1].Item1;
                        if (ov == nv)
                            matrix[i, j] = Tuple.Create(ov, BackRef.G);
                        else if (ov > nv)
                            matrix[i, j] = Tuple.Create(ov, BackRef.V);
                        else
                            matrix[i, j] = Tuple.Create(nv, BackRef.G);
                    }
                }
            }

            return matrix;
        }

#if TEST
        private static void WriteMerges(List<MergeChunk> merge)
        {
            foreach (var chunk in merge)
            {
                switch (chunk.Kind)
                {
                    case MergeKind.FromO:
                        Console.WriteLine($"O>> {chunk.DiffA.OldLine.Item1}");
                        break;
                    case MergeKind.FromA:
                        switch (chunk.DiffA.DiffType)
                        {
                            case DiffType.Unmodified:
                                Console.WriteLine($"A=> {chunk.DiffA.NewLine.Item1}");
                                break;
                            case DiffType.Deleted:
                                Console.WriteLine($"A-> {chunk.DiffA.OldLine.Item1}");
                                break;
                            case DiffType.Inserted:
                                Console.WriteLine($"A+> {chunk.DiffA.NewLine.Item1}");
                                break;
                        }
                        break;
                    case MergeKind.FromB:
                        switch (chunk.DiffB.DiffType)
                        {
                            case DiffType.Unmodified:
                                Console.WriteLine($"B=> {chunk.DiffB.NewLine.Item1}");
                                break;
                            case DiffType.Deleted:
                                Console.WriteLine($"B-> {chunk.DiffB.OldLine.Item1}");
                                break;
                            case DiffType.Inserted:
                                Console.WriteLine($"B+> {chunk.DiffB.NewLine.Item1}");
                                break;
                        }
                        break;
                    case MergeKind.Conflict:
                        Console.WriteLine(">>Conflict");
                        foreach (var aOp in chunk.Conflict.DiffA)
                        {
                            switch (aOp.DiffType)
                            {
                                case DiffType.Unmodified:
                                    Console.WriteLine($"A=> {aOp.NewLine.Item1}");
                                    break;
                                case DiffType.Deleted:
                                    Console.WriteLine($"A-> {aOp.OldLine.Item1}");
                                    break;
                                case DiffType.Inserted:
                                    Console.WriteLine($"A+> {aOp.NewLine.Item1}");
                                    break;
                            }
                        }
                        foreach (var bOp in chunk.Conflict.DiffB)
                        {
                            switch (bOp.DiffType)
                            {
                                case DiffType.Unmodified:
                                    Console.WriteLine($"B=> {bOp.NewLine.Item1}");
                                    break;
                                case DiffType.Deleted:
                                    Console.WriteLine($"B-> {bOp.OldLine.Item1}");
                                    break;
                                case DiffType.Inserted:
                                    Console.WriteLine($"B+> {bOp.NewLine.Item1}");
                                    break;
                            }
                        }
                        Console.WriteLine("<<Conflict");
                        break;
                }
            }
        }

        private static void WriteDiff(List<DiffOp> diff)
        {
            Console.WriteLine(" O OI OS NI NS");
            foreach (var op in diff)
            {
                switch (op.DiffType)
                {
                    case DiffType.Deleted:
                        Console.WriteLine($" -  {op.OldLineNum}  {op.OldLine.Item1}");
                        break;
                    case DiffType.Inserted:
                        Console.WriteLine($" +        {op.NewLineNum}  {op.NewLine.Item1}");
                        break;
                    case DiffType.Unmodified:
                        Console.WriteLine($" *  {op.OldLineNum}  {op.OldLine.Item1}  {op.NewLineNum}  {op.NewLine.Item1}");
                        break;
                }
            }
        }

        private static void WriteLCS(List<string> lst)
        {
            Console.WriteLine("LCS:");
            foreach (var s in lst)
            {
                Console.WriteLine(s);
            }
        }

 
        private static void WriteMatrix(List<Tuple<string, string>> oldList, List<Tuple<string, string>> newList,
            Tuple<int, BackRef>[,] matrix)
        {
            Console.Write("   ");
            foreach (var t in newList)
                Console.Write($" {t.Item1}  ");
            Console.WriteLine();
            for (var i = 0; i < oldList.Count; i++)
            {
                Console.Write($" {oldList[i].Item1} ");
                for (var j = 0; j < newList.Count; j++)
                {
                    var cell = matrix[i, j];
                    var c = 
                        cell.Item2 == BackRef.D 
                            ? "\\" 
                            : cell.Item2 == BackRef.V ? "|" : "-";
                    Console.Write($" {cell.Item1}{c} ");
                }
                Console.WriteLine();

            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteMerges(List<MergeChunk> merge)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteDiff(List<DiffOp> diff)
        {
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteLCS(List<string> lst)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteMatrix(List<Tuple<string, string>> oldList, List<Tuple<string, string>> newList, Tuple<int, BackRef>[,] matrix)
        {
        }
#endif
    }
}