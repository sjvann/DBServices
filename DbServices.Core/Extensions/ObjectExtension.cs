using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbServices.Core.Extensions
{
    public static class ObjectExtension
    {
        public static bool IsNumber(this object value)
        {
            return value is sbyte
                || value is byte
                || value is short
                || value is int
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal
                || value is ushort
                || value is uint
                || value is ulong;
        }
    }
}
