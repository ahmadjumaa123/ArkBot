﻿using ArkBot.Ark;
using ArkBot.Database;
using ArkBot.Extensions;
using ArkBot.Helpers;
using ArkBot.ViewModel;
using ArkBot.WebApi.Model;
using ArkSavegameToolkitNet.Domain;
using Discord;
using QueryMaster.GameServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;

namespace ArkBot.WebApi.Controllers
{
    [AccessControl("pages", "home")]
    public class ServersController : BaseApiController
    {
        private EfDatabaseContextFactory _databaseContextFactory;
        private ArkContextManager _contextManager;
        private Discord.DiscordManager _discordManager;
        private IArkServerService _serverService;

        public ServersController(
            IConfig config,
            EfDatabaseContextFactory databaseContextFactory,  
            ArkContextManager contextManager, 
            Discord.DiscordManager discordManager,
            IArkServerService serverService) : base(config)
        {
            _databaseContextFactory = databaseContextFactory;
            _contextManager = contextManager;
            _discordManager = discordManager;
            _serverService = serverService;
        }

        public async Task<ServerStatusAllViewModel> Get()
        {
            var demoMode = IsDemoMode() ? new DemoMode() : null;
            var result = new ServerStatusAllViewModel { User = WebApiHelper.GetUser(Request, _config), AccessControl = BuildViewModelForAccessControl(_config) };

            foreach (var context in _contextManager.Servers)
            {
                var serverContext = _contextManager.GetServer(context.Config.Key);
                var status = serverContext.Steam.GetServerStatusCached();
                if (status == null || status.Item1 == null || status.Item2 == null)
                {
                    //Server status is currently unavailable
                }
                else
                {
                    var info = status.Item1;
                    var rules = status.Item2;
                    var playerinfos = status.Item3;

                    var m = new Regex(@"^(?<name>.+?)\s+-\s+\(v(?<version>\d+\.\d+)\)$", RegexOptions.IgnoreCase | RegexOptions.Singleline).Match(info.Name);
                    var name = m.Success ? m.Groups["name"].Value : info.Name;
                    var version = m.Success ? m.Groups["version"] : null;
                    var currentTime = rules.FirstOrDefault(x => x.Name == "DayTime_s")?.Value;
                    var tamedDinosCount = context.TamedCreatures?.Count();
                    var uploadedDinosCount = context.CloudCreatures?.Count();
                    var wildDinosCount = context.WildCreatures?.Count();
                    //var tamedDinosMax = 6000; //todo: remove hardcoded value
                    var structuresCount = context.Structures?.Count();
                    var totalPlayers = context.Players?.Count();
                    var totalTribes = context.Tribes?.Count();
                    var serverStarted = _serverService.GetServerStartTime(context.Config.Key);

                    var nextUpdate = context.ApproxTimeUntilNextUpdate;
                    var nextUpdateTmp = nextUpdate?.ToStringCustom();
                    var nextUpdateString = (nextUpdate.HasValue ? (!string.IsNullOrWhiteSpace(nextUpdateTmp) ? $"~{nextUpdateTmp}" : "waiting for new update ...") : null);
                    var lastUpdate = context.LastUpdate;
                    var lastUpdateString = lastUpdate.ToStringWithRelativeDay();

                    var sr = new ServerStatusViewModel
                    {
                        Key = context.Config.Key,
                        Name = name,
                        Address = context.Config.DisplayAddress ?? info.Address,
                        Version = version.ToString(),
                        OnlinePlayerCount = info.Players,
                        OnlinePlayerMax = info.MaxPlayers,
                        MapName = info.Map,
                        InGameTime = currentTime,
                        TamedCreatureCount = tamedDinosCount ?? 0,
                        CloudCreatureCount = uploadedDinosCount ?? 0,
                        WildCreatureCount = wildDinosCount ?? 0,
                        StructureCount = structuresCount ?? 0,
                        PlayerCount = totalPlayers ?? 0,
                        TribeCount = totalTribes ?? 0,
                        LastUpdate = lastUpdateString,
                        NextUpdate = nextUpdateString,
                        ServerStarted = serverStarted
                    };

                    if (HasFeatureAccess("home", "online"))
                    {
                        var onlineplayers = playerinfos?.Where(x => !string.IsNullOrEmpty(x.Name)).ToArray() ?? new PlayerInfo[] { };
                        using (var db = _databaseContextFactory.Create())
                        {
                            // Names of online players (steam does not provide ids)
                            var onlineplayerNames = onlineplayers.Select(x => x.Name).ToArray();

                            // Get the player data for each name (null when no matching name or when multiple players share the same name)
                            var onlineplayerData = context.Players != null ? (from k in onlineplayerNames join p in context.Players on k equals p.Name into grp select grp.Count() == 1 ? grp.ElementAt(0) : null).ToArray() : new ArkPlayer[] { };

                            // Parse all steam ids
                            var parsedSteamIds = onlineplayerData.Select(x => { long steamId; return x?.SteamId != null ? long.TryParse(x.SteamId, out steamId) ? steamId : (long?)null : null; }).ToArray();

                            // Get the player data for each name
                            var databaseUsers = (from k in parsedSteamIds join u in db.Users on k equals u?.SteamId into grp select grp.FirstOrDefault()).ToArray();

                            // Get the discord users
                            var discordUsers = databaseUsers.Select(x => x != null ? _discordManager.GetDiscordUserNameById((ulong)x.DiscordId) : null).ToArray();

                            int n = 0;
                            foreach (var player in onlineplayers)
                            {
                                var extra = onlineplayerData.Length > n ? new { player = onlineplayerData[n], user = databaseUsers[n], discordName = discordUsers[n] } : null;
                                var demoPlayerName = demoMode?.GetPlayerName();

                                sr.OnlinePlayers.Add(new OnlinePlayerViewModel
                                {
                                    SteamName = demoPlayerName ?? player.Name,
                                    CharacterName = demoPlayerName ?? extra?.player?.CharacterName,
                                    TribeName = demoMode?.GetTribeName() ?? extra?.player?.Tribe?.Name,
                                    DiscordName = demoMode != null ? null : extra?.discordName,
                                    TimeOnline = player.Time.ToStringCustom(),
                                    TimeOnlineSeconds = (int)Math.Round(player.Time.TotalSeconds)
                                });

                                n++;
                            }
                        }
                    }
                    else
                    {
                        sr.OnlinePlayers = null;
                    }

                    result.Servers.Add(sr);
                }
            }

            foreach (var context in _contextManager.Clusters)
            {
                var cc = new ClusterStatusViewModel
                {
                    Key = context.Config.Key,
                    ServerKeys = _contextManager.Servers
                        .Where(x => x.Config.Cluster.Equals(context.Config.Key, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.Config.Key).ToArray()
                };
                result.Clusters.Add(cc);
            }

            return result;
        }

        private static AccessControlViewModel BuildViewModelForAccessControl(IConfig config)
        {
            var ac = new AccessControlViewModel { };
            if (config.AccessControl != null)
            {
                foreach (var fg in config.AccessControl)
                {
                    var acfg = new Dictionary<string, string[]>();
                    ac[fg.Key] = acfg;

                    if (fg.Value == null) continue;

                    foreach (var rf in fg.Value)
                    {
                        acfg[rf.Key] = rf.Value;
                    }
                }
            }

            return ac;
        }
    }
}