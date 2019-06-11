﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleTech;
using AIToolkit.Features;
using AIToolkit.Features.Overrides;
using AIToolkit.Resources;
using AIToolkit.Selectors;
using AIToolkit.Selectors.Team;
using AIToolkit.Selectors.Unit;
using AIToolkit.Util;
using Harmony;
using HBS.Logging;

// ReSharper disable UnusedMember.Global

namespace AIToolkit
{
    public static class Main
    {
        internal static ILog HBSLog;
        internal static ModSettings Settings;
        internal static string Directory;

        private static readonly List<string> UnitAIOverridePaths = new List<string>();
        private static readonly List<string> TeamAIOverridePaths = new List<string>();
        private static readonly List<string> BehaviorNodePaths = new List<string>();

        private static readonly List<UnitAIOverrideDef> UnitAIOverrides
            = new List<UnitAIOverrideDef>();
        private static readonly List<TeamAIOverrideDef> TeamAIOverrides
            = new List<TeamAIOverrideDef>();
        internal static readonly List<BehaviorNodeDef> BehaviorNodeDefs
            = new List<BehaviorNodeDef>();

        internal static readonly Dictionary<AbstractActor, UnitAIOverrideDef> UnitToAIOverride
            = new Dictionary<AbstractActor, UnitAIOverrideDef>();
        internal static readonly Dictionary<AITeam, TeamAIOverrideDef> TeamToAIOverride
            = new Dictionary<AITeam, TeamAIOverrideDef>();

        // todo: make selectors use reflection maybe to find the type
        private static readonly Dictionary<string, ISelector<AITeam>> TeamSelectors
            = new Dictionary<string, ISelector<AITeam>>
            {
                { "TeamName", new TeamName() },
                { "IsInterleaved", new IsInterleaved() },
                { "Custom", new Custom<AITeam>() }
            };

        // todo: make selectors use reflection maybe to find the type
        private static readonly Dictionary<string, ISelector<AbstractActor>> UnitSelectors
            = new Dictionary<string, ISelector<AbstractActor>>
            {
                { "TeamName", new UnitTeamName() },
                { "Role", new Role() },
                { "Tree", new TreeID() },
                { "Custom", new Custom<AbstractActor>() }
            };


        public static void Init(string modDir, string settings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.AIToolkit");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            HBSLog = Logger.GetLogger("AIToolkit");

            Settings = ModSettings.Parse(settings);
            Directory = modDir;

            if (Settings.ShouldDump)
                BehaviorTreeDump.DumpTrees(Settings.DumpType);
        }

        public static void FinishedLoading(Dictionary<string, Dictionary<string, VersionManifestEntry>> customResources)
        {
            if (customResources.ContainsKey(nameof(UnitAIOverrideDef)))
            {
                UnitAIOverridePaths.AddRange(customResources[nameof(UnitAIOverrideDef)]
                    .Values.Select(entry => entry.FilePath));
            }

            if (customResources.ContainsKey(nameof(TeamAIOverrideDef)))
            {
                TeamAIOverridePaths.AddRange(customResources[nameof(TeamAIOverrideDef)]
                    .Values.Select(entry => entry.FilePath));
            }

            if (customResources.ContainsKey(nameof(BehaviorNodeDef)))
            {
                BehaviorNodePaths.AddRange(customResources[nameof(BehaviorNodeDef)]
                    .Values.Select(entry => entry.FilePath));
            }
        }


        internal static void OnCombatInit()
        {
            ReloadResources();
            AIPause.DestroyUI();
        }

