namespace PlayniteBridge.Services
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Web.Script.Serialization;
    using PlayniteBridge.Helpers;

    internal class PluginIntegrationService
    {
        private static readonly string SuccessStoryGuid = "cebe6d32-8c46-4459-b993-5a5189d60788";
        private static readonly string GameActivityGuid = "afbb1a0d-04a1-4d0c-9afa-c6e42ca855b4";

        private readonly string _extensionsDataPath;
        private readonly Func<string, string> _readFile;

        public PluginIntegrationService(string extensionsDataPath, Func<string, string> readFile = null)
        {
            _extensionsDataPath = extensionsDataPath;
            _readFile = readFile ?? (path => File.ReadAllText(path, Encoding.UTF8));
        }

        public object GetAchievements(Guid gameId, string gameName)
        {
            var filePath = Path.Combine(_extensionsDataPath, SuccessStoryGuid, "SuccessStory", gameId.ToString() + ".json");
            if (!File.Exists(filePath))
                return new { gameId = gameId.ToString(), gameName, installed = false, message = "SuccessStory plugin data not found for this game" };

            try
            {
                var json = _readFile(filePath);
                return ParseAchievements(json, gameId, gameName);
            }
            catch (Exception ex)
            {
                return new { error = "Failed to read SuccessStory data: " + ex.Message, code = 500 };
            }
        }

        public object ParseAchievements(string json, Guid gameId, string gameName)
        {
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var data = js.Deserialize<Dictionary<string, object>>(json);

            var items = data.GetValueOrNull("Items") as ArrayList;
            var achievements = new List<object>();
            var unlockedCount = 0;
            if (items != null)
            {
                foreach (Dictionary<string, object> a in items)
                {
                    var dateUnlocked = a.ContainsKey("DateUnlocked") ? (a["DateUnlocked"] != null ? a["DateUnlocked"].ToString() : null) : null;
                    var isUnlocked = dateUnlocked != null && !dateUnlocked.StartsWith("0001");
                    if (isUnlocked) unlockedCount++;
                    achievements.Add(new
                    {
                        name = a.GetValueOrNull("Name"),
                        description = a.GetValueOrNull("Description"),
                        unlocked = isUnlocked,
                        dateUnlocked = isUnlocked ? dateUnlocked : null,
                        percent = a.ContainsKey("Percent") ? a["Percent"] : null,
                        gamerScore = a.ContainsKey("GamerScore") ? a["GamerScore"] : null,
                        isHidden = a.ContainsKey("IsHidden") && (bool)a["IsHidden"]
                    });
                }
            }

            var source = data.GetValueOrNull("SourcesLink") as Dictionary<string, object>;

            return new
            {
                gameId = gameId.ToString(),
                gameName,
                installed = true,
                source = source?.GetValueOrNull("Name"),
                total = achievements.Count,
                unlocked = unlockedCount,
                locked = achievements.Count - unlockedCount,
                achievements
            };
        }

        public object GetActivity(Guid gameId, string gameName)
        {
            var filePath = Path.Combine(_extensionsDataPath, GameActivityGuid, "GameActivity", gameId.ToString() + ".json");
            if (!File.Exists(filePath))
                return new { gameId = gameId.ToString(), gameName, installed = false, message = "GameActivity plugin data not found for this game" };

            try
            {
                var json = _readFile(filePath);
                return ParseActivity(json, gameId, gameName);
            }
            catch (Exception ex)
            {
                return new { error = "Failed to read GameActivity data: " + ex.Message, code = 500 };
            }
        }

        public object ParseActivity(string json, Guid gameId, string gameName)
        {
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var data = js.Deserialize<Dictionary<string, object>>(json);

            var items = data.GetValueOrNull("Items") as ArrayList;
            var sessions = new List<object>();
            if (items != null)
            {
                foreach (Dictionary<string, object> s in items)
                {
                    sessions.Add(new
                    {
                        date = s.GetValueOrNull("DateSession"),
                        elapsedSeconds = s.ContainsKey("ElapsedSeconds") ? s["ElapsedSeconds"] : 0,
                        action = s.GetValueOrNull("GameActionName")
                    });
                }
            }

            var totalSeconds = data.ContainsKey("SessionPlaytime") ? data["SessionPlaytime"] : 0;

            return new
            {
                gameId = gameId.ToString(),
                gameName,
                installed = true,
                totalSeconds,
                totalHours = Math.Round(Convert.ToDouble(totalSeconds) / 3600.0, 1),
                sessionCount = sessions.Count,
                sessions
            };
        }
    }
}
