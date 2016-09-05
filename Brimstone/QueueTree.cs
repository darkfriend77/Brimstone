﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Brimstone
{
	public class QueueNode
	{
		public QueueActionEventArgs Data { get; }
		public int Depth { get; }
		public Dictionary<Game, QueueNode> NextSameDepth { get; } = new Dictionary<Game, QueueNode>();
		public Dictionary<Game, QueueNode> NextChildDepth { get; } = new Dictionary<Game, QueueNode>();

		public QueueNode(QueueActionEventArgs data, int depth = 0) {
			Data = data;
			Depth = depth;
		}
	}

	public class QueueTree : ICloneable
	{
		private readonly Stack<QueueNode> InsertionPoints;
		private readonly Stack<QueueNode> DequeuePoints;
		private QueueNode RootNode;
		private QueueNode CurrentInsertionPoint;
		private QueueNode CurrentDequeuePoint;
		private QueueNode PreviousDequeuePoint;

		public Game Game { get; set; }
		public int Depth => InsertionPoints.Count;
		public bool IsBranchEmpty => CurrentDequeuePoint == null;
		public bool IsEmpty => CurrentDequeuePoint == null && Depth == 0;

		public event Action OnBranchResolved;
		public event Action OnTreeResolved;

		public QueueTree() {
			InsertionPoints = new Stack<QueueNode>();
			DequeuePoints = new Stack<QueueNode>();
		}

		public QueueTree(QueueTree cloneFrom) {
			// Does not clone events
			InsertionPoints = new Stack<QueueNode>(cloneFrom.InsertionPoints.Reverse());
			DequeuePoints = new Stack<QueueNode>(cloneFrom.DequeuePoints.Reverse());
			RootNode = cloneFrom.RootNode;
			CurrentInsertionPoint = cloneFrom.CurrentInsertionPoint;
			CurrentDequeuePoint = cloneFrom.CurrentDequeuePoint;
		}

		public void Enqueue(QueueActionEventArgs action) {
			if (RootNode == null && Depth == 0) {
#if _QUEUE_DEBUG
				DebugLog.WriteLine("QueueTree: Seeding root node with " + action);
#endif
				RootNode = new QueueNode(action);
				CurrentInsertionPoint = RootNode;
				CurrentDequeuePoint = RootNode;
				PreviousDequeuePoint = null;
			}
			else {
				var node = new QueueNode(action, Depth);
				if (node.Depth == CurrentInsertionPoint.Depth) {
#if _QUEUE_DEBUG
					DebugLog.WriteLine("QueueTree: Adding node " + action + " - at depth " + Depth + " as sibling");
#endif
					CurrentInsertionPoint.NextSameDepth.Add(Game, node);
				}
				else {
#if _QUEUE_DEBUG
					DebugLog.WriteLine("QueueTree: Adding node " + action + " - at depth " + Depth + " as child");
#endif
					if (CurrentInsertionPoint.NextChildDepth.ContainsKey(Game))
						CurrentInsertionPoint.NextChildDepth[Game] = node;
					else
						CurrentInsertionPoint.NextChildDepth.Add(Game, node);
					CurrentDequeuePoint = node;
				}
				CurrentInsertionPoint = node;
			}
		}

		public QueueActionEventArgs Dequeue(int MaxUnwindDepth = 0)
		{
			PreviousDequeuePoint = CurrentDequeuePoint;
			if (CurrentDequeuePoint == null) {
				if (RootNode == null) {
#if _QUEUE_DEBUG
					DebugLog.WriteLine("QueueTree: Attempt to dequeue when tree is empty");
#endif
					return null;
				}
				CurrentDequeuePoint = RootNode;
#if _QUEUE_DEBUG
				DebugLog.WriteLine("QueueTree: Restarting from root node");
#endif
			}
			var node = CurrentDequeuePoint;
#if _QUEUE_DEBUG
			DebugLog.WriteLine("QueueTree: Dequeued " + node.Data + " - at depth " + Depth);
#endif
			// Let nodes we are finished with go out of scope so they can be garbage collected
			if (Depth == 0)
				RootNode = node;
			return node.Data;
		}

		public int Stack() {
			InsertionPoints.Push(CurrentInsertionPoint);
			DequeuePoints.Push(CurrentDequeuePoint);
#if _QUEUE_DEBUG
			DebugLog.WriteLine("QueueTree: Created new branch at depth " + Depth + " (current dequeue point is: " + (CurrentDequeuePoint?.Data.ToString() ?? "<end of branch>") + ")");
#endif
			return Depth;
		}

		public QueueNode GetNextSiblingOrChildNode() {
			QueueNode nextDequeuePoint;
			CurrentDequeuePoint.NextSameDepth.TryGetValue(Game, out nextDequeuePoint);
#if _QUEUE_DEBUG
			DebugLog.WriteLine("QueueTree: Next node to dequeue is: " + (nextDequeuePoint?.Data.ToString() ?? "<end of branch>") + " at depth " + Depth);
#endif
			return nextDequeuePoint;
		}

		public void Advance() {
			if (CurrentDequeuePoint == null) {
#if _QUEUE_DEBUG
				DebugLog.WriteLine("QueueTree: Cannot advance further at depth " + Depth);
#endif
				return;
			}
			if (CurrentDequeuePoint == PreviousDequeuePoint)
				CurrentDequeuePoint = GetNextSiblingOrChildNode();
		}

		public void Unwind(int MaxUnwindDepth)
		{
#if _QUEUE_DEBUG
			DebugLog.WriteLine("Unwinding to minimum depth " + MaxUnwindDepth);
			bool changed = false;
#endif
			while (CurrentDequeuePoint == null && Depth >= MaxUnwindDepth && Depth > 0) {
#if _QUEUE_DEBUG
				DebugLog.WriteLine("QueueTree: Reached end of branch at depth " + Depth);
#endif
				OnBranchResolved?.Invoke();
				PreviousDequeuePoint = CurrentDequeuePoint = DequeuePoints.Pop();
				CurrentInsertionPoint = InsertionPoints.Pop();
				Advance();
#if _QUEUE_DEBUG
				changed = true;
#endif
			}

			// Queue empty
			if (CurrentDequeuePoint == null && Depth == 0) {
				RootNode = null;
				OnTreeResolved?.Invoke();
#if _QUEUE_DEBUG
				DebugLog.WriteLine("QueueTree: Reached end of tree");
				changed = true;
#endif
			}
#if _QUEUE_DEBUG
			if (changed)
				DebugLog.WriteLine("QueueTree: Next node to dequeue after unwind is " + (CurrentDequeuePoint?.Data.ToString() ?? "<none>") + " at depth " + Depth);
#endif
		}

		public object Clone() {
			return new QueueTree(this);
		}
	}
}
