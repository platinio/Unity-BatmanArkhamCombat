using ArcaneOnyx.TPCharacterController;
using ArcaneOnyx.TPCharacterController.States;
using ArkhamCombat.Combat;
using UnityEngine;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Entering a reaction state is all it ever does to the state machine; each state plays its
    /// own reaction and leaves on its own.
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

        public override void Tick(float deltaTime) => EnterTheStateForThePendingReaction(status, characterBrain.StateMachine);

        internal static void EnterTheStateForThePendingReaction(PlayerStatus status, CharacterStateMachine stateMachine)
        {
            if (status.TryTakePendingReaction(out AppliedReaction reaction))
            {
                EnterTheStateFor(reaction, stateMachine);
            }
        }

        internal static void EnterTheStateFor(AppliedReaction reaction, CharacterStateMachine stateMachine)
        {
            if (reaction == AppliedReaction.Death)
            {
                stateMachine.Change<DeadState>();
                return;
            }

            if (!stateMachine.Current.CanBeInterrupted)
            {
                return;
            }

            switch (reaction)
            {
                case AppliedReaction.None:
                    return;
                case AppliedReaction.Knockdown:
                    stateMachine.Change<KnockedDownState>();
                    return;
                case AppliedReaction.Stagger:
                    stateMachine.Change<StaggerState>();
                    return;
                case AppliedReaction.Flinch:
                    stateMachine.Change<FlinchState>();
                    return;
                default:
                    Debug.LogError($"[{nameof(HitReactionSwitch)}] No state plays the reaction '{reaction}', so the hit shows nothing.");
                    return;
            }
        }
    }
}
