using System;
using System.Collections.Generic;
using System.IO;
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
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: textmerge [original] [a] [b]");
                return;
            }

            try
            {
                var chunks = Merger.Merge(File.ReadLines(args[0]), File.ReadLines(args[1]), File.ReadLines(args[2]),
                    StringComparer.InvariantCulture, Merger.TrimStripper);
                foreach (var chunk in chunks)
                {
                    switch (chunk.Kind)
                    {
                        case MergeKind.FromO:
                            Console.WriteLine(chunk.DiffA.OldLine.Item1);
                            break;
                        case MergeKind.FromA:
                            if (chunk.DiffA.DiffType == DiffType.Deleted)
                                break;
                            Console.WriteLine(chunk.DiffA.NewLine.Item1);
                            break;
                        case MergeKind.FromB:
                            if (chunk.DiffB.DiffType == DiffType.Deleted)
                                break;
                            Console.WriteLine(chunk.DiffB.NewLine.Item1);
                            break;
                        case MergeKind.Conflict:
                            Console.WriteLine("<<CONFLICT");
                            foreach (var opA in chunk.Conflict.DiffA)
                            {
                                switch (opA.DiffType)
                                {
                                    case DiffType.Unmodified:
                                        Console.WriteLine("A=>>{0}", opA.OldLine.Item1);
                                        break;
                                    case DiffType.Inserted:
                                        Console.WriteLine("A+>>{0}", opA.NewLine.Item1);
                                        break;
                                    case DiffType.Deleted:
                                        Console.WriteLine("A->>{0}", opA.OldLine.Item1);
                                        break;
                                }
                            }
                            foreach (var opB in chunk.Conflict.DiffB)
                            {
                                switch (opB.DiffType)
                                {
                                    case DiffType.Unmodified:
                                        Console.WriteLine("B=>>{0}", opB.OldLine.Item1);
                                        break;
                                    case DiffType.Inserted:
                                        Console.WriteLine("B+>>{0}", opB.NewLine.Item1);
                                        break;
                                    case DiffType.Deleted:
                                        Console.WriteLine("B->>{0}", opB.OldLine.Item1);
                                        break;
                                }
                            }
                            Console.WriteLine("CONFLICT>>");
                            break;
                    }
                }
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            
        }
    }
}
