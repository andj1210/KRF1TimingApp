// Copyright 2018-2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using Razorvine.Pickle;
using System;
using System.IO;

namespace adjsw.F12025
{
   /// <summary>
   /// Records raw incoming UDP packets to pickle files compatible with UdpPlaybackData.
   /// Each F1 session gets its own file named &lt;sessionUID&gt;.pkl under a "recordings"
   /// subfolder next to the executable.
   /// Toggle recording with the R key.
   /// </summary>
   public class UdpSessionRecorder : IDisposable
   {
      public bool IsRecording => m_isRecording;

      /// <summary>Fired when recording starts, stops, or the active filename changes.</summary>
      public event Action<string> StatusChanged;

      /// <summary>Fired when an unrecoverable write error forces recording to stop.</summary>
      public event Action<string> RecordingError;

      public void Start()
      {
         m_isRecording = true;
         // File will be created on the first session-change notification or
         // immediately if a session is already active (handled by the caller).
         StatusChanged?.Invoke(StatusText());
      }

      public void Stop()
      {
         m_isRecording = false;
         CloseCurrentFile();
         StatusChanged?.Invoke(StatusText());
      }

      /// <summary>Toggle recording on/off.</summary>
      public void Toggle()
      {
         if (m_isRecording)
            Stop();
         else
            Start();
      }

      /// <summary>
      /// Call this after every m_mapper.Proceed() call.
      /// When the session UID changes the current file is closed and a new one
      /// is opened, so the triggering packet is written to the new file.
      /// </summary>
      public void NotifySessionChanged(ulong newSessionId)
      {
         if (!m_isRecording)
            return;

         CloseCurrentFile();
         OpenNewFile(newSessionId);
      }

      /// <summary>
      /// Write one raw UDP packet to the currently open recording file.
      /// No-op when not recording or no file is open yet.
      /// </summary>
      public void WritePacket(byte[] data)
      {
         if (!m_isRecording || m_stream == null)
            return;

         try
         {
            var now = DateTime.Now;
            // Format expected by UdpPlaybackData.m_StringToUsTimestamp: "HH:mm:ss:ffffff"
            string ts = now.ToString("HH:mm:ss") + ":"
                      + (now.Ticks / 10L % 1_000_000L).ToString("D6");

            // Fresh Pickler per record - no cross-record memo references,
            // each pickle object is fully self-contained as the reader expects.
            new Pickler().dump(new object[] { ts, "", data }, m_stream);
            m_stream.Flush();
         }
         catch (Exception ex)
         {
            CloseCurrentFile();
            RecordingError?.Invoke("UDP recording write error: " + ex.Message);
         }
      }

      public void Dispose()
      {
         CloseCurrentFile();
      }

      private void OpenNewFile(ulong sessionId)
      {
         string dir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "recordings");

         try
         {
            Directory.CreateDirectory(dir);
            string fileName = sessionId.ToString() + ".pkl";
            string fullPath = Path.Combine(dir, fileName);
            m_stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            m_currentFileName = fileName;
            StatusChanged?.Invoke(StatusText());
         }
         catch (Exception ex)
         {
            m_stream = null;
            m_currentFileName = "";
            RecordingError?.Invoke("Cannot open recording file: " + ex.Message);
         }
      }

      private void CloseCurrentFile()
      {
         if (m_stream != null)
         {
            try { m_stream.Flush(); } catch { }
            m_stream.Dispose();
            m_stream = null;
         }
         m_currentFileName = "";
      }

      private string StatusText()
      {
         if (!m_isRecording)
            return "";
         if (string.IsNullOrEmpty(m_currentFileName))
            return "● REC  [waiting for session…]";
         return "● REC  [" + m_currentFileName + "]";
      }

      private bool      m_isRecording     = false;
      private FileStream m_stream          = null;
      private string    m_currentFileName = "";
   }
}
