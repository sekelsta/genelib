using System;
using System.Collections;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Genelib {
    public class Array2DAttribute<T> {
        public T[,] value = null!;

        public virtual bool Equals(IWorldAccessor worldForResolve, IAttribute? attr)
        {
            if (attr == null) return false;

            if (attr.GetValue() is not T[,] otherArray) return false;


            if (otherArray.GetLength(0) != value.GetLength(0)) return false;
            if (otherArray.GetLength(1) != value.GetLength(1)) return false;

            for (int i = 0; i < value.GetLength(0); i++)
            {
                for (int j = 0; j < value.GetLength(1); j++)
                {
                    if (value[i, j] == null)
                    {
                        if (otherArray[i, j] != null) return false;
                    }
                    else if (!value[i, j].Equals(otherArray[i, j]))
                    {
                        return false;
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
