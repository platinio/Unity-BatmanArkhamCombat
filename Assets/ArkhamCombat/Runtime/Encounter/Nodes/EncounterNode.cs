using ArcaneOnyx.BehaviorTree;

namespace ArkhamCombat.Encounter
{
    public abstract class EncounterNode : GameplayNode
    {
        private EncounterAgent encounterAgent;

        // Looked up when first asked for: a value port can be read before the tree has woken its nodes.
        protected EncounterAgent EncounterAgent
        {
            get
            {
                if (encounterAgent == null)
                {
                    encounterAgent = GetComponent<EncounterAgent>();
                }

                return encounterAgent;
            }
        }

        protected bool IsInAnEncounter => EncounterAgent != null;
    }
}
