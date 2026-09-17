using NUnit.Framework;
using SpaceXonix.Core;

namespace SpaceXonix.Tests.EditMode
{
    public sealed class LifeStateModelTests
    {
        [Test]
        public void StageStartsWithConfiguredLives()
        {
            var lives = new LifeStateModel(3);
            Assert.That(lives.Lives, Is.EqualTo(3));
            Assert.That(lives.State, Is.EqualTo(GameplayState.Playing));
        }

        [Test]
        public void FailureTransitionsToRespawningAndConsumesOneLife()
        {
            var lives = new LifeStateModel(3);
            Assert.That(lives.TryFail(), Is.True);
            Assert.That(lives.Lives, Is.EqualTo(2));
            Assert.That(lives.State, Is.EqualTo(GameplayState.Respawning));
        }

        [Test]
        public void DuplicateFailureDuringRespawnIsIgnored()
        {
            var lives = new LifeStateModel(3);
            lives.TryFail();
            Assert.That(lives.TryFail(), Is.False);
            Assert.That(lives.Lives, Is.EqualTo(2));
        }

        [Test]
        public void ThreeFailuresReachGameOverWithoutFourthRespawn()
        {
            var lives = new LifeStateModel(3);
            lives.TryFail(); lives.CompleteRespawn();
            lives.TryFail(); lives.CompleteRespawn();
            Assert.That(lives.TryFail(), Is.True);
            Assert.That(lives.Lives, Is.Zero);
            Assert.That(lives.State, Is.EqualTo(GameplayState.GameOver));
            Assert.That(lives.CompleteRespawn(), Is.False);
            Assert.That(lives.TryFail(), Is.False);
        }

        [Test]
        public void PausedStateRejectsFailureUntilGameplayResumes()
        {
            var lives = new LifeStateModel(3);
            lives.SetState(GameplayState.Paused);
            Assert.That(lives.TryFail(), Is.False);
            lives.SetState(GameplayState.Playing);
            Assert.That(lives.TryFail(), Is.True);
        }

        [Test]
        public void TryCompleteStage_OnlyFromPlaying()
        {
            var model = new LifeStateModel(2);
            Assert.That(model.TryCompleteStage(), Is.True);
            Assert.That(model.State, Is.EqualTo(GameplayState.StageComplete));
            Assert.That(model.TryCompleteStage(), Is.False);
            Assert.That(model.TryFail(), Is.False);
            Assert.That(model.Lives, Is.EqualTo(2));

            var respawning = new LifeStateModel(2);
            respawning.TryFail();
            Assert.That(respawning.TryCompleteStage(), Is.False);
            Assert.That(respawning.State, Is.EqualTo(GameplayState.Respawning));
        }
    }
}