        internal static void ReloadResources()
        {
            UnitToAIOverride.Clear();
            UnitAIOverrides.Clear();
            foreach (var path in UnitAIOverridePaths)
            {
                var unitOverride = SerializeUtil.FromPath<UnitAIOverrideDef>(path);
                if (unitOverride == null)
                {
                    HBSLog?.LogError($"UnitAIOverrideDef Resource did not parse at {path}");
                    break;
                }

                HBSLog?.Log($"Parsed UnitAIOverrideDef {unitOverride.Name} at {path}");
                UnitAIOverrides.Add(unitOverride);
            }

            TeamToAIOverride.Clear();
            TeamAIOverrides.Clear();
            foreach (var path in TeamAIOverridePaths)
            {
                var teamOverride = SerializeUtil.FromPath<TeamAIOverrideDef>(path);
                if (teamOverride == null)
                {
                    HBSLog?.LogError($"TeamAIOverrideDef Resource did not parse at {path}");
                    break;
                }

                HBSLog?.Log($"Parsed TeamAIOverride {teamOverride.Name} at {path}");
                TeamAIOverrides.Add(teamOverride);
            }

            BehaviorNodeDefs.Clear();
            foreach (var path in BehaviorNodePaths)
            {
                var behaviorNodeDef = SerializeUtil.FromPath<BehaviorNodeDef>(path);
                if (behaviorNodeDef == null)
                {
                    HBSLog?.LogError($"BehaviorNodeDef Resource did not parse at {path}");
                    break;
                }

                HBSLog?.Log($"Parsed BehaviorNodeDef with root name {behaviorNodeDef.Name} at {path}");
                BehaviorNodeDefs.Add(behaviorNodeDef);
            }
        }


        internal static UnitAIOverrideDef TryOverrideUnitAI(AbstractActor unit)
        {
            var aiOverride = UnitAIOverrideDef.SelectOverride(unit,
                UnitAIOverrides.Cast<AIOverrideDef<AbstractActor>>(), UnitSelectors) as UnitAIOverrideDef;

            if (aiOverride == null)
                return null;

            // unit has already been overriden and has the same override then we just got
            if (UnitToAIOverride.ContainsKey(unit) && UnitToAIOverride[unit] == aiOverride)
                return UnitToAIOverride[unit];

            // unit has already been overridden but has a different override
            if (UnitToAIOverride.ContainsKey(unit) && UnitToAIOverride[unit] != aiOverride)
                ResetUnitAI(unit);

            HBSLog?.Log($"Overriding AI on unit {unit.UnitName} with {aiOverride.Name}");
            UnitToAIOverride[unit] = aiOverride;

            BehaviorTreeOverride.TryOverrideTree(unit.BehaviorTree, UnitToAIOverride[unit]);
            InfluenceFactorOverride.TryOverrideInfluenceFactors(unit.BehaviorTree, UnitToAIOverride[unit]);

            return UnitToAIOverride[unit];
        }

        internal static void ResetUnitAI(AbstractActor unit)
        {
            HBSLog?.Log($"Resetting AI for unit {unit.UnitName}");

            Traverse.Create(unit.BehaviorTree).Method("InitRootNode").GetValue();
            unit.BehaviorTree.influenceMapEvaluator = new InfluenceMapEvaluator();
            unit.BehaviorTree.Reset();
        }


        internal static TeamAIOverrideDef TryOverrideTeamAI(AITeam team)
        {
            var aiOverride = TeamAIOverrideDef.SelectOverride(team,
                TeamAIOverrides.Cast<AIOverrideDef<AITeam>>(), TeamSelectors) as TeamAIOverrideDef;

            if (aiOverride == null)
                return null;

            // team already overriden and has same override that we just got
            if (TeamToAIOverride.ContainsKey(team) && TeamToAIOverride[team] == aiOverride)
                return TeamToAIOverride[team];

            // unit has been already overriden but has a different override
            if (TeamToAIOverride.ContainsKey(team) && TeamToAIOverride[team] != aiOverride)
                ResetTeamAI(team);

            HBSLog?.Log($"Overriding AI on team {team.Name} with {aiOverride.Name}");
            TeamToAIOverride[team] = aiOverride;
            return TeamToAIOverride[team];
        }

        internal static void ResetTeamAI(AITeam team)
        {
            HBSLog?.Log($"Resetting AI for team {team.Name}");
        }
    }
}