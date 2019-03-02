using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FrameMachine
{
    class Program
    {

        static int Sine(int x) 
            => (int)(int.MaxValue * Math.Sin((double)x / 4)) - 2;

        static IEnumerable<int> SineWave()
        {
            for (int i = 0; ; i = unchecked(i + 1))
            {
                yield return Sine(i);
            }
        }

        static IEnumerable<int> PseudoRandomWave()
        {
            var prng = new Random();
            while (true)
                yield return prng.Next(int.MinValue, int.MaxValue);
        }

        static (int livem, int liveo) RunTest(int machcount, int cyclecount, IEnumerable<int> input)
        {
            var machines = Enumerable.Range(0, machcount).Select(x => Machine.Generate()).ToArray();

            int liveoutcount = 0;

            foreach (var x in input.Take(cyclecount))
            {
                Parallel.ForEach(machines, m =>
                {
                    m.Input = x;
                    m.Cycle();
                    // Debug.Assert((m.Output != 0) && (m.Output != -1), "we got a live one!");
                    //Console.WriteLine(mach.Output);
                    if ((m.Output != 0) && (m.Output != -1))
                    {
                        liveoutcount++;
                        m.__Live__ = true;
                    }
                });
            }
            return (machines.Count(m => m.__Live__), liveoutcount);
        }

        static void LifeTest()
        {
            using (var fs = File.OpenWrite("lifetest.csv"))
            using (var writer = new StreamWriter(fs))
            {
                var header = "mcount,\tccount,\tlivem,\tliveo,";
                Console.WriteLine(header);
                writer.WriteLine(header);

                for (int i = 4; i <= short.MaxValue / 4; i <<= 1)
                {
                    for (int j = 4; j <= short.MaxValue / 4; j <<= 1)
                    {
                        var runcount = string.Format("{0}, {1}, ", i, j);
                        Console.Write(runcount);
                        writer.Write(runcount);

                        (int liveo, int livem) = RunTest(i, j, SineWave());

                        var result = string.Format("{0}, {1},", livem, liveo);
                        Console.WriteLine(result);
                        writer.WriteLine(result);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("-- FRAME MACHINE --");
            Console.WriteLine();

            var last = 0;
            var next = 0;
            IEnumerable<int> SineHyst()
            {
                foreach (var x in SineWave())
                {
                    next = x;
                    yield return last;
                    last = next;
                }
            }

            IEnumerable<int> PrngHyst()
            {
                foreach (var x in PseudoRandomWave())
                {
                    next = x;
                    yield return last;
                    last = next;
                }
            }

            var rng =
                new RNGCryptoServiceProvider();
            int NextRandomInt()
            {
                var buf = new byte[sizeof(int)];
                rng.GetBytes(buf);
                return BitConverter.ToInt32(buf, 0);
            }
            IEnumerable<int> RandomInts()
            {
                while (true)
                    yield return NextRandomInt();
            }
            int[] NextRandomIntArray(int length)
            {
                if (length < 0)
                    throw new ArgumentOutOfRangeException(
                        "Length must be greater than zero.");
                return RandomInts().Take(length).ToArray();
            }

            var runcount = 0;

            var pool =
                new Pool<int, int, int>(
                    x => x, x => x,
                    (i, r, o) =>
                    {
                        var trendup = (o & 1) == 1;
                        return
                            (trendup && (next > i))
                            || (!trendup && (next <= i))
                            ? r + 1
                            : r - 1;
                    },
                    // d =>
                    // {
                    //     var sixteenth = Pool.PoolSize / 16;
                    //     var topdogs = d
                    //         .OrderByDescending(kvp => kvp.Value)
                    //         .Take(sixteenth)
                    //         .Select(kvp => kvp.Key)
                    //         .ToArray();
                    //     Console.WriteLine("--- selection top results ---");
                    //     foreach (var dog in topdogs)
                    //         Console.Out.WriteLine("{0}, {1}", dog, d[dog]);
                    //     Console.WriteLine();
                    //     // make half mutated, half random
                    //     var splitcount = (Pool.PoolSize - topdogs.Length) / 2;
                    //     var muts = Enumerable.Range(0, splitcount)
                    //         .Select(i => new Machine(
                    //             topdogs[i % topdogs.Length].CodeFrame));
                    //     foreach (var mut in muts)
                    //     {
                    //         foreach (var r 
                    //             in NextRandomIntArray(
                    //                 mut.CodeFrame.Length / 16))
                    //         {
                    //             mut.CodeFrame[
                    //                 Math.Abs(r % mut.CodeFrame.Length)] 
                    //                 = NextRandomInt();
                    //         }
                    //     }
                    //     var runts = Enumerable.Range(0, splitcount)
                    //         .Select(x => Machine.Generate());
                    // 
                    //     return topdogs.Concat(muts.Concat(runts));
                    // },
                    d =>
                    {
                        var topset = d
                            .OrderByDescending(kvp => kvp.Value)
                            .Take(2 * (int)Math.Sqrt(Pool.PoolSize))
                            .Select(kvp => kvp.Key)
                            .ToArray();

                        var topmode = topset
                            .GroupBy(m => d[m])
                            .OrderByDescending(r => r.Count())
                            .First()
                            .Key;

                        Console.WriteLine(
                            "--- selection {0}, top {1} results ---",
                            runcount++, topset.Length);
                        foreach (var m in topset)
                            Console.Out.WriteLine("{0}, {1}", m, d[m]);
                        Console.WriteLine();
                        var rand = NextRandomInt();
                        var muts = topset.Take(topset.Length / 2)
                            .SelectMany(
                                x => topset.Take(topset.Length / 2).Select(
                                    y => new Machine(
                                        from i in Enumerable.Range(0, x.CodeFrame.Length)
                                        select
                                            rand > 2 * (int.MaxValue / 3)
                                                ? x.CodeFrame[i]
                                            : rand < 2 * (int.MinValue / 3)
                                                ? y.CodeFrame[i]
                                            : NextRandomInt())))
                            .ToArray();

                        // insert mutations, ~ 16 per mut, random mut
                        // foreach (var i in Enumerable.Range(0, muts.Length * 16))
                        //     muts[
                        //         Math.Abs(
                        //             NextRandomInt()
                        //             % muts.Length)].CodeFrame[
                        //             Math.Abs(
                        //                 NextRandomInt() 
                        //                 % muts[i % 16].CodeFrame.Length)]
                        //         = NextRandomInt();

                        var survivors = topset.TakeWhile(m => d[m] != topmode);
                        if (survivors.Count() < 2)
                            survivors = topset.Take(2);

                        return muts.Take(muts.Length - topset.Length).Concat(survivors);//.Concat(topset);

                        // return topset.Zip(topset, (x, y) 
                        //     => new Machine(
                        //         from a in x.CodeFrame
                        //         from b in y.CodeFrame
                        //         select a ^ b));
                    },
                    0x10000,
                    SineHyst());//.Take(0x800000));

            pool.Start();


            Console.WriteLine("done.");
            Console.ReadKey(true);
        }
    }
}
