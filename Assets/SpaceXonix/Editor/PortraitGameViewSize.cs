using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SpaceXonix.EditorTools
{
    /// <summary>
    /// Adds a 1080x1920 entry to the Game view's resolution list, because the game is designed for
    /// portrait and Free Aspect in a landscape editor window frames it completely differently.
    /// Uses Unity's internal GameViewSizes, so it fails quietly rather than breaking the editor if
    /// that API moves in a future version.
    /// </summary>
    public static class PortraitGameViewSize
    {
        private const string SizeName = "SpaceXonix Portrait 1080x1920";

        [MenuItem("SpaceXonix/Add Portrait Game View Size")]
        public static void Add()
        {
            try
            {
                var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
                var singletonType = typeof(Editor).Assembly.GetType("UnityEditor.ScriptableSingleton`1").MakeGenericType(sizesType);
                var instance = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { CurrentGroupType() });

                var displayTexts = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
                foreach (var text in displayTexts)
                {
                    if (!text.Contains(SizeName)) continue;
                    Debug.Log($"'{SizeName}' is already in the Game view list.");
                    return;
                }

                var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
                var sizeTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                var constructor = sizeType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                // 1 == FixedResolution
                var size = constructor.Invoke(new[] { Enum.ToObject(sizeTypeEnum, 1), 1080, 1920, (object)SizeName });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                Debug.Log($"Added '{SizeName}' to the Game view resolution list.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not add the portrait Game view size automatically: {exception.Message}. " +
                                 "Add 1080x1920 by hand from the Game view's resolution dropdown.");
            }
        }

        private static object CurrentGroupType()
        {
            var groupTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var target = EditorUserBuildSettings.activeBuildTarget;
            var name = target == BuildTarget.Android ? "Android"
                : target == BuildTarget.iOS ? "iOS"
                : "Standalone";
            return Enum.Parse(groupTypeEnum, name);
        }
    }
}
