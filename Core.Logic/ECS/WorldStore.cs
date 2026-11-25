using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TaiwuGu.Core;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Restricts polymorphic JSON deserialization strictly to Core.Logic / allowlisted core runtime types.
    /// </summary>
    public sealed class KnownComponentsBinder : ISerializationBinder
    {
        private static readonly DefaultSerializationBinder s_Default = new();

        private static readonly HashSet<string> s_AllowedSystemTypes = new(StringComparer.Ordinal)
        {
            "System.Collections.Generic.List",
            "System.Collections.Generic.Dictionary",
            "System.Collections.Generic.HashSet",
            "System.Collections.Generic.Queue",
            "System.Collections.Generic.KeyValuePair",
            "System.String",
            "System.Int32",
            "System.UInt32",
            "System.Int64",
            "System.UInt64",
            "System.Single",
            "System.Double",
            "System.Decimal",
            "System.Boolean",
            "System.Byte",
            "System.SByte",
            "System.Int16",
            "System.UInt16",
            "System.Char",
            "System.Object",
            "System.DateTime",
            "System.TimeSpan",
            "System.Guid",
            "System.Nullable",
            "System.ValueTuple"
        };

        public Type BindToType(string? assemblyName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName) || !IsAllowedTypeName(typeName))
            {
                throw new JsonSerializationException($"Disallowed polymorphic type in save payload: '{typeName}'.");
            }

            return s_Default.BindToType(assemblyName, typeName);
        }

        private static bool IsAllowedTypeName(string typeName)
        {
            string trimmed = typeName.Trim();

            // Strip assembly qualification if present at top level (outside generic brackets)
            int bracketStart = trimmed.IndexOf('[');
            string head = bracketStart >= 0 ? trimmed.Substring(0, bracketStart) : trimmed;
            int commaIdx = head.IndexOf(',');
            if (commaIdx >= 0) head = head.Substring(0, commaIdx);
            head = head.Trim();

            // Handle array suffix (e.g., System.Int32[] or TaiwuGu.Map.TileNode[])
            while (head.EndsWith("[]", StringComparison.Ordinal))
            {
                head = head.Substring(0, head.Length - 2);
            }

            // Strip generic arity suffix (e.g., List`1 -> List)
            int tickIdx = head.IndexOf('`');
            string baseName = tickIdx >= 0 ? head.Substring(0, tickIdx) : head;

            bool headAllowed =
                baseName.StartsWith("TaiwuGu.", StringComparison.Ordinal) ||
                s_AllowedSystemTypes.Contains(baseName);

            if (!headAllowed) return false;

            // If there are generic arguments (e.g., [[TypeA, AsmA],[TypeB, AsmB]]), validate every nested type argument
            if (bracketStart >= 0 && bracketStart < trimmed.Length - 1 && trimmed[bracketStart + 1] == '[')
            {
                int depth = 0;
                int argStart = -1;
                for (int i = bracketStart + 1; i < trimmed.Length; i++)
                {
                    char c = trimmed[i];
                    if (c == '[')
                    {
                        if (depth == 0) argStart = i + 1;
                        depth++;
                    }
                    else if (c == ']')
                    {
                        depth--;
                        if (depth == 0 && argStart >= 0)
                        {
                            string arg = trimmed.Substring(argStart, i - argStart);
                            if (!IsAllowedTypeName(arg)) return false;
                            argStart = -1;
                        }
                        else if (depth < 0)
                        {
                            break;
                        }
                    }
                }
            }

            return true;
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
        private readonly Dictionary<(string SectorKey, GridCoord Coord), HashSet<ulong>> _spatialIndex = new();

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
            if (pos != null)
            {
                string sKey = string.IsNullOrEmpty(pos.SectorKey) ? CurrentSectorKey : pos.SectorKey;
                RemoveSpatialIndex(id.Value, sKey, pos.Coord);
            }

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
                    string oldSector = string.IsNullOrEmpty(oldPos.SectorKey) ? CurrentSectorKey : oldPos.SectorKey;
                    RemoveSpatialIndex(id.Value, oldSector, oldPos.Coord);
                }
                string newSector = string.IsNullOrEmpty(newPos.SectorKey) ? CurrentSectorKey : newPos.SectorKey;
                AddSpatialIndex(id.Value, newSector, newPos.Coord);
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
                    string sKey = string.IsNullOrEmpty(pos.SectorKey) ? CurrentSectorKey : pos.SectorKey;
                    RemoveSpatialIndex(id.Value, sKey, pos.Coord);
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
                string sKey = sectorKey ?? CurrentSectorKey;
                Set(id, new PositionComponent(newCoord, sKey));
            }
            else
            {
                string oldSector = string.IsNullOrEmpty(pos.SectorKey) ? CurrentSectorKey : pos.SectorKey;
                string newSector = sectorKey ?? oldSector;
                RemoveSpatialIndex(id.Value, oldSector, pos.Coord);
                pos.Coord = newCoord;
                pos.SectorKey = newSector;
                AddSpatialIndex(id.Value, newSector, newCoord);
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
            string sKey = sectorKeyFilter ?? CurrentSectorKey;
            if (_spatialIndex.TryGetValue((sKey, coord), out var set))
            {
                foreach (var rawId in set)
                {
                    yield return new EntityId(rawId);
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
                string sKey = string.IsNullOrEmpty(pos.SectorKey) ? CurrentSectorKey : pos.SectorKey;
                AddSpatialIndex(id.Value, sKey, pos.Coord);
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
                    if (string.Equals(sector.SectorKey, CurrentSectorKey, StringComparison.Ordinal))
                    {
                        sector.HydrateTiles();
                    }
                }
            }

            foreach (var (id, faction) in GetAll<FactionComponent>())
            {
                FactionEntitiesById[faction.ID] = id.Value;
            }
        }

        private static string ComponentKey<T>() => typeof(T).FullName ?? typeof(T).Name;

        private void AddSpatialIndex(ulong id, string sectorKey, GridCoord coord)
        {
            var key = (sectorKey ?? CurrentSectorKey, coord);
            if (!_spatialIndex.TryGetValue(key, out var set))
            {
                set = new HashSet<ulong>();
                _spatialIndex[key] = set;
            }
            set.Add(id);
        }

        private void RemoveSpatialIndex(ulong id, string sectorKey, GridCoord coord)
        {
            var key = (sectorKey ?? CurrentSectorKey, coord);
            if (_spatialIndex.TryGetValue(key, out var set))
            {
                set.Remove(id);
                if (set.Count == 0) _spatialIndex.Remove(key);
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
