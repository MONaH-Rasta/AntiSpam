# AntiSpam
Oxide plugin for Rust game. Filters spam and impersonation in player names and chat messages.

This plugin is about to filter spam and impersonation.

It supports 2 way of filtering: regex and blacklist. Regex should cover most cases, but if you need to filter something specific you can use both.

Default regex matches `IP`, `port`, `domain` with `subdomains`, `admin`, `moder` words and spam tags like `#BESTRUST`. It's case insensitive by the design (at least for english). If `Filter player names` is set to `true` player names will be checked. If player name is empty (or whitespace) player will be renamed into `Replacement for empty name` & 6 last diggits of his SteamID (`Player-123456` by default).

All checks are disabled by default, so you can install plugin safely and then change default config to your needs. You probably want to try enabling only regex list first, as it may be all you need. Afterwards you can always enable additional checks if needed.

* `Tip`: to extend the list of domains in regex, add new domains as follows:
*  `((\\p{L}|[0-9]|-) \\.) (domain1|domain2|domain3)"`

## Permissions

* `antispam.immune` -- Allows player to not being checked by this plugin

##  Configuration

```json
{
  "Enable logging": false,
  "Filter player names": false,
  "Filter chat messages": false,
  "Use regex": false,
  "Regex spam list": [
    "(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)",
    "(:\\d{3,5})",
    "(https|http|ftp|):\\/\\/",
    "((\\p{L}|[0-9]|-) \\.) (com|org|net|int|edu|gov|mil|ch|cn|co|de|eu|fr|in|nz|ru|tk|tr|uk|us)",
    "((\\p{L}|[0-9]|-) \\.) (ua|pro|io|dev|me|ml|tk|ml|ga|cf|gq|tf|money|pl|gg|net|info|cz|sk|nl)",
    "((\\p{L}|[0-9]|-) \\.) (store|shop)",
    "(\\# (. )?rust(. )?)",
    "((. )?rust(. )?\\# )"
  ],
  "Regex impersonation list": [
    "([Ааa4][Ддd][Ммm][Ииi1][Ннn])",
    "([Ммm][Ооo0][Ддd][Ееe3][Ррr])"
  ],
  "Use impersonation blacklist": false,
  "Impersonation blacklist": [
    "Admin",
    "Administrator",
    "Moder",
    "Moderator"
  ],
  "Use spam blacklist": false,
  "Spam blacklist": [
    "#SPAMRUST",
    "#BESTRUST"
  ],
  "Replacement for impersonation": "",
  "Replacement for spam": "",
  "Replacement for empty name": "Player-"
}
```

## API
### GetSpamFreeText
Plugins can call this API to clear text from spam.
```cs
string GetSpamFreeText(string text)
```

* **Example**:
```cs
string textWithSpam = "Some text with spam";
string textCleared = string.Empty;

if (AntiSpam != null && AntiSpam.IsLoaded)
{
    textCleared = AntiSpam.Call<string>("GetSpamFreeText", textWithSpam);
}
```

### GetImpersonationFreeText
Plugins can call this API to clear text from impersonation.
```cs
string GetImpersonationFreeText(string text)
```

* **Example**:
```cs
string textWithImpersonation = "Some text with impersonation";
string textCleared = string.Empty;

if (AntiSpam != null && AntiSpam.IsLoaded)
{
    textCleared = AntiSpam.Call<string>("GetImpersonationFreeText", textWithImpersonation);
}
```

## Credits

**Ultra**, the original author of AntiSpamNames plugin which this is inspired by