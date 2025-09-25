using System;
using System.IO;
using Vintagestory.API.Datastructures;

namespace Genelib
{
    public class ByteArray2DAttribute : Array2DAttribute<byte>, IAttribute
    {
        private const int AttributeID = 4542;

        public ByteArray2DAttribute()
        {

        }

        public ByteArray2DAttribute(byte[,] value)
        {
            this.value = value;
        }

        public void ToBytes(BinaryWriter stream)
        {
            stream.Write((ushort)value.GetLength(0));
            stream.Write((ushort)value.GetLength(1));
            byte[] buffer = new byte[value.Length];
            Buffer.BlockCopy(value, 0, buffer, 0, buffer.Length);
            stream.Write(buffer);
        }

        public void FromBytes(BinaryReader stream)
        {
            int length0 = stream.ReadInt16();
            int length1 = stream.ReadInt16();
            value = new byte[length0, length1];
            byte[] buffer = stream.ReadBytes(length0 * length1);
            Buffer.BlockCopy(buffer, 0, value, 0, buffer.Length);
        }

        public int GetAttributeId()
        {
            return AttributeID;
        }

        public IAttribute Clone()
        {
            return new ByteArray2DAttribute((byte[,])value.Clone());
        }
    }
}
