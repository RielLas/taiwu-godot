using System;
using System.Collections.Generic;
using TaiwuGu.Core;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Lightweight, modular Entity-Component Store (ECS-Lite / Component Data Store).
    /// Provides:
    /// - POCO serialization friendly state container
    /// - Constant-time O(1) spatial index for instant tile occupancy queries
    /// - Extensible component attachment without touching monolithic state classes
    /// </summary>
    [Serializable]
    public class EntityStore
    {
        private ulong _nextEntityId = 1;

        // Master entity set
        public HashSet<ulong> ActiveEntities { get; set; } = new();

        // Component tables (Type-indexed POCO bags)
        // Key: Type name or component key, Value: EntityId -> Component object
        public Dictionary<string, Dictionary<ulong, object>> Components { get; set; } = new();

        // High-performance spatial lookup table: GridCoord -> Set of Entity IDs
        [NonSerialized]
        private readonly Dictionary<GridCoord, HashSet<ulong>> _spatialIndex = new();

        public EntityStore()
        {
        }

        public EntityId CreateEntity()
        {
            var id = new EntityId(_nextEntityId++);
            ActiveEntities.Add(id.Value);
            return id;
        }

        public EntityId CreateEntityWithId(ulong specificId)
        {
            var id = new EntityId(specificId);
            ActiveEntities.Add(id.Value);
            if (specificId >= _nextEntityId)
            {
                _nextEntityId = specificId + 1;
            }
            return id;
        }

        public bool IsAlive(EntityId id) => ActiveEntities.Contains(id.Value);

        public void DestroyEntity(EntityId id)
        {
            if (!ActiveEntities.Remove(id.Value)) return;

            // Remove from spatial index if it has position
            var pos = Get<PositionComponent>(id);
            if (pos != null)
            {
                RemoveSpatialIndex(id.Value, pos.Coord);
            }

            // Remove all components
            foreach (var table in Components.Values)
            {
                table.Remove(id.Value);
            }
        }

        public T Set<T>(EntityId id, T component) where T : class
        {
            if (!ActiveEntities.Contains(id.Value))
            {
                ActiveEntities.Add(id.Value);
            }

            string key = typeof(T).FullName ?? typeof(T).Name;
            if (!Components.TryGetValue(key, out var table))
            {
                table = new Dictionary<ulong, object>();
                Components[key] = table;
            }

            // Spatial tracking special-case
            if (component is PositionComponent newPos)
            {
                if (table.TryGetValue(id.Value, out var oldObj) && oldObj is PositionComponent oldPos)
                {
                    RemoveSpatialIndex(id.Value, oldPos.Coord);
                }
                AddSpatialIndex(id.Value, newPos.Coord);
            }

            table[id.Value] = component;
            return component;
        }

        public T? Get<T>(EntityId id) where T : class
        {
            string key = typeof(T).FullName ?? typeof(T).Name;
            if (Components.TryGetValue(key, out var table) && table.TryGetValue(id.Value, out var comp))
            {
                return comp as T;
            }
            return null;
        }

        public bool Has<T>(EntityId id) where T : class
        {
            string key = typeof(T).FullName ?? typeof(T).Name;
            return Components.TryGetValue(key, out var table) && table.ContainsKey(id.Value);
        }

        public bool Remove<T>(EntityId id) where T : class
        {
            string key = typeof(T).FullName ?? typeof(T).Name;
            if (Components.TryGetValue(key, out var table))
            {
                if (typeof(T) == typeof(PositionComponent) && table.TryGetValue(id.Value, out var obj) && obj is PositionComponent pos)
                {
                    RemoveSpatialIndex(id.Value, pos.Coord);
                }
                return table.Remove(id.Value);
            }
            return false;
        }

        public void SetPosition(EntityId id, GridCoord newCoord)
        {
            var pos = Get<PositionComponent>(id);
            if (pos == null)
            {
                Set(id, new PositionComponent(newCoord));
            }
            else
            {
                RemoveSpatialIndex(id.Value, pos.Coord);
                pos.Coord = newCoord;
                AddSpatialIndex(id.Value, newCoord);
            }
        }

        public IEnumerable<EntityId> GetEntitiesAt(GridCoord coord)
        {
            if (_spatialIndex.TryGetValue(coord, out var set))
            {
                foreach (var id in set)
                {
                    yield return new EntityId(id);
                }
            }
        }

        public IEnumerable<(EntityId Id, T Component)> GetAll<T>() where T : class
        {
            string key = typeof(T).FullName ?? typeof(T).Name;
            if (Components.TryGetValue(key, out var table))
            {
                foreach (var kvp in table)
                {
                    if (kvp.Value is T comp)
                    {
                        yield return (new EntityId(kvp.Key), comp);
                    }
                }
            }
        }

        public void RebuildSpatialIndex()
        {
            _spatialIndex.Clear();
            foreach (var (id, pos) in GetAll<PositionComponent>())
            {
                AddSpatialIndex(id.Value, pos.Coord);
            }
        }

        private void AddSpatialIndex(ulong id, GridCoord coord)
        {
            if (!_spatialIndex.TryGetValue(coord, out var set))
            {
                set = new HashSet<ulong>();
                _spatialIndex[coord] = set;
            }
            set.Add(id);
        }

        private void RemoveSpatialIndex(ulong id, GridCoord coord)
        {
            if (_spatialIndex.TryGetValue(coord, out var set))
            {
                set.Remove(id);
                if (set.Count == 0)
                {
                    _spatialIndex.Remove(coord);
                }
            }
        }

        public void Clear()
        {
            ActiveEntities.Clear();
            Components.Clear();
            _spatialIndex.Clear();
            _nextEntityId = 1;
        }
    }
}
