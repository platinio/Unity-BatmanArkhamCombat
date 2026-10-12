using System.Collections.Generic;
using UnityEngine;

namespace ArkhamCombat.Combat
{
    /// <summary>
    /// A move that carries its character a set distance in a direction, where an attack carries it
    /// to a target: an evade, a dodge.
    /// </summary>
    [CreateAssetMenu(menuName = "ArkhamCombat/Dash", fileName = "Dash")]
    public sealed class DashDefinition : ActionDefinition
    {
        [Tooltip("The character is moved over this span (normalized time).")]
        [SerializeField] private Window travelWindow = new Window(0f, 0.6f);

        [Tooltip("Metres it travels, in the direction it was started with.")]
        [SerializeField, Min(0f)] private float travelDistance = 2.5f;

        public Window TravelWindow => travelWindow;
        public float TravelDistance => travelDistance;

        public void ConfigureDash(Window travelWindow, float travelDistance)
        {
            this.travelWindow = travelWindow;
            this.travelDistance = travelDistance;
        }

        internal override void GiveItsWindowsTo(ActionRunner runner) => runner.TakeTheWindowsOf(this);

        public override bool Validate(List<string> errors)
        {
            int errorCountBefore = errors.Count;

            base.Validate(errors);
            ReportWhenWindowEndsBeforeItStarts(nameof(travelWindow), travelWindow, errors);

            bool isWellFormed = errors.Count == errorCountBefore;
            return isWellFormed;
        }
    }
}
