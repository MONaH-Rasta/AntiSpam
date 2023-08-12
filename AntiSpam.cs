using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

using Pool = Facepunch.Pool;

namespace Oxide.Plugins
{
    [Info("Anti Spam", "MON@H", "2.1.0")]
    [Description("Filters spam and impersonation in player names and chat messages.")]

    class AntiSpam : CovalencePlugin
    {
        #region Variables

        [PluginReference] private readonly Plugin BetterChat;

        private const string PermissionImmunity = "antispam.immunity";
        private const string ColorAdmin = "#AAFF55";
        private const string ColorDeveloper = "#FFAA55";
        private const string ColorPlayer = "#55AAFF";

        private readonly StringBuilder _sb = new StringBuilder();
        
        private Regex _regexSpam;
        private Regex _regexImpersonation;
        private Regex _regexProfanities;

        private readonly object _true = true;

        #endregion Variables

        #region Initialization
        private void Init()
        {
            UnsubscribeHooks();
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionImmunity, this);

            CacheRegex();
            CacheProfanities();

            if (_configData.GlobalSettings.FilterPlayerNames)
            {
                foreach (IPlayer player in players.Connected)
                {
                    HandleName(player);
                }
            }

            SubscribeHooks();
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalSettings GlobalSettings = new GlobalSettings();

            [JsonProperty(PropertyName = "Spam settings")]
            public SpamSettings SpamSettings = new SpamSettings();

            [JsonProperty(PropertyName = "Impersonation settings")]
            public ImpersonationSettings ImpersonationSettings = new ImpersonationSettings();
        }

        private class GlobalSettings
        {
            [JsonProperty(PropertyName = "Enable logging")]
            public bool LoggingEnabled = false;

            [JsonProperty(PropertyName = "Filter chat messages")]
            public bool FilterChatMessages = false;

            [JsonProperty(PropertyName = "Filter player names")]
            public bool FilterPlayerNames = false;

            [JsonProperty(PropertyName = "Use UFilter plugin on player names")]
            public bool UFilterPlayerNames = false;

            [JsonProperty(PropertyName = "Replacement for empty name")]
            public string ReplacementEmptyName = "Player-";
        }

        private class SpamSettings
        {
            [JsonProperty(PropertyName = "Use regex")]
            public bool UseRegex = false;

