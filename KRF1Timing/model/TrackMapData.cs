// Copyright 2018-2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using Newtonsoft.Json;
using System.Collections.Generic;

namespace adjsw.F12026
{
   public class TrackPoint
   {
      [JsonProperty("x")]
      public float X { get; set; }

      [JsonProperty("z")]
      public float Z { get; set; }
   }

   public class TrackMapData
   {
      [JsonProperty("trackId")]
      public int TrackId { get; set; }

      [JsonProperty("trackName")]
      public string TrackName { get; set; }

      /// <summary>
      /// Manually editable rotation in degrees applied to the stored points before rendering.
      /// Positive = clockwise. Cars are rotated with the same transform so they stay on track.
      /// </summary>
      [JsonProperty("rotationDegrees")]
      public double RotationDegrees { get; set; }

      [JsonProperty("points")]
      public List<TrackPoint> Points { get; set; } = new List<TrackPoint>();
   }
}
