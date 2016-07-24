﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Brimstone
{
	public interface IEntity : IEnumerable<KeyValuePair<GameTag, int>>, ICloneable
	{
		int Id { get; set; }
		// Allow owner game and controller to be changed for state cloning
		Game Game { get; set; }
		IEntity Controller { get; set; }
		Card Card { get; }
		Dictionary<GameTag, int> CopyTags();
		int this[GameTag t] { get; set; }
		string ShortDescription { get; }
		int FuzzyHash { get; }

		IEntity CloneState();
	}

	public interface IPlayable : IEntity
	{
		IPlayable Play();
	}

	public interface IMinion : IPlayable
	{
		void Hit(int amount);
	}

	public interface ISpell : IPlayable
	{
	}

	public class BaseEntityData : ICloneable
	{
		public int Id { get; set; }
		public Card Card { get; }
		public Dictionary<GameTag, int> Tags { get; }

		public int this[GameTag t] {
			get {
				// Use the entity tag if available, otherwise the card tag
				if (Tags.ContainsKey(t))
					return Tags[t];
				if (Card.Tags.ContainsKey(t))
					return Card[t];
				return 0;
			}
			set {
				Tags[t] = value;
			}
		}

		public BaseEntityData(Card card, Dictionary<GameTag, int> tags = null) {
			Card = card;
			if (tags != null)
				Tags = tags;
			else
				Tags = new Dictionary<GameTag, int>((int)GameTag._COUNT);
		}

		// Cloning copy constructor
		public BaseEntityData(BaseEntityData cloneFrom) {
			Card = cloneFrom.Card;
			Id = cloneFrom.Id;
			Tags = new Dictionary<GameTag, int>(cloneFrom.Tags);
		}

		public virtual object Clone() {
			return new BaseEntityData(this);
		}
	}

	public class ReferenceCount
	{
		public ReferenceCount() {
			Count = 1;
		}

		public int Count { get; set; }
	}

	public partial class Entity : IEntity
	{
		private BaseEntityData _entity;
		private ReferenceCount _referenceCount;

		public int ReferenceCount { get { return _referenceCount.Count; } }
		public BaseEntityData BaseEntityData { get { return _entity; } }

		public Game Game { get; set; }
		private IEntity _controller;
		public IEntity Controller {
			get {
				return _controller;
			}
			set {
				if (Game != null)
					if (Game.Entities != null)
						Changing(false);
				_controller = value;
			}
		}

		public Entity(Entity cloneFrom) {
			_fuzzyHash = cloneFrom._fuzzyHash;
			_entity = cloneFrom._entity;
			_referenceCount = cloneFrom._referenceCount;
			_referenceCount.Count++;
		}

		public Entity(Game game, IEntity controller, Card card, Dictionary<GameTag, int> tags = null) {
			_entity = new BaseEntityData(card, tags);
			_referenceCount = new ReferenceCount();
			_controller = controller;
			if (game != null) {
				game.Entities.Add(this);
				game.Entities.EntityChanging(_entity.Id, 0);
			}
		}

		public int this[GameTag t] {
			get {
				if (t == GameTag.ENTITY_ID)
					return _entity.Id;
				if (t == GameTag.CONTROLLER)
					return Controller.Id;
				return _entity[t];
			}
			set {
				// Ignore unchanged data
				if (_entity.Tags.ContainsKey(t) && _entity[t] == value)
					return;
				else if (t == GameTag.CONTROLLER) {
					Controller = Game.Entities[(int)value];
				}
				else if (t == GameTag.ENTITY_ID) {
					Changing();
					_entity.Id = (int)value;
				} else {
					Changing();
					_entity[t] = value;
				}
				if (Game != null)
					Game.PowerHistory.Add(new TagChange(this, new Tag(t, value)));
			}
		}

		public Card Card {
			get {
				return _entity.Card;
			}
		}

		public int Id {
			get {
				return _entity.Id;
			}

			set {
				_entity.Id = value;
			}
		}

		// TODO: Add Zone property helper semantics

		public virtual object Clone() {
			return new Entity(this);
		}

		public virtual IEntity CloneState() {
			return Clone() as IEntity;
		}

		private void Changing(bool cow = true) {
			// TODO: Replace with a C# event
			Game.Entities.EntityChanging(Id, _fuzzyHash);
			_fuzzyHash = 0;
			if (cow) CopyOnWrite();
		}

		private void CopyOnWrite() {
			if (_referenceCount.Count > 1) {
				_entity = (BaseEntityData)_entity.Clone();
				_referenceCount.Count--;
				_referenceCount = new ReferenceCount();
			}
		}

		// Returns a *copy* of all tags from both the entity and the underlying card
		public Dictionary<GameTag, int> CopyTags() {
			var allTags = new Dictionary<GameTag, int>(_entity.Card.Tags);
			
			// Entity tags override card tags
			foreach (var tag in _entity.Tags)
				allTags[tag.Key] = tag.Value;

			// Specially handled tags
			allTags[GameTag.CONTROLLER] = Controller.Id;
			allTags[GameTag.ENTITY_ID] = _entity.Id;
			return allTags;
		}

		public IEnumerator<KeyValuePair<GameTag, int>> GetEnumerator() {
			// Hopefully we're only iterating through tags in test code
			// so it doesn't matter that we are making a deep clone of the dictionary
			return CopyTags().GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public string ShortDescription {
			get {
				return Card.Name + " [" + Id + "]";
			}
		}

		// Get a NON-UNIQUE hash code for the entity (without copying the tags for speed)
		// This is used for testing fuzzy entity equality across games
		// The ENTITY_ID is left out, and the ZONE_POSITION is left out if the entity is in the player's hand
		// All underlying card tags are included to differentiate cards from each other. CONTROLLER is included
		private int _fuzzyHash = 0;
		public int FuzzyHash {
			// TODO: Caching
			get {
				if (_fuzzyHash != 0)
					return _fuzzyHash;
				bool inHand = _entity.Tags.ContainsKey(GameTag.ZONE) && _entity.Tags[GameTag.ZONE] == (int)Zone.HAND;
				int hash = 17;
				// The card's asset ID uniquely identifies the set of immutable starting tags for the card
				hash = hash * 31 + _entity.Card.AssetId;
				foreach (var kv in _entity.Tags)
					if (kv.Key != GameTag.ZONE_POSITION || !inHand) {
						hash = hash * 31 + (int)kv.Key;
						hash = hash * 31 + kv.Value;
					}
				hash = hash * 31 + (int)GameTag.CONTROLLER;
				hash = hash * 31 + Controller.Id;
				_fuzzyHash = hash;
				return _fuzzyHash;
			}
		}

		public override string ToString() {
			string s = Card.Name + " - ";
			foreach (var tag in this) {
				s += new Tag(tag.Key, tag.Value) + ", ";
			}
			return s.Substring(0, s.Length - 2);
		}
	}

	public class FuzzyEntityComparer : IEqualityComparer<IEntity>
	{
		// Used when adding to and fetching from HashSet, and testing for equality
		public bool Equals(IEntity x, IEntity y) {
			return x.FuzzyHash == y.FuzzyHash;
		}

		public int GetHashCode(IEntity obj) {
			return obj.FuzzyHash;
		}
	}

	public class EntityController : IEnumerable<IEntity>, ICloneable {
		public Game Game { get; }
		public int NextEntityId = 1;

		private Dictionary<int, IEntity> Entities = new Dictionary<int, IEntity>();

		public IEntity this[int id] {
			get {
				return Entities[id];
			}
		}

		public int Count {
			get {
				return Entities.Count;
			}
		}

		public ICollection<int> Keys {
			get {
				return Entities.Keys;
			}
		}

		public bool ContainsKey(int key) {
			return Entities.ContainsKey(key);
		}

		public EntityController(Game game) {
			Game = game;

			// Fuzzy hashing
			_changedHashes = new HashSet<int>();
		}

		public EntityController(EntityController es) {
			_gameHash = es._gameHash;
			_undoHash = es._undoHash;
			_changedHashes = new HashSet<int>(es._changedHashes);

			NextEntityId = es.NextEntityId;
			foreach (var entity in es) {
				Entities.Add(entity.Id, (IEntity) entity.Clone());
			}
			// Change ownership
			Game = FindGame();
			foreach (var entity in Entities)
				entity.Value.Game = Game;
			foreach (var entity in Entities)
				entity.Value.Controller = Entities[es.Entities[entity.Key].Controller.Id];
		}

		public int Add(IEntity entity) {
			entity.Game = Game;
			entity.Id = NextEntityId++;
			Entities[entity.Id] = entity;
			Game.PowerHistory.Add(new CreateEntity(entity));
			Game.ActiveTriggers.Add(entity);
			return entity.Id;
		}

		public Game FindGame() {
			// Game is always entity ID 1
			return (Game)Entities[1];
		}

		public Player FindPlayer(int p) {
			// Player is always p+1
			return (Player)Entities[p + 1];
		}

		// Calculate a fuzzy hash for the whole game state
		// WARNING: The hash algorithm MUST be designed in such a way that the order
		// in which the entities are hashed doesn't matter
		private int _gameHash = 0;
		private int _undoHash = 0;
		private HashSet<int> _changedHashes;

		public void EntityChanging(int id, int previousHash) {
			// Only undo hash once if multiple changes occur since we last re-calculated
			if (!_changedHashes.Contains(id)) {
				_undoHash ^= previousHash;
				_changedHashes.Add(id);
			}
		}

		public int FuzzyGameHash {
			get {
				_gameHash ^= _undoHash;
				foreach (var eId in _changedHashes)
					_gameHash ^= Entities[eId].FuzzyHash;
				_changedHashes.Clear();
				_undoHash = 0;
				return _gameHash;
			}
		}

		public IEnumerator<IEntity> GetEnumerator() {
			return Entities.Values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		public object Clone() {
			return new EntityController(this);
		}
	}
}