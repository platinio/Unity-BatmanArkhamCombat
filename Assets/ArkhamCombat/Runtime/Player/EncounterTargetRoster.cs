using System.Collections.Generic;
using ArkhamCombat.Combat;
using ArkhamCombat.Encounter;
using UnityEngine;

namespace ArkhamCombat.Player
{
    public sealed class EncounterTargetRoster : ITargetRoster
    {
        private readonly List<ICombatTarget> targets = new List<ICombatTarget>();
        private readonly Dictionary<IEncounterAgent, ICombatTarget> targetByAgent = new Dictionary<IEncounterAgent, ICombatTarget>();

        public EncounterTargetRoster(EncounterDirector director)
        {
            foreach (IEncounterAgent agentAlreadyInTheEncounter in director.Agents)
            {
                AddTheTargetOf(agentAlreadyInTheEncounter);
            }

            director.AgentRegistered += AddTheTargetOf;
            director.AgentUnregistered += RemoveTheTargetOf;
        }

        public IReadOnlyList<ICombatTarget> Targets => targets;

        private void AddTheTargetOf(IEncounterAgent agent)
        {
            if (TryGetTheTargetOn(agent, out ICombatTarget target))
            {
                targets.Add(target);
                targetByAgent.Add(agent, target);
            }
        }

        private static bool TryGetTheTargetOn(IEncounterAgent agent, out ICombatTarget target)
        {
            target = null;
            return agent is Component agentComponent && agentComponent.TryGetComponent(out target);
        }

        private void RemoveTheTargetOf(IEncounterAgent agent)
        {
            if (targetByAgent.TryGetValue(agent, out ICombatTarget target))
            {
                targets.Remove(target);
                targetByAgent.Remove(agent);
            }
        }
    }
}
