using ArkhamCombat.Combat;
using UnityEngine;
using Zenject;

namespace ArkhamCombat.Player
{
    /// <summary>
    /// Plays this character's actions: each tick it runs the character's <see cref="ActionRunner"/>,
    /// which starts an action from a press only while the character's start gate allows it. The
    /// runner lives in the character's own context, which the scene cannot see into, so this is also
    /// where a state or the overlay finds the runner and the frame's warp movement.
    /// </summary>
    public sealed class CombatActions : CombatComponent
    {
        private ActionRunner runner;
        private MotorWarpMover warpMover;
        private IActionStartGate startGate;

        public ActionRunner Runner => runner;

        [Inject]
        internal void Construct(ActionRunner runner, MotorWarpMover warpMover, IActionStartGate startGate)
        {
            this.runner = runner;
            this.warpMover = warpMover;
            this.startGate = startGate;
        }

        private void Awake()
        {
            if (runner == null)
            {
                Debug.LogError(
                    $"[{nameof(CombatActions)}] Nothing was injected on '{name}'. The character needs a " +
                    $"GameObjectContext with a {nameof(CombatCharacterInstaller)}, and the scene a SceneContext.", this);
            }
        }

        public override void Tick(float deltaTime) =>
            runner.Tick(deltaTime, transform.position, startGate.CanStartFromIdle);

        /// <summary>How far the runner's warp asked the character to move since this was last taken.</summary>
        public Vector3 TakePendingWarpMovement() => warpMover.TakePendingMovement();
    }
}
