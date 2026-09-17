using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Pooling;
using SpaceXonix.PowerUps;
using UnityEngine;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class PowerUpTests
    {
        [TestCase(4.99f, 0f)]
        [TestCase(5f, .25f)]
        [TestCase(10f, .35f)]
        [TestCase(22.5f, .6f)]
        [TestCase(40f, .6f)]
        public void SpawnChance_FollowsGddFormulaWithMinimumAndCap(float percentage, float expected)
        {
            Assert.That(PowerUpSpawnRules.GetSpawnChance(percentage, 5f, .15f, .02f, .6f), Is.EqualTo(expected).Within(.0001f));
        }

        [Test]
        public void Slot_StoresImmediatelyWhenEmpty()
        {
            var slot = new PowerUpSlotModel();
            Assert.That(slot.Collect(PowerUpType.Freeze), Is.EqualTo(PickupCollectResult.Stored));
            Assert.That(slot.Stored, Is.EqualTo(PowerUpType.Freeze));
        }

        [Test]
        public void Slot_OccupiedSlotIsReplacedImmediately()
        {
            var slot = new PowerUpSlotModel();
            slot.Collect(PowerUpType.Shield);
            Assert.That(slot.Collect(PowerUpType.Freeze), Is.EqualTo(PickupCollectResult.Replaced));
            Assert.That(slot.Stored, Is.EqualTo(PowerUpType.Freeze));
            Assert.That(slot.TryConsume(out var type), Is.True);
            Assert.That(type, Is.EqualTo(PowerUpType.Freeze));
            Assert.That(slot.HasStored, Is.False);
            Assert.That(slot.TryConsume(out _), Is.False);
        }

        [Test]
        public void EnemyDrift_PushesTowardSideAndStopsAtWall()
        {
            var model = new EnemyMovementModel(Vector2.zero, Vector2.up) { Drift = new Vector2(2f, 0f) };
            model.Advance(.25f, p => p.x < 1f);
            Assert.That(model.Position.x, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(model.Position.y, Is.EqualTo(.25f).Within(.0001f));
            model.Advance(.5f, p => p.x < 1f);
            Assert.That(model.Position.x, Is.LessThan(1f));
            model.MovementEnabled = false;
            Assert.That(model.EffectiveVelocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SmallCapture_NeverSpawnsPickup()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Manager.TrySpawnFromCapture(4.9f), Is.False);
                Assert.That(fixture.Manager.ActivePickup, Is.Null);
            }
        }

        [Test]
        public void QualifyingCapture_SpawnsOnePickupOnUncapturedCellAwayFromEnemies()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.SpawnEnemy(new GridCoordinate(20, 40));
                for (var i = 0; i < 25; i++)
                {
                    Assert.That(fixture.Manager.TrySpawnFromCapture(10f), Is.True);
                    var pickup = fixture.Manager.ActivePickup;
                    Assert.That(fixture.Board.Model.GetCell(pickup.Cell), Is.EqualTo(BoardCellState.Uncaptured));
                    Assert.That(Mathf.Abs(pickup.Cell.X - enemy.LogicalCell.X) + Mathf.Abs(pickup.Cell.Y - enemy.LogicalCell.Y), Is.GreaterThanOrEqualTo(3));
                    Assert.That(fixture.Manager.TrySpawnFromCapture(10f), Is.False, "only one pickup may be on the board");
                    Assert.That(fixture.Manager.CollectActivePickup(), Is.True);
                }
            }
        }

        [Test]
        public void PickupExpiresAfterLifetime()
        {
            using (var fixture = new Fixture())
            {
                fixture.SpawnDefinition.pickupLifetime = 1f;
                fixture.Manager.TrySpawnFromCapture(10f);
                var pickup = fixture.Manager.ActivePickup;
                fixture.Manager.Tick(.6f);
                Assert.That(fixture.Manager.ActivePickup, Is.SameAs(pickup));
                fixture.Manager.Tick(.6f);
                Assert.That(fixture.Manager.ActivePickup, Is.Null);
                Assert.That(pickup.gameObject.activeSelf, Is.False);
            }
        }

        [Test]
        public void PlayerReachingPickupCell_StoresItImmediatelyWhenSlotEmpty()
        {
            using (var fixture = new Fixture())
            {
                fixture.SpawnPickupAtPlayer(PowerUpType.Freeze);
                fixture.Manager.Tick(.01f);
                Assert.That(fixture.Manager.StoredPowerUp, Is.EqualTo(PowerUpType.Freeze));
                Assert.That(fixture.Manager.ActivePickup, Is.Null);
                Assert.That(fixture.Game.IsPaused, Is.False);
            }
        }

        [Test]
        public void TouchingPickupWithStoredAbility_ReplacesItInstantlyWithoutPausing()
        {
            using (var fixture = new Fixture())
            {
                fixture.SpawnPickupAtPlayer(PowerUpType.Shield);
                fixture.Manager.Tick(.01f);
                fixture.SpawnPickupAtPlayer(PowerUpType.ArenaTilt);
                var changes = new List<PowerUpType?>();
                fixture.Manager.StoredChanged += stored => changes.Add(stored);
                fixture.Manager.Tick(.01f);

                Assert.That(fixture.Manager.StoredPowerUp, Is.EqualTo(PowerUpType.ArenaTilt));
                Assert.That(changes, Is.EqualTo(new PowerUpType?[] { PowerUpType.ArenaTilt }));
                Assert.That(fixture.Manager.ActivePickup, Is.Null);
                Assert.That(fixture.Game.IsPaused, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.True);
                Assert.That(fixture.Manager.TryUseStoredAbility(), Is.True);
                Assert.That(fixture.Manager.IsEffectActive(PowerUpType.ArenaTilt), Is.True);
            }
        }

        [Test]
        public void GamePause_FreezesTimeRejectsDamageAndResumesState()
        {
            using (var fixture = new Fixture())
            {
                fixture.Game.SetPaused(true);
                Assert.That(Time.timeScale, Is.EqualTo(0f));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.False);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.TrailHit), Is.False);
                Assert.That(fixture.Manager.TryUseStoredAbility(), Is.False);
                fixture.Game.SetPaused(false);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(fixture.Input.GameplayInputEnabled, Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.Playing));
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));
            }
        }

        [Test]
        public void UsingWithEmptySlot_DoesNothing()
        {
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Input.RequestAbility(), Is.True);
                Assert.That(fixture.Manager.IsEffectActive(PowerUpType.Shield), Is.False);
                Assert.That(fixture.Manager.TryUseStoredAbility(), Is.False);
            }
        }

        [Test]
        public void Shield_BlocksEnemyAndLaserButNotTrailFailures_ThenExpires()
        {
            using (var fixture = new Fixture())
            {
                fixture.Store(PowerUpType.Shield);
                Assert.That(fixture.Input.RequestAbility(), Is.True);
                Assert.That(fixture.Manager.StoredPowerUp, Is.Null);
                Assert.That(fixture.Manager.IsEffectActive(PowerUpType.Shield), Is.True);
                Assert.That(fixture.Manager.GetEffectRemaining(PowerUpType.Shield), Is.EqualTo(4f).Within(.05f));
                Assert.That(fixture.ShieldVisual.activeSelf, Is.True);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.False);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser), Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3));

                fixture.Manager.ExpireEffect(PowerUpType.Shield);
                Assert.That(fixture.Game.IsShieldActive, Is.False);
                Assert.That(fixture.ShieldVisual.activeSelf, Is.False);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.EnemyContact), Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
            }
            using (var fixture = new Fixture())
            {
                fixture.Store(PowerUpType.Shield);
                fixture.Manager.TryUseStoredAbility();
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.TrailHit), Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
            }
        }

        [Test]
        public void ShieldedExposedShip_EnemyReachingShipCell_DoesNotKill()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.PrepareExposedShipFacingEnemy(shielded: true);
                PlayerFailureReason? reason = null;
                fixture.Game.PlayerFailed += r => reason = r;
                var reachedShipCell = false;
                for (var i = 0; i < 120; i++)
                {
                    enemy.AdvanceMovement(.02f);
                    foreach (var cell in enemy.LastTraversedCells) if (cell == fixture.Board.PlayerCell) reachedShipCell = true;
                }
                Assert.That(reachedShipCell, Is.True);
                Assert.That(fixture.Game.Lives, Is.EqualTo(3), $"died with shield: {reason}");
                Assert.That(fixture.Board.IsPlayerExposed, Is.True);
            }
        }

        [Test]
        public void UnshieldedExposedShip_EnemyReachingShip_StillKills()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.PrepareExposedShipFacingEnemy(shielded: false);
                for (var i = 0; i < 120 && fixture.Game.Lives == 3; i++) enemy.AdvanceMovement(.02f);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
            }
        }

        [Test]
        public void ShieldPassThrough_AlienThatTouchedShipCannotHitTrailUntilItLeavesTheTrail()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.PrepareExposedShipFacingEnemy(shielded: true);
                PlayerFailureReason? reason = null;
                fixture.Game.PlayerFailed += r => reason = r;
                var trailCell = fixture.Board.Model.ToCoordinate(fixture.Board.Model.ActiveTrail[0]);

                fixture.Teleport(enemy, fixture.Player.transform.position, Vector2.zero);
                enemy.AdvanceMovement(.02f);
                Assert.That(enemy.HasShieldPassThroughGrace, Is.True);

                fixture.Teleport(enemy, fixture.Board.GetWorldPosition(trailCell), Vector2.down);
                enemy.AdvanceMovement(.02f);
                Assert.That(reason, Is.Null, "the alien the ship passed through lands on the trail behind it");
                Assert.That(enemy.HasShieldPassThroughGrace, Is.True, "grace lasts while its body is still on the trail");

                fixture.Manager.ExpireEffect(PowerUpType.Shield);
                enemy.AdvanceMovement(.02f);
                Assert.That(reason, Is.Null, "grace outlasts the shield while the alien is still crossing the trail");

                fixture.Teleport(enemy, fixture.Board.GetWorldPosition(new GridCoordinate(30, 60)), Vector2.down);
                enemy.AdvanceMovement(.02f);
                Assert.That(enemy.HasShieldPassThroughGrace, Is.False, "grace ends once the body has left the trail");

                fixture.Teleport(enemy, fixture.Board.GetWorldPosition(trailCell), Vector2.down);
                enemy.AdvanceMovement(.02f);
                Assert.That(reason, Is.EqualTo(PlayerFailureReason.TrailHit));
            }
        }

        [Test]
        public void ShieldPassThrough_IsNotGrantedWithoutShieldAndIsClearedOnPoolReuse()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.PrepareExposedShipFacingEnemy(shielded: true);
                fixture.Teleport(enemy, fixture.Player.transform.position, Vector2.zero);
                enemy.AdvanceMovement(.02f);
                Assert.That(enemy.HasShieldPassThroughGrace, Is.True);
                enemy.Deactivate();
                Assert.That(enemy.HasShieldPassThroughGrace, Is.False);
            }
            using (var fixture = new Fixture())
            {
                var enemy = fixture.PrepareExposedShipFacingEnemy(shielded: false);
                fixture.Teleport(enemy, fixture.Player.transform.position, Vector2.zero);
                enemy.AdvanceMovement(.02f);
                Assert.That(enemy.HasShieldPassThroughGrace, Is.False);
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
            }
        }

        [Test]
        public void Shield_DoesNotProtectTrailBehindShip()
        {
            using (var fixture = new Fixture())
            {
                fixture.PrepareExposedShipFacingEnemy(shielded: true);
                var trailCell = fixture.Board.Model.ToCoordinate(fixture.Board.Model.ActiveTrail[0]);
                Assert.That(trailCell, Is.Not.EqualTo(fixture.Board.PlayerCell));
                var onTrail = fixture.SpawnEnemyAt(fixture.Board.GetWorldPosition(trailCell) + Vector3.up * fixture.Board.CellWorldSize, Vector2.down);
                PlayerFailureReason? reason = null;
                fixture.Game.PlayerFailed += r => reason = r;
                for (var i = 0; i < 60 && reason == null; i++) onTrail.AdvanceMovement(.02f);
                Assert.That(reason, Is.EqualTo(PlayerFailureReason.TrailHit));
                Assert.That(fixture.Game.Lives, Is.EqualTo(2));
            }
        }

        [Test]
        public void Freeze_StopsAllEnemiesAndTintsThemUntilExpiry()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.SpawnEnemy(new GridCoordinate(20, 40));
                var enemyRenderer = enemy.GetComponent<Renderer>();
                var original = enemyRenderer.sharedMaterial;
                fixture.Store(PowerUpType.Freeze);
                Assert.That(fixture.Manager.TryUseStoredAbility(), Is.True);

                Assert.That(fixture.EnemyManager.IsMovementSuspended, Is.True);
                Assert.That(enemy.MovementEnabled, Is.False);
                Assert.That(enemyRenderer.sharedMaterial, Is.SameAs(fixture.FrozenMaterial));
                var before = enemy.transform.position;
                enemy.AdvanceMovement(.5f);
                Assert.That(enemy.transform.position, Is.EqualTo(before));

                fixture.Manager.ExpireEffect(PowerUpType.Freeze);
                Assert.That(fixture.EnemyManager.IsMovementSuspended, Is.False);
                Assert.That(enemy.MovementEnabled, Is.True);
                Assert.That(enemyRenderer.sharedMaterial, Is.SameAs(original));
                enemy.AdvanceMovement(.1f);
                Assert.That(enemy.transform.position, Is.Not.EqualTo(before));
            }
        }

        [Test]
        public void ArenaTilt_DriftsEnemiesSlowsPlayerRollsCameraAndRestores()
        {
            using (var fixture = new Fixture())
            {
                var enemy = fixture.SpawnEnemy(new GridCoordinate(20, 40));
                var baseSpeed = fixture.Player.MoveSpeed;
                fixture.Store(PowerUpType.ArenaTilt);
                Assert.That(fixture.Manager.TryUseStoredAbility(), Is.True);

                Assert.That(fixture.Player.MoveSpeed, Is.EqualTo(baseSpeed * .8f).Within(.0001f));
                Assert.That(Mathf.Abs(enemy.Drift.x), Is.EqualTo(.4f).Within(.0001f));
                Assert.That(enemy.Drift.y, Is.Zero);
                var roll = Mathf.DeltaAngle(0f, fixture.Camera.localEulerAngles.z);
                Assert.That(Mathf.Abs(roll), Is.EqualTo(6f).Within(.01f));
                Assert.That(Mathf.Sign(roll), Is.EqualTo(Mathf.Sign(enemy.Drift.x)), "the side the aliens drift toward is the side that tilts down");
                var rightSideOnScreen = Quaternion.Inverse(fixture.Camera.localRotation) * Vector3.right;
                Assert.That(Mathf.Sign(rightSideOnScreen.y), Is.EqualTo(-Mathf.Sign(enemy.Drift.x)), "a rightward drift lowers the right side on screen");

                fixture.Manager.ExpireEffect(PowerUpType.ArenaTilt);
                Assert.That(fixture.Player.MoveSpeed, Is.EqualTo(baseSpeed));
                Assert.That(enemy.Drift, Is.EqualTo(Vector2.zero));
                Assert.That(fixture.Camera.localRotation, Is.EqualTo(Quaternion.identity));
            }
        }

        [Test]
        public void ConfiguredTiltDrift_LeavesEveryAlienAbleToMoveAgainstTheTilt()
        {
            var tilt = UnityEditor.AssetDatabase.LoadAssetAtPath<PowerUpDefinition>("Assets/SpaceXonix/ScriptableObjects/PowerUps/ArenaTilt.asset");
            Assert.That(tilt, Is.Not.Null);
            foreach (var name in new[] { "BasicBouncer", "LinearAlien", "UnstableAlien", "VolatileAlien" })
            {
                var alien = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"Assets/SpaceXonix/ScriptableObjects/Enemies/{name}.asset");
                var slowest = alien.type == EnemyType.Unstable ? alien.unstableMinSpeed : alien.moveSpeed;
                // Bouncers move diagonally; horizontal Linear aliens move along X at full speed.
                var sideways = alien.type == EnemyType.Linear ? slowest : slowest * Mathf.Sqrt(.5f);
                Assert.That(tilt.tiltEnemyDrift, Is.LessThan(sideways * .8f), $"{name} would free-fall toward the tilted wall");

                var model = new EnemyMovementModel(Vector2.zero, new Vector2(-sideways, 0f)) { Drift = new Vector2(tilt.tiltEnemyDrift, 0f) };
                model.Advance(1f, p => true);
                Assert.That(model.Position.x, Is.LessThan(0f), $"{name} still makes progress against the tilt");
            }
        }

        [Test]
        public void ReactivatingTilt_DoesNotCompoundPlayerSlow()
        {
            using (var fixture = new Fixture())
            {
                var baseSpeed = fixture.Player.MoveSpeed;
                fixture.Store(PowerUpType.ArenaTilt);
                fixture.Manager.TryUseStoredAbility();
                fixture.Store(PowerUpType.ArenaTilt);
                fixture.Manager.TryUseStoredAbility();
                Assert.That(fixture.Player.MoveSpeed, Is.EqualTo(baseSpeed * .8f).Within(.0001f));
                fixture.Manager.ExpireEffect(PowerUpType.ArenaTilt);
                Assert.That(fixture.Player.MoveSpeed, Is.EqualTo(baseSpeed));
            }
        }

        [Test]
        public void GameOver_EndsEffectsAndClearsBoardPickup()
        {
            using (var fixture = new Fixture(startingLives: 1))
            {
                fixture.Store(PowerUpType.Freeze);
                fixture.Manager.TryUseStoredAbility();
                fixture.Manager.TrySpawnFromCapture(10f);
                Assert.That(fixture.Manager.ActivePickup, Is.Not.Null);
                Assert.That(fixture.Game.ReportPlayerFailure(PlayerFailureReason.Laser), Is.True);
                Assert.That(fixture.Game.CurrentState, Is.EqualTo(GameplayState.GameOver));
                Assert.That(fixture.Manager.IsEffectActive(PowerUpType.Freeze), Is.False);
                Assert.That(fixture.Manager.ActivePickup, Is.Null);
                Assert.That(fixture.Manager.TryUseStoredAbility(), Is.False);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("PowerUpFixture");
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            private readonly GameObject enemyPrefab;
            private readonly EnemyDefinition enemyDefinition;
            private readonly Dictionary<PowerUpType, PowerUpDefinition> definitions = new Dictionary<PowerUpType, PowerUpDefinition>();
            public readonly BoardManager Board;
            public readonly InputRouter Input;
            public readonly PlayerController Player;
            public readonly GameManager Game;
            public readonly EnemyManager EnemyManager;
            public readonly PowerUpManager Manager;
            public readonly PowerUpSpawnDefinition SpawnDefinition;
            public readonly GameObject ShieldVisual;
            public readonly Material FrozenMaterial;
            public readonly Transform Camera;

            public Fixture(int startingLives = 3)
            {
                Time.timeScale = 1f;
                root.SetActive(false);
                Board = root.AddComponent<BoardManager>();
                Board.Initialize();
                Input = root.AddComponent<InputRouter>();
                var playerObject = new GameObject("Player"); owned.Add(playerObject);
                Player = playerObject.AddComponent<PlayerController>();
                playerObject.transform.position = Board.GetWorldPosition(new GridCoordinate(0, 1));
                Invoke(Player, "Awake");
                Game = root.AddComponent<GameManager>();
                Set(Game, "inputRouter", Input); Set(Game, "playerController", Player); Set(Game, "boardManager", Board);
                Set(Game, "startingLives", startingLives);
                var pool = root.AddComponent<PoolService>();
                EnemyManager = root.AddComponent<EnemyManager>();
                Set(EnemyManager, "boardManager", Board); Set(EnemyManager, "gameManager", Game);
                Set(EnemyManager, "playerController", Player); Set(EnemyManager, "poolService", pool);

                enemyPrefab = new GameObject("EnemyPrefab"); enemyPrefab.AddComponent<BasicBouncer>(); enemyPrefab.AddComponent<MeshRenderer>();
                enemyPrefab.SetActive(false); owned.Add(enemyPrefab);
                enemyDefinition = ScriptableObject.CreateInstance<EnemyDefinition>(); owned.Add(enemyDefinition);
                var pickupPrefab = new GameObject("PickupPrefab"); pickupPrefab.AddComponent<PowerUpPickup>(); pickupPrefab.SetActive(false); owned.Add(pickupPrefab);

                SpawnDefinition = ScriptableObject.CreateInstance<PowerUpSpawnDefinition>(); owned.Add(SpawnDefinition);
                SpawnDefinition.baseChance = 1f; SpawnDefinition.maxChance = 1f; SpawnDefinition.pickupLifetime = 0f;
                foreach (PowerUpType type in Enum.GetValues(typeof(PowerUpType)))
                {
                    var definition = ScriptableObject.CreateInstance<PowerUpDefinition>(); owned.Add(definition);
                    definition.type = type;
                    definition.duration = type == PowerUpType.Shield ? 4f : type == PowerUpType.Freeze ? 3f : 5f;
                    definitions[type] = definition;
                }
                ShieldVisual = new GameObject("ShieldVisual"); owned.Add(ShieldVisual);
                FrozenMaterial = new Material(Shader.Find("Hidden/Internal-Colored")); owned.Add(FrozenMaterial);
                var cameraObject = new GameObject("Camera"); owned.Add(cameraObject);
                Camera = cameraObject.transform;

                Manager = root.AddComponent<PowerUpManager>();
                Set(Manager, "gameManager", Game); Set(Manager, "boardManager", Board); Set(Manager, "inputRouter", Input);
                Set(Manager, "playerController", Player); Set(Manager, "enemyManager", EnemyManager); Set(Manager, "poolService", pool);
                Set(Manager, "spawnDefinition", SpawnDefinition); Set(Manager, "pickupPrefab", pickupPrefab);
                Set(Manager, "powerUps", new List<PowerUpDefinition>(definitions.Values).ToArray());
                Set(Manager, "shieldVisual", ShieldVisual); Set(Manager, "frozenEnemyMaterial", FrozenMaterial);
                Set(Manager, "arenaCamera", Camera); Set(Manager, "randomSeed", 1234);

                Invoke(Game, "Awake");
                root.SetActive(true);
                Invoke(Game, "Start");
                Invoke(Manager, "Awake");
                Invoke(Manager, "OnEnable");
            }

            public EnemyController SpawnEnemy(GridCoordinate cell)
            {
                Assert.That(EnemyManager.Spawn(enemyPrefab, enemyDefinition, cell, Vector2.right), Is.True);
                return EnemyManager.ActiveEnemies[EnemyManager.ActiveEnemies.Count - 1];
            }

            public EnemyController PrepareExposedShipFacingEnemy(bool shielded)
            {
                if (shielded)
                {
                    Store(PowerUpType.Shield);
                    Assert.That(Manager.TryUseStoredAbility(), Is.True);
                }
                Input.TrySelectDirection(CardinalDirection.Up);
                for (var i = 0; i < 400 && Board.PlayerCell.Y < 20; i++) Player.AdvanceMovement(.02f);
                Input.TrySelectDirection(CardinalDirection.Right);
                for (var i = 0; i < 400 && Board.PlayerCell.X < 4; i++) Player.AdvanceMovement(.02f);
                Input.ReleaseDirection();
                Assert.That(Board.IsPlayerExposed, Is.True);
                var start = Board.GetWorldPosition(new GridCoordinate(Board.PlayerCell.X + 3, Board.PlayerCell.Y));
                return SpawnEnemyAt(start, Vector2.left);
            }

            public void Teleport(EnemyController enemy, Vector3 worldPosition, Vector2 velocity)
            {
                enemy.transform.position = worldPosition;
                typeof(EnemyController).GetField("movement", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(enemy, new EnemyMovementModel(worldPosition, velocity));
            }

            public EnemyController SpawnEnemyAt(Vector3 worldPosition, Vector2 direction)
            {
                var enemy = SpawnEnemy(new GridCoordinate(30, 60));
                enemy.transform.position = worldPosition;
                typeof(EnemyController).GetField("movement", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(enemy, new EnemyMovementModel(worldPosition, direction));
                return enemy;
            }

            public void SpawnPickupAtPlayer(PowerUpType type)
            {
                Assert.That(Manager.TrySpawnFromCapture(10f), Is.True);
                var cell = Board.PlayerCell;
                Manager.ActivePickup.Configure(definitions[type], cell, Board.GetWorldPosition(cell), 0f);
            }

            public void Store(PowerUpType type)
            {
                SpawnPickupAtPlayer(type);
                Assert.That(Manager.CollectActivePickup(), Is.True);
            }

            public void Dispose()
            {
                Time.timeScale = 1f;
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }

            private static void Set(object target, string name, object value) =>
                target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

            private static void Invoke(object target, string name) =>
                target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        }
    }
}