            [JsonProperty(PropertyName = "Regex list", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RegexList = new List<string>()
            {
                "(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)",
                "(:\\d{3,5})",
                "(https|http|ftp|):\\/\\/",
                "((\\p{L}|[0-9]|-)+\\.)+(com|org|net|int|edu|gov|mil|ch|cn|co|de|eu|fr|in|nz|ru|tk|tr|uk|us)",
                "((\\p{L}|[0-9]|-)+\\.)+(ua|pro|io|dev|me|ml|tk|ml|ga|cf|gq|tf|money|pl|gg|net|info|cz|sk|nl)",
                "((\\p{L}|[0-9]|-)+\\.)+(store|shop)",
                "(\\#+(.+)?rust(.+)?)",
                "((.+)?rust(.+)?\\#+)"
            };

            [JsonProperty(PropertyName = "Use blacklist")]
            public bool UseBlacklist = false;

            [JsonProperty(PropertyName = "Blacklist", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Blacklist = new List<string>()
            {
                "#SPAMRUST",
                "#BESTRUST"
            };

            [JsonProperty(PropertyName = "Replacement for spam")]
            public string Replacement = "";
        }

        private class ImpersonationSettings
        {
            [JsonProperty(PropertyName = "Use regex")]
            public bool UseRegex = false;

            [JsonProperty(PropertyName = "Regex list", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RegexList = new List<string>()
            {
                "([Ааa4][Ддd][Ммm][Ииi1][Ннn])",
                "([Ммm][Ооo0][Ддd][Ееe3][Ррr])"
            };

            [JsonProperty(PropertyName = "Use blacklist")]
            public bool UseBlacklist = false;

            [JsonProperty(PropertyName = "Blacklist", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Blacklist = new List<string>()
            {
                "Admin",
                "Administrator",
                "Moder",
                "Moderator"
            };

            [JsonProperty(PropertyName = "Replacement for impersonation")]
            public string Replacement = "";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region Oxide Hooks

        private void OnUserConnected(IPlayer player) => HandleName(player);

        private void OnUserNameUpdated(string id, string oldName, string newName)
        {
            if (newName != oldName)
            {
                HandleName(players.FindPlayerById(id));
            }
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            IPlayer player = (IPlayer)data["Player"];
            string text = (string)data["Message"];

            if (string.IsNullOrWhiteSpace(text) || permission.UserHasPermission(player.Id, PermissionImmunity))
            {
                return null;
            }

            string newText = GetSpamFreeMessage(player, text);
            if (string.IsNullOrWhiteSpace(newText))
            {
                data["CancelOption"] = 2;
                return data;
            }

            if (newText != text)
            {
                data["Message"] = newText;
                return data;
            }

            return null;
        }

#if RUST
        private object OnPlayerChat(BasePlayer basePlayer, string message, ConVar.Chat.ChatChannel channel) => HandleChatMessage(basePlayer.IPlayer, message, (int)channel);
#else
        private object OnUserChat(IPlayer player, string message) => HandleChatMessage(player, message);
#endif


        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin == null)
            {
                return;
            }

            if (plugin.Name == "UFilter")
            {
                CacheProfanities();
            }
        }

        private void OnProfanityAdded(string profanity) => CacheProfanities();
        private void RemoveProfanity(string profanity) => CacheProfanities();

        #endregion Oxide Hooks

        #region Core Methods

        public void CacheRegex()
        {
            List<string> pattern = Pool.GetList<string>();

            if (_configData.SpamSettings.UseRegex)
            {
                pattern.AddRange(_configData.SpamSettings.RegexList);
            }

            if (_configData.SpamSettings.UseBlacklist)
            {
                foreach (string item in _configData.SpamSettings.Blacklist)
                {
                    pattern.Add($"^{Regex.Escape(item)}$");
                }
            }

            if (pattern.Count > 0)
            {
                _regexSpam = new Regex(string.Join("|", pattern), RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }

            pattern.Clear();

            if (_configData.ImpersonationSettings.UseRegex)
            {
                pattern.AddRange(_configData.ImpersonationSettings.RegexList);
            }

            if (_configData.ImpersonationSettings.UseBlacklist)
            {
                foreach (string item in _configData.ImpersonationSettings.Blacklist)
                {
                    pattern.Add($"^{Regex.Escape(item)}$");
                }
            }

            if (pattern.Count > 0)
            {
                _regexImpersonation = new Regex(string.Join("|", pattern), RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }

            Pool.FreeList(ref pattern);
        }

        public void CacheProfanities()
        {
            if (!_configData.GlobalSettings.UFilterPlayerNames)
            {
                _regexProfanities = null;
                return;
            }

            Plugin plugin = plugins.Find("UFilter");

            if (!IsPluginLoaded(plugin))
            {
                PrintWarning("Use UFilter plugin on chat messages is set to true in config, but the UFilter plugin is not loaded! Please load the UFilter plugin and then reload this plugin.");
                return;
            }

            if (plugin.Version < new VersionNumber(5, 1, 2))
            {
                PrintError("UFilter plugin must be version 5.1.2 or higher. Please update the UFilter plugin and then reload this plugin.");
                return;
            }

            List<string> pattern = Pool.GetList<string>();

            string[] profanities = plugin.Call("GetProfanities") as string[] ?? Array.Empty<string>();
            string[] allowedProfanity = plugin.Call("GetAllowedProfanity") as string[] ?? Array.Empty<string>();

            foreach (string profanity in profanities)
            {
                if (!allowedProfanity.Contains(profanity) && !pattern.Contains(profanity))
                {
                    pattern.Add($"^{Regex.Escape(profanity)}$");
                }
            }

            if (pattern.Count > 0)
            {
                pattern.Sort();
                _regexProfanities = new Regex(string.Join("|", pattern), RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }

            Pool.FreeList(ref pattern);
        }

        public void HandleName(IPlayer player)
        {
            if (player == null || !player.IsConnected || permission.UserHasPermission(player.Id, PermissionImmunity))
            {
                return;
            }

            string newName = GetClearName(player);
            if (newName != player.Name)
            {
                Log($"{player.Id} renaming '{player.Name}' to '{newName}'");
                player.Rename(newName);
            }
        }

        public object HandleChatMessage(IPlayer player, string message, int channel = 0)
        {
            if (string.IsNullOrWhiteSpace(message)
            || IsPluginLoaded(BetterChat)
            || permission.UserHasPermission(player.Id, PermissionImmunity))
            {
                return null;
            }

            string newText = GetSpamFreeMessage(player, message);
            if (string.IsNullOrWhiteSpace(newText))
            {
                return _true;
            }

            if (newText != message)
            {
                Broadcast(player, covalence.FormatText($"[{(player.IsAdmin ? ColorAdmin : ColorPlayer)}]{player.Name}[/#]: {newText}"), channel);

                return _true;
            }

            return null;
        }

        public string GetClearName(IPlayer player)
        {
            if (permission.UserHasPermission(player.Id, PermissionImmunity))
            {
                return player.Name;
            }

            string newName = GetSpamFreeText(player.Name);
            newName = GetImpersonationFreeText(newName);
            if (_regexProfanities != null)
            {
                newName = _regexProfanities.Replace(newName, _configData.SpamSettings.Replacement);
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                newName = $"{_configData.GlobalSettings.ReplacementEmptyName}{player.Id.Substring(11, 6)}";
            }

            return newName.Trim();
        }

        public string GetSpamFreeMessage(IPlayer player, string message)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            string newText = GetSpamFreeText(message);
            if (newText != message)
            {
                Log($"{player.Id} spam detected in message: {message}");

                return newText;
            }

            return message;
        }

        #endregion Core Methods

        #region API

        private string GetSpamFreeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return _regexSpam != null ? _regexSpam.Replace(text, _configData.SpamSettings.Replacement) : text;
        }

        private string GetImpersonationFreeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return _regexImpersonation != null ? _regexImpersonation.Replace(text, _configData.SpamSettings.Replacement) : text;
        }

        private Regex GetRegexImpersonation() => _regexImpersonation;
        private Regex GetRegexProfanities() => _regexProfanities;
        private Regex GetRegexSpam() => _regexSpam;

        #endregion API

        #region Helpers

        public void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnBetterChat));
#if RUST
            Unsubscribe(nameof(OnPlayerChat));
#else
            Unsubscribe(nameof(OnUserChat));
#endif
            Unsubscribe(nameof(OnUserConnected));
            Unsubscribe(nameof(OnUserNameUpdated));
            if (!_configData.GlobalSettings.UFilterPlayerNames)
            {
                Unsubscribe(nameof(OnPluginLoaded));
                Unsubscribe(nameof(OnProfanityAdded));
                Unsubscribe(nameof(RemoveProfanity));
            }
        }

        public void SubscribeHooks()
        {
            if (_configData.GlobalSettings.FilterPlayerNames)
            {
                Subscribe(nameof(OnUserConnected));
                Subscribe(nameof(OnUserNameUpdated));
            }

            if (_configData.GlobalSettings.FilterChatMessages)
            {
                Subscribe(nameof(OnBetterChat));
#if RUST
                Subscribe(nameof(OnPlayerChat));
#else
                Subscribe(nameof(OnUserChat));
#endif
            }
        }

        public bool IsPluginLoaded(Plugin plugin) => plugin != null && plugin.IsLoaded;

        public void Log(string text)
        {
            if (_configData.GlobalSettings.LoggingEnabled)
            {
                LogToFile("log", $"{DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")} {text}", this);
            }
        }

        public void Broadcast(IPlayer sender, string text, int channel = 0)
        {
#if RUST
            foreach (IPlayer target in players.Connected)
            {
                target.Command("chat.add", channel, sender.Id, text);
            }
#else
            server.Broadcast(text);
#endif
        }

        #endregion Helpers
    }
}