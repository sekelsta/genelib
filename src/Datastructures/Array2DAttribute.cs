using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Genelib {
    public class Array2DAttribute<T> {
        public T[,] value = null!;

        public virtual bool Equals(IWorldAccessor worldForResolve, IAttribute attr)
        {
            if (attr == null) return false;

            object othervalue = attr.GetValue();
            if (othervalue == null || !othervalue.GetType().IsArray) return false;

            if (othervalue is not Array otherArray) return false;

            if (value == null) return false;

            // Ensure both are 2D arrays and dimensions match
            if (value.Rank != 2 || otherArray.Rank != 2) return false;

            int rowsA = value.GetLength(0);
            int colsA = value.GetLength(1);
            int rowsB = otherArray.GetLength(0);
            int colsB = otherArray.GetLength(1);

            if (rowsA != rowsB || colsA != colsB) return false;

            for (int i = 0; i < rowsA; i++)
            {
                for (int j = 0; j < colsA; j++)
                {
                    object? valA = value[i, j];
                    object? valB = otherArray.GetValue(i, j);

                    if (valA == null)
                    {
                        if (valB != null) return false;
                    }
                    else if (!valA.Equals(valB))
                    {
                        if (!EqualityUtil.NumberEquals(valA, valB)) return false;
                    }
                }
            }

            return true;
        }

        public virtual object GetValue()
        {
            return value;
        }

        public virtual string ToJsonToken()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{ l0: ");
            sb.Append(value.GetLength(0));
            sb.Append(", l1: ");
            sb.Append(value.GetLength(1));
            sb.Append(", [");

            for (int i = 0; i < value.GetLength(0); ++i)
            {
                for (int j = 0; j < value.GetLength(1); ++j)
                {
                    if (i + j > 0) sb.Append(", ");

                    if (value[i,j] is IAttribute attr) sb.Append(attr.ToJsonToken());
                    else sb.Append(value[i,j]);    
                }
            }
            sb.Append("] }");

            return sb.ToString();
        }

        public override string ToString()
        {
            return ToJsonToken();
        }

        public override int GetHashCode()
        {
            int hashcode = 0;
            for (int i = 0; i < value.GetLength(0); ++i)
            {
                for (int j = 0; j < value.GetLength(1); ++j)
                {
                    hashcode ^= i * j * (value[i,j]?.GetHashCode() ?? 0);
                }
            }

            return hashcode;
        }
    }
}