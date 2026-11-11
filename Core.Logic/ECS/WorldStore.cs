using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TaiwuGu.Core;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Restricts polymorphic JSON deserialization strictly to Core.Logic / core runtime types.
    /// </summary>
    public sealed class KnownComponentsBinder : ISerializationBinder
    {
        private static readonly DefaultSerializationBinder s_Default = new();

        public Type BindToType(string? assemblyName, string typeName)
        {
            bool allowedType =
                typeName.StartsWith("TaiwuGu.", StringComparison.Ordinal) ||
                typeName.StartsWith("System.", StringComparison.Ordinal);

            if (!allowedType)
            {
                throw new JsonSerializationException($"Disallowed polymorphic type in save payload: '{typeName}'.");
            }

            return s_Default.BindToType(assemblyName, typeName);
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            s_Default.BindToName(serializedType, out assemblyName, out typeName);
        }
    }

    /// <summary>
    /// Modular, serializable ECS world state ("POOF" Single Source of Truth).
    /// Holds all active entity IDs, component tables, world metadata, and non-serialized O(1) indices.
    /// </summary>
    [Serializable]
    public class WorldStore
    {
        public ulong PlayerId { get; set; }
        public ulong WorldEntityId { get; set; }
        public string CurrentSectorKey { get; set; } = "Sector_SouthernBorder_GuYueVillage";
        public int RunSeed { get; set; } = 1;
        public ulong NextEntityId { get; set; } = 1;

        public HashSet<ulong> ActiveEntities { get; set; } = new();
        public Dictionary<string, Dictionary<ulong, object>> Components { get; set; } = new();

        public Dictionary<string, ulong> SectorEntitiesByKey { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<FactionID, ulong> FactionEntitiesById { get; set; } = new();
        public Dictionary<string, ulong> EntitiesByExternalId { get; set; } = new(StringComparer.Ordinal);

        [JsonIgnore]
        private readonly Dictionary<GridCoord, HashSet<ulong>> _spatialIndex = new();

        [JsonIgnore]
        private readonly Dictionary<ulong, HashSet<ulong>> _ownerIndex = new();

        public EntityId PlayerEntity => new(PlayerId);
        public EntityId WorldEntity => new(WorldEntityId);

        public EntityId CreateEntity(string? externalId = null)
        {
            var id = new EntityId(NextEntityId++);
            ActiveEntities.Add(id.Value);
            if (!string.IsNullOrEmpty(externalId))
            {
                EntitiesByExternalId[externalId] = id.Value;
            }
            return id;
        }

        public EntityId CreateEntityWithId(ulong specificId, string? externalId = null)
        {
            var id = new EntityId(specificId);
            ActiveEntities.Add(id.Value);
            if (specificId >= NextEntityId)
            {
                NextEntityId = specificId + 1;
            }
            if (!string.IsNullOrEmpty(externalId))
            {
                EntitiesByExternalId[externalId] = id.Value;
            }
            return id;
        }

        public bool IsAlive(EntityId id) => id.IsValid && ActiveEntities.Contains(id.Value);

        public EntityId FindByExternalId(string? externalId)
        {
            if (string.IsNullOrEmpty(externalId)) return EntityId.None;
            if (EntitiesByExternalId.TryGetValue(externalId, out ulong rawId) && ActiveEntities.Contains(rawId))
            {
                return new EntityId(rawId);
            }
            return EntityId.None;
        }

        public void DestroyEntity(EntityId id)
        {
            if (!id.IsValid || !ActiveEntities.Remove(id.Value)) return;

            var pos = Get<PositionComponent>(id);
            if (pos != null) RemoveSpatialIndex(id.Value, pos.Coord);

            var owner = Get<OwnerComponent>(id);
            if (owner != null) RemoveOwnerIndex(owner.OwnerEntityId, id.Value);

            var cult = Get<CultivatorComponent>(id);
            if (cult != null && !string.IsNullOrEmpty(cult.ExternalId))
            {
                EntitiesByExternalId.Remove(cult.ExternalId);
            }

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
                if (id.Value >= NextEntityId) NextEntityId = id.Value + 1;
            }

            string key = ComponentKey<T>();
            if (!Components.TryGetValue(key, out var table))
            {
                table = new Dictionary<ulong, object>();
                Components[key] = table;
            }

            if (component is PositionComponent newPos)
            {
                if (table.TryGetValue(id.Value, out var oldObj) && oldObj is PositionComponent oldPos)
                {
                    RemoveSpatialIndex(id.Value, oldPos.Coord);
                }
                AddSpatialIndex(id.Value, newPos.Coord);
            }
            else if (component is OwnerComponent newOwner)
            {
                if (table.TryGetValue(id.Value, out var oldObj) && oldObj is OwnerComponent oldOwner)
                {
                    RemoveOwnerIndex(oldOwner.OwnerEntityId, id.Value);
                }
                AddOwnerIndex(newOwner.OwnerEntityId, id.Value);
            }
            else if (component is CultivatorComponent cult && !string.IsNullOrEmpty(cult.ExternalId))
            {
                EntitiesByExternalId[cult.ExternalId] = id.Value;
            }
            else if (component is SectorGridComponent sector && !string.IsNullOrEmpty(sector.SectorKey))
            {
                SectorEntitiesByKey[sector.SectorKey] = id.Value;
            }
            else if (component is FactionComponent faction)
            {
                FactionEntitiesById[faction.ID] = id.Value;
            }

            table[id.Value] = component;
            return component;
        }

        public T? Get<T>(EntityId id) where T : class
        {
            if (!id.IsValid) return null;
            string key = ComponentKey<T>();
            if (Components.TryGetValue(key, out var table) && table.TryGetValue(id.Value, out var comp))
            {
                return comp as T;
            }
            return null;
        }

        public bool TryGet<T>(EntityId id, out T component) where T : class
        {
            T? found = Get<T>(id);
            if (found != null)
            {
                component = found;
                return true;
            }
            component = null!;
            return false;
        }

        public bool Has<T>(EntityId id) where T : class
        {
            if (!id.IsValid) return false;
            string key = ComponentKey<T>();
            return Components.TryGetValue(key, out var table) && table.ContainsKey(id.Value);
        }

        public bool Remove<T>(EntityId id) where T : class
        {
            if (!id.IsValid) return false;
            string key = ComponentKey<T>();
            if (Components.TryGetValue(key, out var table))
            {
                if (typeof(T) == typeof(PositionComponent) && table.TryGetValue(id.Value, out var posObj) && posObj is PositionComponent pos)
                {
                    RemoveSpatialIndex(id.Value, pos.Coord);
                }
                else if (typeof(T) == typeof(OwnerComponent) && table.TryGetValue(id.Value, out var ownObj) && ownObj is OwnerComponent own)
                {
                    RemoveOwnerIndex(own.OwnerEntityId, id.Value);
                }
                return table.Remove(id.Value);
            }
            return false;
        }

        public T? GetSingleton<T>() where T : class => Get<T>(WorldEntity);

        public T GetOrCreateSingleton<T>() where T : class, new()
        {
            if (TryGet<T>(WorldEntity, out var existing)) return existing;
            return Set(WorldEntity, new T());
        }

        public void SetPosition(EntityId id, GridCoord newCoord, string? sectorKey = null)
        {
            var pos = Get<PositionComponent>(id);
            if (pos == null)
            {
                Set(id, new PositionComponent(newCoord, sectorKey ?? CurrentSectorKey));
            }
            else
            {
                RemoveSpatialIndex(id.Value, pos.Coord);
                pos.Coord = newCoord;
                if (sectorKey != null) pos.SectorKey = sectorKey;
                AddSpatialIndex(id.Value, newCoord);
            }
        }

        public void SetOwner(EntityId itemId, EntityId newOwnerId, bool? isEquipped = null, int? equipSlot = null)
        {
            var owner = Get<OwnerComponent>(itemId);
            if (owner == null)
            {
                Set(itemId, new OwnerComponent(newOwnerId.Value, isEquipped ?? false, equipSlot ?? -1));
            }
            else
            {
                RemoveOwnerIndex(owner.OwnerEntityId, itemId.Value);
                owner.OwnerEntityId = newOwnerId.Value;
                if (isEquipped.HasValue) owner.IsEquipped = isEquipped.Value;
                if (equipSlot.HasValue) owner.EquipSlot = equipSlot.Value;
                AddOwnerIndex(newOwnerId.Value, itemId.Value);
            }
        }

        public IEnumerable<EntityId> GetEntitiesAt(GridCoord coord, string? sectorKeyFilter = null)
        {
            if (_spatialIndex.TryGetValue(coord, out var set))
            {
                foreach (var rawId in set)
                {
                    var eid = new EntityId(rawId);
                    if (!string.IsNullOrEmpty(sectorKeyFilter))
                    {
                        var pos = Get<PositionComponent>(eid);
                        if (pos != null && !string.IsNullOrEmpty(pos.SectorKey) &&
                            !string.Equals(pos.SectorKey, sectorKeyFilter, StringComparison.Ordinal))
                        {
                            continue;
                        }
                    }
                    yield return eid;
                }
            }
        }

        public IEnumerable<(EntityId Id, T Component)> GetAll<T>() where T : class
        {
            string key = ComponentKey<T>();
            if (Components.TryGetValue(key, out var table))
            {
                foreach (var kvp in table)
                {
                    if (ActiveEntities.Contains(kvp.Key) && kvp.Value is T comp)
                    {
                        yield return (new EntityId(kvp.Key), comp);
                    }
                }
            }
        }

        public IEnumerable<(EntityId Id, T Component)> GetOwned<T>(EntityId ownerId) where T : class
        {
            if (!_ownerIndex.TryGetValue(ownerId.Value, out var set)) yield break;

            foreach (var rawId in set)
            {
                var eid = new EntityId(rawId);
                if (ActiveEntities.Contains(rawId) && TryGet<T>(eid, out var comp))
                {
                    yield return (eid, comp);
                }
            }
        }

        public SectorGridComponent? GetCurrentSector() => GetSector(CurrentSectorKey);

        public SectorGridComponent? GetSector(string sectorKey)
        {
            if (!string.IsNullOrEmpty(sectorKey) && SectorEntitiesByKey.TryGetValue(sectorKey, out ulong id))
            {
                return Get<SectorGridComponent>(new EntityId(id));
            }
            return null;
        }

        public FactionComponent? GetFaction(FactionID id)
        {
            if (FactionEntitiesById.TryGetValue(id, out ulong rawId))
            {
                return Get<FactionComponent>(new EntityId(rawId));
            }
            return null;
        }

        public GridCoord GetPlayerPosition()
        {
            return Get<PositionComponent>(PlayerEntity)?.Coord ?? new GridCoord(15, 15);
        }

        /// <summary>
        /// Rebuilds all runtime lookup indices & sector grids after JSON deserialization.
        /// </summary>
        public void RebuildIndices()
        {
            _spatialIndex.Clear();
            _ownerIndex.Clear();
            EntitiesByExternalId.Clear();
            SectorEntitiesByKey.Clear();
            FactionEntitiesById.Clear();

            foreach (var (id, pos) in GetAll<PositionComponent>())
            {
                AddSpatialIndex(id.Value, pos.Coord);
            }

            foreach (var (id, owner) in GetAll<OwnerComponent>())
            {
                AddOwnerIndex(owner.OwnerEntityId, id.Value);
            }

            foreach (var (id, cult) in GetAll<CultivatorComponent>())
            {
                if (!string.IsNullOrEmpty(cult.ExternalId))
                {
                    EntitiesByExternalId[cult.ExternalId] = id.Value;
                }
            }

            foreach (var (id, sector) in GetAll<SectorGridComponent>())
            {
                if (!string.IsNullOrEmpty(sector.SectorKey))
                {
                    SectorEntitiesByKey[sector.SectorKey] = id.Value;
                    sector.HydrateTiles();
                }
            }

            foreach (var (id, faction) in GetAll<FactionComponent>())
            {
                FactionEntitiesById[faction.ID] = id.Value;
            }
        }

        private static string ComponentKey<T>() => typeof(T).FullName ?? typeof(T).Name;

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
                if (set.Count == 0) _spatialIndex.Remove(coord);
            }
        }

        private void AddOwnerIndex(ulong ownerId, ulong itemId)
        {
            if (!_ownerIndex.TryGetValue(ownerId, out var set))
            {
                set = new HashSet<ulong>();
                _ownerIndex[ownerId] = set;
            }
            set.Add(itemId);
        }

        private void RemoveOwnerIndex(ulong ownerId, ulong itemId)
        {
            if (_ownerIndex.TryGetValue(ownerId, out var set))
            {
                set.Remove(itemId);
                if (set.Count == 0) _ownerIndex.Remove(ownerId);
            }
        }
    }
}
