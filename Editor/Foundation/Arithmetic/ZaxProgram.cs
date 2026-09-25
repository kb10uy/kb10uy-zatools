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
        public readonly ZaxValueType[] ResultTypes;

        public ZaxProgram(
            string source,
            ZaxInstruction[] instructions,
            ZaxValue[] constants,
            ZaxVariable[] variables,
            int stackSize,
            ZaxValueType[] resultTypes)
        {
            if (resultTypes == null || resultTypes.Length == 0) throw new ArgumentException("program must produce at least one value", nameof(resultTypes));

            Source = source;
            Instructions = instructions;
            Constants = constants;
            Variables = variables;
            StackSize = stackSize;
            ResultTypes = resultTypes;
        }

        public int ResultCount => ResultTypes.Length;

        public ZaxValueType ResultType
        {
            get
            {
                if (ResultTypes.Length != 1) throw new InvalidOperationException($"program produces {ResultTypes.Length} values; use {nameof(ResultTypes)}");
                return ResultTypes[0];
            }
        }

        public string ResultDisplayName => ZaxValueTypeEx.DisplayName(ResultTypes);

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
                .Append(", result = ").Append(ResultDisplayName).Append('\n');
            for (var i = 0; i < Instructions.Length; ++i)
            {
                builder.Append(i.ToString("D4")).Append("  ").Append(Instructions[i]).Append('\n');
            }
            return builder.ToString();
        }
    }
}
