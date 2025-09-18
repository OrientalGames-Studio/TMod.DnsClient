using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMod.DnsClient.Utils
{
    internal class DnsMessageParser
    {
        internal static string ParseName(byte[] buffer, int offset, out int newOffset)
        {
            //StringBuilder sb = new();
            //newOffset = offset;
            //bool jumped = false;
            //int jumpOffset = 0;

            ////while ( true )
            ////{
            ////    byte len = buffer[newOffset++];
            ////    if ( len == 0 ) break;

            ////    if ( ( len & 0xC0 ) == 0xC0 )
            ////    {
            ////        int pointer = ((len & 0x3F) << 8) | buffer[newOffset++];
            ////        if ( !jumped )
            ////        {
            ////            jumpOffset = newOffset;
            ////            jumped = true;
            ////        }
            ////        newOffset = pointer;
            ////        continue;
            ////    }

            ////    if ( sb.Length > 0 ) sb.Append('.');
            ////    sb.Append(Encoding.ASCII.GetString(buffer, newOffset, len));
            ////    newOffset += len;
            ////}

            //while ( true )
            //{
            //    byte len = buffer[newOffset++];
            //    if ( len == 0 ) break;

            //    if ( ( len & 0xC0 ) == 0xC0 )
            //    {
            //        int pointer = ((len & 0x3F) << 8) | buffer[newOffset++];
            //        if ( !jumped )
            //        {
            //            jumpOffset = newOffset;
            //            jumped = true;
            //        }
            //        newOffset = pointer;
            //        continue;
            //    }

            //    if ( sb.Length > 0 ) sb.Append('.');
            //    sb.Append(Encoding.ASCII.GetString(buffer, newOffset, len));
            //    newOffset += len;
            //}

            //if ( jumped )
            //{
            //    newOffset = jumpOffset;
            //}
            //return sb.ToString();

            var sb = new StringBuilder();
            newOffset = offset;
            bool jumped = false;
            int jumpBack = -1;
            while ( true )
            {
                if ( newOffset >= buffer.Length ) throw new InvalidDataException("ReadName out of bounds");
                byte len = buffer[newOffset++];
                if ( len == 0 ) break;
                if ( ( len & 0xC0 ) == 0xC0 )
                {
                    // pointer: take next byte, compute pointer
                    if ( newOffset >= buffer.Length ) throw new InvalidDataException("ReadName pointer OOB");
                    int pointer = ((len & 0x3F) << 8) | buffer[newOffset++];
                    if ( !jumped )
                    {
                        jumpBack = newOffset;
                        jumped = true;
                    }
                    newOffset = pointer;
                    continue;
                }
                if ( sb.Length > 0 ) sb.Append('.');
                if ( newOffset + len > buffer.Length ) throw new InvalidDataException("Label OOB");
                sb.Append(Encoding.ASCII.GetString(buffer, newOffset, len));
                newOffset += len;
            }
            if ( jumped ) newOffset = jumpBack;
            return sb.ToString();
        }
    }
}
