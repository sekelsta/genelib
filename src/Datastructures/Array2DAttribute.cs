using System;
using System.Collections;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Genelib {
    public class Array2DAttribute<T> {
        public T[,] value;

        public virtual bool Equals(IWorldAccessor worldForResolve, IAttribute attr)
        {
            object othervalue = attr.GetValue();
            if (!othervalue.GetType().IsArray) return false;

            IList a = (IList)value;
            IList b = (IList)othervalue;

            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    if (!EqualityUtil.NumberEquals(a[i], b[i])) return false;
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

                    if (value[i,j] is IAttribute)
                    {
                        sb.Append((value[i,j] as IAttribute).ToJsonToken());
                    } else
                    {
                        sb.Append(value[i,j]);
                    }         
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
                    hashcode ^= value[i,j].GetHashCode();
                }
            }

            return hashcode;
        }
    }
}
