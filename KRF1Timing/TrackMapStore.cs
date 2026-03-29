// Copyright 2018-2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace adjsw.F12025
{
   /// <summary>
   /// Loads and caches track layout JSON files from ./tracks/track{id}.json.
   /// Call Save() after learning to write (and invalidate cache for) that track.
   /// </summary>
   public static class TrackMapStore
   {
      /// <summary>
      /// Returns the TrackMapData for the given track, or null if the file does not exist
      /// or cannot be parsed.  Results are cached in memory.
      /// </summary>
      public static TrackMapData Load(Track track)
      {
         if (s_cache.TryGetValue(track, out var cached))
            return cached; // may be null (negative cache entry)

         string path = FilePath(track);
         if (!File.Exists(path))
         {
            s_cache[track] = null;
            return null;
         }

         try
         {
            string json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<TrackMapData>(json);
            s_cache[track] = data;
            return data;
         }
         catch
         {
            s_cache[track] = null;
            return null;
         }
      }

      /// <summary>
      /// Serialises data to ./tracks/track{id}.json and refreshes the cache entry.
      /// The tracks directory is created if it does not exist.
      /// </summary>
      public static void Save(TrackMapData data)
      {
         string dir = TracksDirectory();
         Directory.CreateDirectory(dir);

         string path = FilePath((Track)data.TrackId);
         string json = JsonConvert.SerializeObject(data, Formatting.Indented);
         File.WriteAllText(path, json);

         s_cache[(Track)data.TrackId] = data;
      }

      public static string FilePath(Track track)
      {
         return Path.Combine(TracksDirectory(), $"track{(int)track}.json");
      }

      private static string TracksDirectory()
      {
         return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tracks");
      }

      private static readonly Dictionary<Track, TrackMapData> s_cache =
         new Dictionary<Track, TrackMapData>();
   }
}
