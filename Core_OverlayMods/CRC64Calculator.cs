using System;
using System.Linq;

namespace KoiSkinOverlayX
{
    internal static class CRC64Calculator
    {
        private const int width = 64;
        private const ulong polynomial = 0xE4C11DB7B4AC89F3;
        private const ulong initialValue = 0xFFFFFFFFFFFFFFFF;
        private const ulong xorOutValue = 0xFFFFFFFFFFFFFFFF;

        private static readonly ulong[] Crc64Table;

        static CRC64Calculator()
        {
            // Initialize CRC64 table
            Crc64Table = GenerateCrc64Table();
        }

        /// <summary>
        /// Calculate the numeric hash
        /// </summary>
        /// <param name="data">The data bytes to hash.</param>
        /// <param name="size">How many bytes to hash. Leave out to hash all.</param>
        public static ulong CalculateCRC64(byte[] data, int? size = null, int? sizeEnd = null, bool hashLen = false)
        {
            size = Math.Min(data.Length, size ?? data.Length);
            sizeEnd = Math.Min(data.Length, sizeEnd ?? data.Length);
            byte[] crcCheckVal = CalculateCheckValue(data, size.Value, sizeEnd.Value, hashLen);
            Array.Resize(ref crcCheckVal, 8);
            return BitConverter.ToUInt64(crcCheckVal, 0);
        }

        public static byte[] CalculateCheckValue(byte[] data, int size, int sizeEnd, bool hashLen)
        {
            if (data == null) return null;

            ulong crc = initialValue;

            // Hash the start
            int i;
            for (i = 0; i < size; i++)
            {
                crc = Crc64Table[((crc >> (width - 8)) ^ data[i]) & 0xFF] ^ (crc << 8);
                crc &= UInt64.MaxValue >> (64 - width);
            }

            // Hash the end
            int length = data.Length;
            int downEnd = length - sizeEnd - 1;
            for (i = length - 1; i > downEnd; i--)
            {
                crc = Crc64Table[((crc >> (width - 8)) ^ data[i]) & 0xFF] ^ (crc << 8);
                crc &= UInt64.MaxValue >> (64 - width);
            }

            // Hash the length
            if (hashLen)
            {
                byte[] lengthBytes = BitConverter.GetBytes(length);
                foreach (byte b in lengthBytes)
                {
                    crc = Crc64Table[((crc >> (width - 8)) ^ b) & 0xFF] ^ (crc << 8);
                    crc &= UInt64.MaxValue >> (64 - width);
                }
            }

            ulong crcFinalValue = crc ^ xorOutValue;
            return BitConverter.GetBytes(crcFinalValue).Take((width + 7) / 8).ToArray();
        }

        private static ulong[] GenerateCrc64Table()
        {
            var lookupTable = new ulong[256];
            ulong topBit = (ulong)1 << (width - 1);

            for (int i = 0; i < lookupTable.Length; i++)
            {
                byte inByte = (byte)i;

                ulong r = (ulong)inByte << (width - 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((r & topBit) != 0)
                    {
                        r = (r << 1) ^ polynomial;
                    }
                    else
                    {
                        r <<= 1;
                    }
                }

                lookupTable[i] = r & (UInt64.MaxValue >> (64 - width));
            }

            return lookupTable;
        }
    }
}