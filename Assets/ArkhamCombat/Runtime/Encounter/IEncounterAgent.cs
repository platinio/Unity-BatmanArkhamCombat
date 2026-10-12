using UnityEngine;

namespace ArkhamCombat.Encounter
{
    public interface IEncounterAgent
    {
        Vector3 Position { get; }

        /// <summary>The object the agent lives on. An agent that is a plain object has none.</summary>
        GameObject Owner => null;
    }
}
