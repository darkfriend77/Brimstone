﻿using System.Collections.Generic;

namespace Brimstone
{
	public class ActionGraph
	{
		public List<QueueAction> Graph { get; private set; }

		public ActionGraph(QueueAction q) {
			Graph = new List<QueueAction>() { q };
		}

		public ActionGraph(ActionGraph g) {
			Graph = new List<QueueAction>(g.Graph);
		}

		// Convert single QueueAction to ActionGraph
		public static implicit operator ActionGraph(QueueAction q) {
			return new ActionGraph(q);
		}

		public ActionGraph Then(ActionGraph act) {
			Graph.AddRange(act.Graph);
			return this;
		}

		public ActionGraph Repeat(ActionGraph qty) {
			var repeatAction = new Repeat { Actions = new ActionGraph(this), Args = { qty } };
			Graph = new List<QueueAction>() { repeatAction };
			return this;
		}

		// Convert values to actions
		public static implicit operator ActionGraph(int x) {
			return new FixedNumber { Num = x };
		}
		public static implicit operator ActionGraph(Card x) {
			return new FixedCard { Card = x };
		}
		public static implicit operator ActionGraph(Entity x) {
			return new LazyEntity { EntityId = x.Id };
		}

		// Repeated action
		public static ActionGraph operator *(ActionGraph x, ActionGraph y) {
			return x.Repeat(y);
		}

		// Add the graph to the game's action queue
		public void Queue(IEntity source, ActionQueue queue) {
			foreach (var action in Graph) {
				foreach (var arg in action.Args)
					arg.Queue(source, queue);
				queue.EnqueuePaused(source, action);
			}
		}
	}
}