// Copyright 2018-2024 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only


// Playback of UDP capture
using Razorvine.Pickle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class UdpPlaybackData
{
   public struct TimedUpdPacket
   {
      public UInt64 timestamp; // based on the first packet in data
      public byte[] data;
   }

   private TimedUpdPacket[] m_data = null;
   private UInt64 m_tsFirst = 0; // in ms

   public TimedUpdPacket[] GetPackets() { return m_data; }

   public UdpPlaybackData(string filename)
   {
      try
      {
         List<TimedUpdPacket> l = new List<TimedUpdPacket>();
         using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
         {
            Unpickler unpickler = new Unpickler();


            try
            {
               object obj = unpickler.load(stream);
               while (obj != null)
               {
                  m_ExtractDataFromPickleObj(obj, l);
                  obj = unpickler.load(stream);
               }
            }
            catch { }

         }
         m_data = l.ToArray();
      }
      catch (Exception e)
      { }
   }

   private UInt64 m_StringToUsTimestamp(string str)
   {
      if (str.Length < 15)
         return 0;

      int idx = 0;

      // parse: hh:mm:ss:uuuuuu
      uint h = (uint)(str[idx++] - '0');
      h *= 10;
      h += (uint)(str[idx++] - '0');

      if (h > 24)
         return 0;

      ++idx;
      uint m = (uint)(str[idx++] - '0');
      m *= 10;
      m += (uint)(str[idx++] - '0');
      if (m > 59)
         return 0;

      ++idx;
      uint s = (uint)(str[idx++] - '0');
      s *= 10;
      s += (uint)(str[idx++] - '0');
      if (s > 59)
         return 0;

      ++idx;
      uint uSec = 0;

      for (int i = 0; i < 5; ++i)
      {
         uSec += (uint)(str[idx++] - '0');
         uSec *= 10;
      }

      if (uSec > 999999)
         return 0;

      UInt64 newTimeStamp = h * ((UInt64)60 * 60 * 1000000);
      newTimeStamp += m * (60 * 1000000);
      newTimeStamp += s * (1000000);
      newTimeStamp += uSec;
      newTimeStamp /= 1000; // from uSec to mSec
      return newTimeStamp;
   }

   private void m_ExtractDataFromPickleObj(object o, List<TimedUpdPacket> l)
   {
      IEnumerable enumerable = o as IEnumerable;
      TimedUpdPacket p = new TimedUpdPacket();
      p.timestamp = 0;
      p.data = null;

      if (enumerable != null)
      {
         int idx = 0;
         foreach (object dat in enumerable)
         {
            if (idx == 0)
            {
               string time = dat as string;

               if (time != null)
               {
                  UInt64 ts = m_StringToUsTimestamp(time);
                  if (m_tsFirst == 0)
                  {
                     m_tsFirst = ts;
                  }

                  p.timestamp = ts - m_tsFirst;
               }
            }

            if (idx == 2)
            {
               byte[] data = dat as byte[];

               if (data != null)
               {
                  p.data = data;
               }
            }
            ++idx;
         }
      }

      if (p.data != null)
      {
         l.Add(p);
      }
   }

   private void m_ParseFile(string filename)
   {
      byte[] data = new byte[1024 * 1024];
      bool fileEnd = false;
      int idxStart = 0;
      int idxEnd = 0;
      using (var fs = File.Open(filename, FileMode.Open, FileAccess.Read))
      {
         // read 1 MB of file
         if (!fileEnd)
         {
            var spaceInBuffer = data.Length - idxEnd;
            var readLen = fs.Read(data, idxEnd, spaceInBuffer);
            if (readLen < spaceInBuffer)
               fileEnd = true;
         }

         while (idxStart < idxEnd)
         {


         }


         // copy remaining data to the start of buffer
         Array.Copy(data, idxStart, data, 0, data.Length - idxStart);
      }
   }

   private bool m_FindTelegram(int idxStart, int idxEnd, byte[] data, out UInt64 timestamp)
   {
      timestamp = 0;
      return true;
   }

   // compare if content of array1, starting from idxArray1, equals the content of array2
   // if array1 contains more data it is not an error
   private bool m_ArrayHeaderCompare(byte[] array1, int idxArray1, byte[] array2)
   {
      if ((array1.Length - idxArray1) < array2.Length)
         return false;

      for (int i = 0; i < array2.Length; i++) 
      {
        if (array1[i+idxArray1] != array2[i]) 
            return false;
      }

      return true;
   }

}