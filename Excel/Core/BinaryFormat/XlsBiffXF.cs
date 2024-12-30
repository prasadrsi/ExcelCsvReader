using System;
using System.Collections.Generic;
using System.Text;
using Excel.Exceptions;

namespace Excel.Core.BinaryFormat
{

    internal class XlsBiffXF : XlsBiffRecord
    {
        internal XlsBiffXF(byte[] bytes, uint offset)
            : base(bytes, offset)
        {
        }

        internal XlsBiffXF(byte[] bytes)
            : this(bytes, 0)
        {
        }


        /// <summary>
        /// Returns FORMAT for specified column
        /// </summary>
        /// <returns></returns>
        public ushort Format
        {
            get
            {
                switch (this.ID)
                {
                    case BIFFRECORDTYPE.XF:
                        return this.ReadUInt16(2);
                    case BIFFRECORDTYPE.XF_V2:
                        byte cellFormat = this.ReadByte(2);
                        cellFormat = (byte)(cellFormat & 0x3F);
                        return cellFormat;
                    case BIFFRECORDTYPE.XF_V3:
                    case BIFFRECORDTYPE.XF_V4:
                        return this.ReadByte(1);
                    default:
                        throw new BiffRecordException("Unknown XF type!");
                }

            }
        }
    }
}
