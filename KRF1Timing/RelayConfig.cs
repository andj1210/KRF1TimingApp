// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.IO;
using Newtonsoft.Json;

namespace adjsw.F12025
{
   /// <summary>
   /// Loads relay server configuration from relay_config.json in the app directory.
   /// If the file does not exist, the relay feature is completely hidden.
   /// </summary>
   public class RelayConfig
   {
      [JsonProperty("server")]
      public string Server { get; set; } = "127.0.0.1";

      [JsonProperty("port")]
      public int Port { get; set; } = 9877;

      /// <summary>
      /// Try to load the config. Returns null if the file does not exist or is invalid.
      /// </summary>
      public static RelayConfig TryLoad()
      {
         try
         {
            string path = Path.Combine(
               AppDomain.CurrentDomain.BaseDirectory, "relay_config.json");

            if (!File.Exists(path))
               return null;

            string json = File.ReadAllText(path);
            var cfg = JsonConvert.DeserializeObject<RelayConfig>(json);
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.Server))
               return null;

            return cfg;
         }
         catch
         {
            return null;
         }
      }
   }
}
