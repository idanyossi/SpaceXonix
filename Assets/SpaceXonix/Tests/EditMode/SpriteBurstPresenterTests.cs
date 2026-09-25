using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Enemies;
using SpaceXonix.Pooling;
using SpaceXonix.Presentation;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class SpriteBurstPresenterTests
    {
        [Test]
        public void ShotAlien_ExplodesWhereItWasOnceAndReturnsToThePool()
        {
            var root = new GameObject("Presenter");
            var prefab = new GameObject("ExplosionPrefab");
            var alien = new GameObject("Alien");
            try
            {
                var burst = prefab.AddComponent<SpriteBurst>();
                var frames = new Sprite[5];
                for (var i = 0; i < frames.Length; i++) frames[i] = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
                Set(burst, "frames", frames);
                prefab.SetActive(false);
                var presenter = root.AddComponent<SpriteBurstPresenter>();
                Set(presenter, "poolService", root.AddComponent<PoolService>());
                Set(presenter, "explosionPrefab", prefab);

                alien.transform.position = new Vector3(3f, 4f, -.5f);
                var enemy = alien.AddComponent<BasicBouncer>();
                typeof(SpriteBurstPresenter).GetMethod("OnEnemyDestroyed", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(presenter, new object[] { enemy });

                Assert.That(presenter.Active, Has.Count.EqualTo(1), "the kill explodes");
                Assert.That(presenter.Active[0].transform.position, Is.EqualTo(alien.transform.position));
                Assert.That(presenter.Active[0].IsPlaying, Is.True);

                presenter.Tick(presenter.Active[0].Duration + .01f);
                Assert.That(presenter.Active, Is.Empty, "it plays once");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(alien);
            }
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
