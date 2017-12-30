﻿using System.Collections.Generic;
using ArkBot.Configuration;
using Discord;

namespace ArkBot
{
    public interface IConfig
    {
        ArkMultipliersConfigSection ArkMultipliers { get; set; }
        string BotName { get; set; }
        string BotUrl { get; set; }
        string AppUrl { get; set; }
        string GoogleApiKey { get; set; }
        string SteamApiKey { get; set; }
        string TempFileOutputDirPath { get; set; }
        DiscordConfigSection Discord { get; set; }
        Dictionary<string, string[]> UserRoles { get; set; }
        AccessControlConfigSection AccessControl { get; set; }
        BackupsConfigSection Backups { get; set; }
        string WebApiListenPrefix { get; set; }
        string WebAppListenPrefix { get; set; }
        string[] WebAppRedirectListenPrefix { get; set; }
        string PowershellFilePath { get; set; }
        bool UseCompatibilityChangeWatcher { get; set; }
        SslConfigSection Ssl { get; set; }
        int? SavegameExtractionMaxDegreeOfParallelism { get; set; }
        bool AnonymizeWebApiData { get; set; }
        LogSeverity DiscordLogLevel { get; set; }

        ServerConfigSection[] Servers { get; set; }
        ClusterConfigSection[] Clusters { get; set; }
    }
}