using UnityEngine;

namespace SpaceXonix.Campaign
{
    /// <summary>
    /// Every stage modifier the campaign can draw from. In its own file because Unity only creates a
    /// MonoScript for the type matching the file name; a ScriptableObject declared beside another one
    /// gets saved with no script reference and silently loads as null after a domain reload.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceXonix/Stage Modifier Set")]
    public sealed class StageModifierSetDefinition : ScriptableObject
    {
        public StageModifierDefinition[] modifiers;
    }
}
