using Godot;

namespace GridQuest.entities.enemy;

/// <summary>
/// Scene/script compatibility wrapper. Enemy implementation lives in entities/enemy/.
/// </summary>
[GlobalClass]
public partial class Enemy : GridQuest.Entities.Enemy.EnemyEntity
{
}