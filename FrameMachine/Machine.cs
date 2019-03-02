using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FrameMachine
{
    class Machine
    {
        const int FrameCount    =     2;
        const int FrameSize     = 0x100;
        const int InputCell     =  0x00;
        const int OutputCell    =  FrameSize - 1;

        // a debugging variable
        public bool __Live__ = false;

        int[][] Frames = new int[FrameCount][];
        int FramePointer = 0;
        int[] CurrentFrame => Frames[FramePointer];
        int[] NextFrame => Frames[(FramePointer + 1) % FrameCount];
        public int[] CodeFrame = new int[FrameSize];
        public int Input
        {
            get => CurrentFrame[InputCell];
            set => CurrentFrame[InputCell] = value;
        }
        public int Output
        {
            get => CurrentFrame[OutputCell];
            private set => CurrentFrame[OutputCell] = value;
        }

        // 3x8 bit addr: cmp, arg0, arg1 -- 24 bits
        // 2 bits cmp type: z, nz, neg, pos -- 26 bits total
        // 3 bits neg select -- 29 bits total
        // 2 bits op select: nop, or, and, xor, -- 31 bits total
        // note: nop is important for storing data
        //       nop indicates cell is data

        // 0         8          16         24
        // | cmpaddr | arg0addr | arg1addr |
        // 24        26         28          31     32
        // | cmptype | opselect | negselect | zero |

        public enum InstOpSelect
        {
            Input, Or, And, Xor,
        }

        public enum InstCmpType
        {
            Zero, NotZero, Negative, Positive,
        }

        [Flags]
        public enum InstNegSelect
        {
            None    = 0,
            Arg0    = 1,
            Arg1    = 2,
            Result  = 4,
        }

        public struct Instruction
        {
            public int CmpAddr, Arg0Addr, Arg1Addr;
            public InstOpSelect     OpSelect;
            public InstCmpType      CmpType;
            public InstNegSelect    NegSelect;

            public static implicit operator Instruction(int value)
                => new Instruction
                {
                    CmpAddr     = value             & 0xff,
                    Arg0Addr    = (value >>  8)     & 0xff,
                    Arg1Addr    = (value >> 16)     & 0xff,
                    CmpType     = (InstCmpType)     ((value >> 24) & 0x03),
                    OpSelect    = (InstOpSelect)    ((value >> 26) & 0x03),
                    NegSelect   = (InstNegSelect)   ((value >> 28) & 0x07),
                };

            public static implicit operator int(Instruction inst)
                => (inst.CmpAddr        & 0xff)
                | ((inst.Arg0Addr       & 0xff) <<  8)
                | ((inst.Arg1Addr       & 0xff) << 16)
                | (((int)inst.CmpType   & 0x03) << 24)
                | (((int)inst.OpSelect  & 0x03) << 26)
                | (((int)inst.NegSelect & 0x07) << 28);
        }

        public Machine(
            int[] codeframe)
        {
            CodeFrame = codeframe
                ?? throw new ArgumentNullException(
                    "codeframe");
            if (CodeFrame.Length != FrameSize)
                throw new ArgumentException(string.Format(
                    "codeframe must be of length FrameSize ({0}).",
                    FrameSize));
            for (int i = 0; i < FrameCount; i++)
                Frames[i] = new int[FrameSize];
        }

        public Machine(
            IEnumerable<int> codeframe)
            : this(codeframe.ToArray())
        { }

        public void Cycle()
        {
            for (int i = 0; i < FrameSize; i++)
            {
                // decode instruction
                var inst = (Instruction)CodeFrame[i];
                
                // test comparison
                // note that the actual comparison is inverted
                // in order to continue the loop
                var testval = CurrentFrame[inst.CmpAddr];
                if (   ((inst.CmpType == InstCmpType.Zero) 
                        && (testval != 0))
                    || ((inst.CmpType == InstCmpType.NotZero) 
                        && (testval == 0))
                    || ((inst.CmpType == InstCmpType.Negative) 
                        && (testval >= 0))
                    || ((inst.CmpType == InstCmpType.Positive) 
                        && (testval <= 0)))
                    continue;

                void ExecuteOp(Func<int, int, int> op)
                {
                    var arg0 = CurrentFrame[inst.Arg0Addr];
                    var arg1 = CurrentFrame[inst.Arg1Addr];
                    if ((inst.NegSelect & InstNegSelect.Arg0)
                        == InstNegSelect.Arg0)
                        arg0 = ~arg0;
                    if ((inst.NegSelect & InstNegSelect.Arg1)
                        == InstNegSelect.Arg1)
                        arg1 = ~arg1;
                    var result = op(arg0, arg1);
                    if ((inst.NegSelect & InstNegSelect.Result)
                        == InstNegSelect.Result)
                        result = ~result;
                    NextFrame[i] = result;
                }

                // execute op ...
                switch (inst.OpSelect)
                {
                    case InstOpSelect.Or:
                        ExecuteOp((x, y) => x | y);
                        break;
                    case InstOpSelect.And:
                        ExecuteOp((x, y) => x & y);
                        break;
                    case InstOpSelect.Xor:
                        ExecuteOp((x, y) => x ^ y);
                        break;
                    case InstOpSelect.Input:
                        NextFrame[i] = Input;
                        break;
                    default:
                        break;
                }
            }

            // switch frames for next run
            FramePointer = (FramePointer + 1) % FrameCount;
        }

        // this shit is so slow
        static readonly RNGCryptoServiceProvider Rng =
            new RNGCryptoServiceProvider();
        static int NextRandomInt()
        {
            var buf = new byte[sizeof(int)];
            Rng.GetBytes(buf);
            return BitConverter.ToInt32(buf, 0);
        }
        static IEnumerable<int> RandomInts()
        {
            while (true)
                yield return NextRandomInt();
        }
        static int[] NextRandomIntArray(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(
                    "Length must be greater than zero.");
            return RandomInts().Take(length).ToArray();
        }
        static int[] NextRandomBuffer()
            => NextRandomIntArray(FrameSize);

        public static Machine Generate()
            => new Machine(NextRandomBuffer());

        public override string ToString()
        {
            return string.Format("{0:X8}", GetHashCode()); //base.ToString();
        }
    }
}
