using System;
using UnityEngine;
namespace SpaceXonix.Enemies
{
    public sealed class UnstableEnemy : EnemyController
    {
        private float timer;
        private float currentSpeed;
        public event Action<float> SpeedChanged;
        public float CurrentSpeed => currentSpeed;
        public float SpeedChangeTimeRemaining => timer;
        public override void Activate(EnemyDefinition data, SpaceXonix.Board.BoardManager board, SpaceXonix.Core.GameManager game, Vector3 position, Vector2 direction)
        {
            base.Activate(data, board, game, position, direction);
            timer = data.unstableInterval;
            currentSpeed = Mathf.Clamp(data.moveSpeed, data.unstableMinSpeed, data.unstableMaxSpeed);
            movement.SetSpeed(currentSpeed);
        }
        protected override void Update()
        {
            if (IsActiveEnemy && movement.MovementEnabled)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    timer += definition.unstableInterval;
                    currentSpeed = currentSpeed >= definition.unstableMaxSpeed ? definition.unstableMinSpeed : definition.unstableMaxSpeed;
                    movement.SetSpeed(currentSpeed); SpeedChanged?.Invoke(currentSpeed);
                }
            }
            base.Update();
        }
    }
}
