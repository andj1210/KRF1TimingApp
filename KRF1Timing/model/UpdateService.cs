// Copyright 2026 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace adjsw.F12026
{
   /// <summary>
   /// GitHub-release based self-updater. All network access happens only via the
   /// public methods, which are invoked from the explicit "Check for Updates"
   /// sidebar action -- nothing is contacted automatically at startup.
   ///
   /// Flow: download the latest release zip -> extract into the "_update"
   /// subfolder -> launch the updater batch shipped *inside that release* (so the
   /// updater logic always matches the new version) -> quit. The batch waits for
   /// us to exit, copies the files up into the install folder and relaunches.
   /// </summary>
   public class UpdateService
   {
      // -- config -------------------------------------------------------------

      private const string Owner = "andj1210";
      private const string Repo  = "KRF1TimingApp";
      private const string ApiLatest =
         "https://api.github.com/repos/" + Owner + "/" + Repo + "/releases/latest";

      // Subfolder (under the install dir) the release is extracted into.
      private const string UpdateDirName     = "_update";
      // Shipped inert (not a clickable .bat); renamed to the runnable name inside
      // _update just before launch.
      private const string UpdaterStagedName = "kr1timing-update.ba_";
      private const string UpdaterBatName    = "kr1timing-update.bat";

      // -- nested types -------------------------------------------------------

      public class ReleaseInfo
      {
         public string  Tag     { get; set; }
         public Version Version { get; set; }
         public string  ZipUrl  { get; set; }
         public string  HtmlUrl { get; set; }
      }

      // -- fields -------------------------------------------------------------

      private static readonly HttpClient s_http = _CreateClient();

      // -- public surface -----------------------------------------------------

      /// <summary>Local (running) version, taken from the build-stamped VERSION file.</summary>
      public Version LocalVersion => _ParseVersion(BuildVersion.Value);

      /// <summary>Queries GitHub for the latest release. Network call -- user-triggered only.</summary>
      public async Task<ReleaseInfo> CheckAsync()
      {
         string json = await s_http.GetStringAsync(ApiLatest);
         JObject rel = JObject.Parse(json);

         string zipUrl = null;
         JToken assets = rel["assets"];
         if (assets != null)
         {
            foreach (JToken asset in assets)
            {
               string name = (string)asset["name"];
               if (!string.IsNullOrEmpty(name) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
               {
                  zipUrl = (string)asset["browser_download_url"];
                  break;
               }
            }
         }

         string tag = (string)rel["tag_name"];
         return new ReleaseInfo
         {
            Tag     = tag,
            Version = _ParseVersion(tag),
            ZipUrl  = zipUrl,
            HtmlUrl = (string)rel["html_url"]
         };
      }

      public bool IsNewer(ReleaseInfo rel)
      {
         return rel != null && rel.Version != null && rel.Version > LocalVersion;
      }

      /// <summary>
      /// Downloads the release zip, extracts it into the "_update" subfolder,
      /// launches the shipped updater batch and shuts the app down so the files
      /// can be replaced.
      /// </summary>
      public async Task DownloadAndApplyAsync(ReleaseInfo rel)
      {
         if (rel == null || string.IsNullOrEmpty(rel.ZipUrl))
            throw new InvalidOperationException("Release has no downloadable .zip asset.");

         string installDir = AppContext.BaseDirectory.TrimEnd('\\');
         string updateDir  = Path.Combine(installDir, UpdateDirName);

         // fresh staging folder
         if (Directory.Exists(updateDir))
            Directory.Delete(updateDir, true);
         Directory.CreateDirectory(updateDir);

         // 1. download to a temp file
         string zipPath = Path.Combine(Path.GetTempPath(), "krf1_update_" + Guid.NewGuid().ToString("N") + ".zip");
         byte[] data = await s_http.GetByteArrayAsync(rel.ZipUrl);
         File.WriteAllBytes(zipPath, data);

         // 2. extract into _update (in-process -- no external unzip needed)
         try
         {
            ZipFile.ExtractToDirectory(zipPath, updateDir);
         }
         finally
         {
            try { File.Delete(zipPath); } catch { }
         }

         // 3. the updater logic always comes from the freshly downloaded release.
         //    It ships inert as ".ba_"; rename it to the runnable ".bat" inside
         //    _update. The release zip wraps everything in a top-level folder, so
         //    search for it rather than assuming a fixed depth.
         string[] staged = Directory.GetFiles(updateDir, UpdaterStagedName, SearchOption.AllDirectories);
         if (staged.Length == 0)
            throw new FileNotFoundException("Downloaded release does not contain " + UpdaterStagedName + " -- cannot self-update.");

         string bat = Path.Combine(Path.GetDirectoryName(staged[0]), UpdaterBatName);
         if (File.Exists(bat))
            File.Delete(bat);
         File.Move(staged[0], bat);

         // 4. launch the batch in its own console window, passing our PID so it can
         //    wait for us to exit. The batch derives the install folder as "..\.."
         //    from its own location inside _update.
         int pid = Process.GetCurrentProcess().Id;
         var psi = new ProcessStartInfo
         {
            FileName         = bat,
            Arguments        = pid.ToString(),
            WorkingDirectory = Path.GetDirectoryName(bat),
            UseShellExecute  = true
         };
         Process.Start(psi);

         // 5. quit so the batch can copy over the running files
         Application.Current.Shutdown();
      }

      // -- helpers ------------------------------------------------------------

      private static HttpClient _CreateClient()
      {
         var c = new HttpClient();
         c.DefaultRequestHeaders.UserAgent.ParseAdd("KRF1Timing-Updater"); // GitHub 403s without a UA
         c.Timeout = TimeSpan.FromSeconds(30);
         return c;
      }

      private static Version _ParseVersion(string tag)
      {
         if (string.IsNullOrEmpty(tag))
            return null;

         // tolerate "V0.7.0", "v1.2", "0.99.0"
         string v = tag.TrimStart('v', 'V', ' ');
         return Version.TryParse(v, out Version parsed) ? parsed : null;
      }
   }
}
