using System;

namespace Excel.Core.BinaryFormat
{
	/// <summary>
	/// Represents an RK number cell
	/// </summary>
	internal class XlsBiffRKCell : XlsBiffBlankCell
	{
		internal XlsBiffRKCell(byte[] bytes)
			: this(bytes, 0)
		{
		}

		internal XlsBiffRKCell(byte[] bytes, uint offset)
			: base(bytes, offset)
		{
		}

		/// <summary>
		/// Returns value of this cell
		/// </summary>
		public double Value
		{
			get { return NumFromRK(base.ReadUInt32(0x6)); }
		}



        /// <summary>
        /// Decodes RK-encoded number
        /// </summary>
        /// <param name="rk">Encoded number</param>
        /// <returns></returns>
        public static double StringFromRK(uint rk)
        {
            double num;
            if ((rk & 0x2) == 0x2)
            {
                num = (int)(rk >> 2 | ((rk & 0x80000000) == 0 ? 0 : 0xC0000000));
            }
            else
            {
                // hi words of IEEE num
                num = Helpers.Int64BitsToDouble(((long)(rk & 0xfffffffc) << 32));
            }
            if ((rk & 0x1) == 0x1)
                num /= 100; // divide by 100

            return num;
        }

		/// <summary>
		/// Decodes RK-encoded number
		/// </summary>
		/// <param name="rk">Encoded number</param>
		/// <returns></returns>
		public static double NumFromRK(uint rk)
		{
			double num;
			if ((rk & 0x2) == 0x2)
			{
                num = (int)(rk >> 2 | ((rk & 0x80000000) == 0 ? 0 : 0xC0000000));
			}
			else
			{
				// hi words of IEEE num
				num = Helpers.Int64BitsToDouble(((long)(rk & 0xfffffffc) << 32));
                string s = Helpers.Int64BitsToString(((long)(rk & 0xfffffffc) << 32));

			}
			if ((rk & 0x1) == 0x1)
				num /= 100; // divide by 100

			return num;
		}
	}
}