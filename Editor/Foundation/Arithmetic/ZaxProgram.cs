using System;
using System.Text;

namespace KusakaFactory.Zatools.Foundation.Arithmetic
{
    public readonly struct ZaxVariable
    {
        public readonly string Name;
        public readonly ZaxValueType Type;

        public ZaxVariable(string name, ZaxValueType type)
        {
            Name = name;
            Type = type;
        }
    }

    public sealed class ZaxProgram
    {
        public readonly string Source;
        public readonly ZaxInstruction[] Instructions;
        public readonly ZaxValue[] Constants;
        public readonly ZaxVariable[] Variables;
        public readonly int StackSize;
        public readonly ZaxValueType ResultType;

        public ZaxProgram(
            string source,
            ZaxInstruction[] instructions,
            ZaxValue[] constants,
            ZaxVariable[] variables,
            int stackSize,
            ZaxValueType resultType)
        {
            Source = source;
            Instructions = instructions;
            Constants = constants;
            Variables = variables;
            StackSize = stackSize;
            ResultType = resultType;
        }

        public int IndexOfVariable(string name)
        {
            for (var i = 0; i < Variables.Length; ++i)
            {
                if (string.Equals(Variables[i].Name, name, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        public string Disassemble()
        {
            var builder = new StringBuilder();
            builder
                .Append("; stack = ").Append(StackSize)
                .Append(", result = ").Append(ResultType.DisplayName()).Append('\n');
            for (var i = 0; i < Instructions.Length; ++i)
            {
                builder.Append(i.ToString("D4")).Append("  ").Append(Instructions[i]).Append('\n');
            }
            return builder.ToString();
        }
    }
}
