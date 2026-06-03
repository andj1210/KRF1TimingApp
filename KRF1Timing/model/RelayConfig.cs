// Copyright 2025 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.IO;
using Newtonsoft.Json;

namespace adjsw.F12026
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

      [JsonProperty("tls_enabled")]
      public bool TlsEnabled { get; set; } = false;

      /// <summary>
      /// Path to the server's self-signed certificate (.crt / .pem) for TLS pinning.
      /// Only required when tls_enabled is true.
      /// </summary>
      [JsonProperty("tls_cert_file")]
      public string TlsCertFile { get; set; } = "";

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
