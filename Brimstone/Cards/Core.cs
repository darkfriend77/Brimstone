﻿namespace Brimstone
{
	public partial class BehaviourScripts : Behaviours
	{
		// Game
		public static Behaviour Game = new Behaviour {
			Triggers =
			{
				At(TriggerType.GameStart, StartGame),
				At(TriggerType.BeginMulligan, BeginMulligan),
				At(TriggerType.PhaseMainNext, EndTurn)
			}
		};

		// Player
		public static Behaviour Player = new Behaviour {
			Triggers = {
				At(TriggerType.DealMulligan, IsSelf, PerformMulligan),
				At(TriggerType.MulliganWaiting, IsSelf, WaitForMulliganComplete),
				At(TriggerType.PhaseMainReady, IsFriendly, BeginTurn),
				At(TriggerType.PhaseMainStartTriggers, IsFriendly, BeginTurnTriggers),
				At(TriggerType.PhaseMainStart, IsFriendly, BeginTurnForPlayer),
				At(TriggerType.PhaseMainAction, IsFriendly, (System.Action<IEntity>)(p => { p.Game.NextStep = Step.MAIN_END; })),
				At(TriggerType.PhaseMainEnd, IsFriendly, EndTurnForPlayer),
				At(TriggerType.PhaseMainCleanup, IsFriendly, EndTurnCleanupForPlayer)
			}
		};
	}
}
