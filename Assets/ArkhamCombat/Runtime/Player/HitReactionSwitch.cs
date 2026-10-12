using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Entering a reaction state is all it ever does to the state machine; each state plays its
    /// own reaction and leaves on its own. Listed after the other switches, so a hit wins over an
    /// attack that started on the same frame.
    /// </summary>
    [RequireComponent(typeof(CharacterBrain), typeof(PlayerStatus))]
    public sealed class HitReactionSwitch : CharacterComponent
    {
        private CharacterBrain characterBrain;
        private PlayerStatus status;

        private void Awake()
        {
            characterBrain = GetComponent<CharacterBrain>();
            status = GetComponent<PlayerStatus>();
        }

        public override void Tick(float deltaTime)
        {
            if (status.TryTakePendingReaction(out AppliedReaction reaction))
            {
                EnterTheStateFor(reaction, characterBrain.StateMachine);
            }
        }

        internal static void EnterTheStateFor(AppliedReaction reaction, CharacterStateMachine stateMachine)
        {
            if (reaction == AppliedReaction.Death)
            {
                stateMachine.Change<DeadState>();
                return;
            }

            bool isOnTheFloor = stateMachine.Current is KnockedDownState;
            if (isOnTheFloor)
            {
                return;
            }

            switch (reaction)
            {
                case AppliedReaction.Knockdown:
                    stateMachine.Change<KnockedDownState>();
                    return;
                case AppliedReaction.Stagger:
                    stateMachine.Change<StaggerState>();
                    return;
                case AppliedReaction.Flinch:
                    stateMachine.Change<FlinchState>();
                    return;
            }
        }
    }
}
