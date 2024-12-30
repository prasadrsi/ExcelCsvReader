using System.Collections.Generic;
namespace Excel.Core.BinaryFormat
{
	/// <summary>
	/// Represents blank cell
	/// Base class for all cell types
	/// </summary>
	internal class XlsBiffBlankCell : XlsBiffRecord
	{
		internal XlsBiffBlankCell(byte[] bytes, uint offset)
			: base(bytes, offset)
		{
		}

		internal XlsBiffBlankCell(byte[] bytes)
			: this(bytes, 0)
		{
		}

		/// <summary>
		/// Zero-based index of row containing this cell
		/// </summary>
		public ushort RowIndex
		{
			get { return base.ReadUInt16(0x0); }
		}

		/// <summary>
		/// Zero-based index of column containing this cell
		/// </summary>
		public ushort ColumnIndex
		{
			get { return base.ReadUInt16(0x2); }
		}

		/// <summary>
		/// Format used for this cell
		/// </summary>
		public ushort XFormat
		{
			get { return base.ReadUInt16(0x4); }
		}
        
        /// <summary>
       /// Format used for this cell
       /// </summary>
        public ushort GetFormat(List<XlsBiffXF> extendedFormats)
        {
            switch(this.ID)
            {
                //BIFF2
                case BIFFRECORDTYPE.BLANK_OLD:
               case BIFFRECORDTYPE.BOOLERR_OLD:
                case BIFFRECORDTYPE.INTEGER_OLD:
                case BIFFRECORDTYPE.LABEL_OLD:
                case BIFFRECORDTYPE.NUMBER_OLD:
                   byte cellFormat = this.ReadByte(3);
                   cellFormat = (byte)(cellFormat & 0x3F);
                   return cellFormat;
                //other (BIFF3, BIFF4, BIFF5, BIFF8)
                default:
                     return extendedFormats[base.ReadUInt16(0x4)].Format;
            }
        }
	}
}