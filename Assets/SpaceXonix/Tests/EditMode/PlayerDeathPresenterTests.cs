using System;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using SpaceXonix.Presentation;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class PlayerDeathPresenterTests
    {
        [Test]
        public void Death_BurstsAndHidesTheShipUntilTheRespawnCompletes()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Presenter.ShipHidden, Is.False);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);

                Assert.That(fixture.Presenter.ShipHidden, Is.True, "the ship disappears on the hit");
                Assert.That(fixture.PlayerVisual.Visual.gameObject.activeSelf, Is.False);
                Assert.That(fixture.PlayerVisual.Shadow.gameObject.activeSelf, Is.False);
                Assert.That(fixture.Rings.ActiveRings.Count, Is.EqualTo(1), "a burst marks the death spot");

                fixture.CompleteRespawn();
                Assert.That(fixture.Presenter.ShipHidden, Is.False, "the ship returns with the respawn");
                Assert.That(fixture.PlayerVisual.Visual.gameObject.activeSelf, Is.True);
                Assert.That(fixture.PlayerVisual.Shadow.gameObject.activeSelf, Is.True);
            }
        }

        [Test]
        public void RespawnDelay_IsShortEnoughToReadAsDeathRatherThanAFreeze()
        {
            var prefabScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/SpaceXonix/Scenes/Game.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                GameManager game = null;
                foreach (var root in prefabScene.GetRootGameObjects())
                {
                    game = root.GetComponentInChildren<GameManager>(true);
                    if (game != null) break;
                }
                Assert.That(game, Is.Not.Null);
                var delay = (float)typeof(GameManager).GetField("respawnDelay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
                Assert.That(delay, Is.LessThanOrEqualTo(.8f), "a long dead pause reads as lag");
                Assert.That(delay, Is.GreaterThan(.2f), "some pause is needed to register the death");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(prefabScene, true);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("DeathPresenterFixture");
            private readonly GameObject playerObject = new GameObject("Player");
            private readonly GameObject ringPrefab;
            public readonly GameManager Game;
            public readonly ActorVisual PlayerVisual;
            public readonly ExplosionRingPresenter Rings;
            public readonly PlayerDeathPresenter Presenter;

            public Fixture()
            {
                root.SetActive(false);
                var board = root.AddComponent<BoardManager>();
                board.Initialize();
                var input = root.AddComponent<InputRouter>();
                var player = playerObject.AddComponent<PlayerController>();
                Invoke(player, "Awake");
                PlayerVisual = playerObject.AddComponent<ActorVisual>();
                var visual = new GameObject("Visual"); visual.transform.SetParent(playerObject.transform, false);
                var shadow = new GameObject("Shadow"); shadow.transform.SetParent(playerObject.transform, false);
                Set(PlayerVisual, "visual", visual.transform);
                Set(PlayerVisual, "shadow", shadow.transform);
                Set(PlayerVisual, "boardManager", board);

                Game = root.AddComponent<GameManager>();
                Set(Game, "inputRouter", input); Set(Game, "playerController", player); Set(Game, "boardManager", board);

                var pool = root.AddComponent<PoolService>();
                ringPrefab = new GameObject("RingPrefab");
                ringPrefab.AddComponent<MeshRenderer>();
                ringPrefab.AddComponent<ExplosionRing>();
                ringPrefab.SetActive(false);
                Rings = root.AddComponent<ExplosionRingPresenter>();
                Set(Rings, "poolService", pool);
                Set(Rings, "ringPrefab", ringPrefab);

                Presenter = root.AddComponent<PlayerDeathPresenter>();
                Set(Presenter, "gameManager", Game);
                Set(Presenter, "playerVisual", PlayerVisual);
                Set(Presenter, "ringPresenter", Rings);

                Invoke(Game, "Awake");
                root.SetActive(true);
                Invoke(Game, "Start");
                Invoke(Presenter, "OnEnable");
            }

            public void CompleteRespawn()
            {
                Invoke(Game, "CompleteRespawn");
                Set(Game, "failureGateReleaseFrame", Time.frameCount - 1);
                Invoke(Game, "LateUpdate");
                Game.AdvanceLifecycle(5f);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(ringPrefab);
            }

            private static void Set(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

            private static void Invoke(object target, string name) =>
                target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        }
    }
}
