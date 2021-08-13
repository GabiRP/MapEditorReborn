﻿namespace MapEditorReborn
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using API;
    using Exiled.API.Enums;
    using Exiled.API.Extensions;
    using Exiled.API.Features;
    using Exiled.CustomItems.API.Features;
    using Exiled.Loader;
    using Interactables.Interobjects;
    using Interactables.Interobjects.DoorUtils;
    using MEC;
    using Mirror;
    using RemoteAdmin;
    using UnityEngine;

    using Object = UnityEngine.Object;
    using Random = UnityEngine.Random;

    /// <summary>
    /// Contains mostly methods for spawning objects, saving/loading maps.
    /// </summary>
    public partial class Handler
    {
        #region Map Schematic Methods

        /// <summary>
        /// Loads the <see cref="MapSchematic"/> map.
        /// It also may be used for reloading the map.
        /// </summary>
        /// <param name="map"><see cref="MapSchematic"/> to load.</param>
        public static void LoadMap(MapSchematic map)
        {
            Log.Debug("Trying to load the map...", Config.Debug);

            foreach (GameObject spawnedObj in SpawnedObjects)
            {
                // NetworkServer.Destroy(spawnedObj) doesn't call OnDestroy methods in components for some reason.
                Object.Destroy(spawnedObj);
            }

            SpawnedObjects.Clear();

            foreach (GameObject indicator in Indicators.Keys)
            {
                NetworkServer.Destroy(indicator);
            }

            Indicators.Clear();

            Log.Debug("Destroyed all map's GameObjects and indicators.", Config.Debug);

            if (map == null)
            {
                Log.Debug("Map is null. Returning...", Config.Debug);
                return;
            }

            // Map.Rooms is null at this time, so this delay is required.
            Timing.CallDelayed(0.01f, () =>
            {
                // This MUST be executed first. If the default spawnpoins were destroyed I only have a brief period of time to replace them with a new ones.
                foreach (PlayerSpawnPointObject playerSpawnPoint in map.PlayerSpawnPoints)
                {
                    Log.Debug($"Trying to spawn a player spawn point at {playerSpawnPoint.Position}...", Config.Debug);
                    SpawnPlayerSpawnPoint(playerSpawnPoint);
                }

                if (map.PlayerSpawnPoints.Count > 0)
                    Log.Debug("All player spawn points have been spawned!", Config.Debug);

                foreach (DoorObject door in map.Doors)
                {
                    Log.Debug($"Trying to spawn door at {door.Position}...", Config.Debug);
                    SpawnDoor(door);
                }

                if (map.Doors.Count > 0)
                    Log.Debug("All doors have been successfully spawned!", Config.Debug);

                foreach (WorkStationObject workstation in map.WorkStations)
                {
                    Log.Debug($"Spawning workstation at {workstation.Position}...", Config.Debug);
                    SpawnWorkStation(workstation);
                }

                if (map.WorkStations.Count > 0)
                    Log.Debug("All workstations have been successfully spawned!", Config.Debug);

                foreach (ItemSpawnPointObject itemSpawnPoint in map.ItemSpawnPoints)
                {
                    Log.Debug($"Trying to spawn a item spawn point at {itemSpawnPoint.Position}...", Config.Debug);
                    SpawnItemSpawnPoint(itemSpawnPoint);
                }

                if (map.ItemSpawnPoints.Count > 0)
                    Log.Debug("All item spawn points have been spawned!", Config.Debug);

                foreach (RagdollSpawnPointObject ragdollSpawnPoint in map.RagdollSpawnPoints)
                {
                    Log.Debug($"Trying to spawn a ragdoll spawn point at {ragdollSpawnPoint.Position}...", Config.Debug);
                    SpawnRagdollSpawnPoint(ragdollSpawnPoint);
                }

                if (map.RagdollSpawnPoints.Count > 0)
                    Log.Debug("All ragdoll spawn points have been spawned!", Config.Debug);

                Log.Debug("All GameObject have been spawned and the MapSchematic has been fully loaded!", Config.Debug);
            });
        }

        /// <summary>
        /// Saves the map to a file.
        /// </summary>
        /// <param name="name">The name of the map.</param>
        public static void SaveMap(string name)
        {
            Log.Debug("Trying to save the map...", Config.Debug);

            MapSchematic map = new MapSchematic
            {
                Name = name,
                RemoveDefaultSpawnPoints = CurrentLoadedMap?.RemoveDefaultSpawnPoints ?? default,
                RoleNames = CurrentLoadedMap?.RoleNames ?? default,
            };

            Log.Debug($"Map name set to \"{map.Name}\"", Config.Debug);

            foreach (GameObject gameObject in SpawnedObjects)
            {
                Log.Debug($"Trying to save GameObject at {gameObject.transform.position}...", Config.Debug);

                Room room = Map.FindParentRoom(gameObject);
                Vector3 relativePosition = room.Type == RoomType.Surface ? gameObject.transform.position : room.transform.InverseTransformPoint(gameObject.transform.position);
                Vector3 relativeRotation = room.Type == RoomType.Surface ? gameObject.transform.eulerAngles : (gameObject.transform.rotation * room.transform.rotation).eulerAngles;

                switch (gameObject.name)
                {
                    case "LCZ BreakableDoor(Clone)":
                    case "HCZ BreakableDoor(Clone)":
                    case "EZ BreakableDoor(Clone)":
                        {
                            DoorVariant door = gameObject.GetComponent<DoorVariant>();
                            BreakableDoor breakableDoor = door as BreakableDoor;
                            DoorObjectComponent doorObjectComponent = door.GetComponent<DoorObjectComponent>();

                            map.Doors.Add(new DoorObject(
                                door.GetDoorTypeByName(),
                                relativePosition,
                                relativeRotation,
                                door.transform.localScale,
                                room.Type,
                                door.NetworkTargetState,
                                door.NetworkActiveLocks == 64,
                                door.RequiredPermissions.RequiredPermissions,
                                breakableDoor._ignoredDamageSources,
                                breakableDoor._maxHealth,
                                doorObjectComponent.OpenOnWarheadActivation));

                            break;
                        }

                    case "Work Station(Clone)":
                        {
                            map.WorkStations.Add(new WorkStationObject(
                                relativePosition,
                                relativeRotation,
                                gameObject.transform.localScale,
                                room.Type));

                            break;
                        }

                    case "PlayerSpawnPointObject(Clone)":
                        {
                            map.PlayerSpawnPoints.Add(new PlayerSpawnPointObject(
                                gameObject.tag.ConvertToRoleType(),
                                relativePosition,
                                room.Type));

                            break;
                        }

                    case "ItemSpawnPointObject(Clone)":
                        {
                            ItemSpawnPointComponent itemSpawnPointComponent = gameObject.GetComponent<ItemSpawnPointComponent>();

                            map.ItemSpawnPoints.Add(new ItemSpawnPointObject(
                                itemSpawnPointComponent.ItemName,
                                relativePosition,
                                relativeRotation,
                                room.Type));

                            break;
                        }

                    case "RagdollSpawnPointObject(Clone)":
                        {
                            RagdollObjectComponent ragdollObjectComponent = gameObject.GetComponent<RagdollObjectComponent>();
                            string ragdollName = string.Empty;

                            if (CurrentLoadedMap != null && CurrentLoadedMap.RoleNames.TryGetValue(ragdollObjectComponent.RagdollRoleType, out List<string> list) && !list.Contains(ragdollObjectComponent.RagdollName))
                            {
                                ragdollName = ragdollObjectComponent.RagdollName;
                            }

                            map.RagdollSpawnPoints.Add(new RagdollSpawnPointObject(
                                ragdollName,
                                ragdollObjectComponent.RagdollRoleType,
                                ragdollObjectComponent.RagdollDamageType,
                                relativePosition,
                                relativeRotation,
                                room.Type));

                            break;
                        }
                }
            }

            string path = Path.Combine(MapEditorReborn.PluginDir, $"{map.Name}.yml");

            Log.Debug($"Path to file set to: {path}", Config.Debug);

            bool prevValue = Config.EnableFileSystemWatcher;
            if (prevValue)
                Config.EnableFileSystemWatcher = false;

            Log.Debug("Trying to serialize the MapSchematic...", Config.Debug);

            File.WriteAllText(path, Loader.Serializer.Serialize(map));

            Log.Debug("MapSchematic has been successfully saved to a file!", Config.Debug);

            Timing.CallDelayed(1f, () => Config.EnableFileSystemWatcher = prevValue);
        }

        /// <summary>
        /// Gets the <see cref="MapSchematic"/> by it's name.
        /// </summary>
        /// <param name="mapName">The name of the map.</param>
        /// <returns><see cref="MapSchematic"/> if the file with the map was found, otherwise <see langword="null"/>.</returns>
        public static MapSchematic GetMapByName(string mapName)
        {
            string path = Path.Combine(MapEditorReborn.PluginDir, $"{mapName}.yml");

            if (!File.Exists(path))
                return null;

            return Loader.Deserializer.Deserialize<MapSchematic>(File.ReadAllText(path));
        }

        #endregion

        #region Spawning Objects Methods

        /// <summary>
        /// Spawns a door.
        /// </summary>
        /// <param name="door">The <see cref="DoorObject"/> which is used to spawn a door.</param>
        /// <returns><see cref="GameObject"/> of the spawned door.</returns>
        public static GameObject SpawnDoor(DoorObject door)
        {
            Room room = GetRandomRoom(door.RoomType);

            GameObject gameObject = Object.Instantiate(door.DoorType.GetDoorObjectByType(), GetRelativePosition(door.Position, room), GetRelativeRotation(door.Rotation, room));
            gameObject.transform.localScale = door.Scale;

            DoorVariant doorVaraintComponent = gameObject.GetComponent<DoorVariant>();
            doorVaraintComponent.NetworkTargetState = door.IsOpen;
            doorVaraintComponent.ServerChangeLock(DoorLockReason.SpecialDoorFeature, door.IsLocked);
            doorVaraintComponent.RequiredPermissions.RequiredPermissions = door.KeycardPermissions;

            BreakableDoor breakableDoor = doorVaraintComponent as BreakableDoor;
            breakableDoor._ignoredDamageSources = door.IgnoredDamageSources;
            breakableDoor._maxHealth = door.DoorHealth;

            DoorObjectComponent doorObjectComponent = gameObject.AddComponent<DoorObjectComponent>();
            doorObjectComponent.OpenOnWarheadActivation = door.OpenOnWarheadActivation;

            SpawnedObjects.Add(gameObject);

            NetworkServer.Spawn(gameObject);

            return gameObject;
        }

        /// <summary>
        /// Spawns a workstation.
        /// </summary>
        /// <param name="workStation">The <see cref="WorkStationObject"/> to spawn.</param>
        /// <returns><see cref="GameObject"/> of the spawned workstation.</returns>
        public static GameObject SpawnWorkStation(WorkStationObject workStation)
        {
            Room room = GetRandomRoom(workStation.RoomType);

            GameObject gameObject = Object.Instantiate(WorkstationObj, GetRelativePosition(workStation.Position, room), GetRelativeRotation(workStation.Rotation, room));
            gameObject.transform.localScale = workStation.Scale;

            gameObject.AddComponent<WorkStation>();

            SpawnedObjects.Add(gameObject);

            NetworkServer.Spawn(gameObject);

            return gameObject;
        }

        /// <summary>
        /// Spawns a ItemSpawnPoint.
        /// </summary>
        /// <param name="itemSpawnPoint">The <see cref="ItemSpawnPointObject"/> to spawn.</param>
        /// <returns><see cref="GameObject"/> of the spawned ItemSpawnPoint.</returns>
        public static GameObject SpawnItemSpawnPoint(ItemSpawnPointObject itemSpawnPoint)
        {
            Room room = GetRandomRoom(itemSpawnPoint.RoomType);

            GameObject gameObject = Object.Instantiate(ItemSpawnPointObj, GetRelativePosition(itemSpawnPoint.Position, room), GetRelativeRotation(itemSpawnPoint.Rotation, room));

            ItemSpawnPointComponent itemSpawnPointComponent = gameObject.AddComponent<ItemSpawnPointComponent>();
            itemSpawnPointComponent.ItemName = itemSpawnPoint.Item;
            itemSpawnPointComponent.SpawnChance = itemSpawnPoint.SpawnChance;

            SpawnedObjects.Add(gameObject);

            NetworkServer.Spawn(gameObject);

            return gameObject;
        }

        /// <summary>
        /// Spawns a PlayerSpawnPoint.
        /// </summary>
        /// <param name="playerSpawnPoint">The <see cref="PlayerSpawnPointObject"/> to spawn.</param>
        /// <returns><see cref="GameObject"/> of the spawned PlayerSpawnPoint.</returns>
        public static GameObject SpawnPlayerSpawnPoint(PlayerSpawnPointObject playerSpawnPoint)
        {
            Room room = GetRandomRoom(playerSpawnPoint.RoomType);

            GameObject gameObject = Object.Instantiate(PlayerSpawnPointObj, GetRelativePosition(playerSpawnPoint.Position, room), Quaternion.identity);
            gameObject.tag = playerSpawnPoint.RoleType.ConvertToSpawnPointTag();

            SpawnedObjects.Add(gameObject);

            NetworkServer.Spawn(gameObject);

            return gameObject;
        }

        /// <summary>
        /// Spawns a RagdollSpawnPoint.
        /// </summary>
        /// <param name="ragdollSpawnPoint">The <see cref="RagdollSpawnPointObject"/> to spawn.</param>
        /// <returns><see cref="GameObject"/> of the spawned RagdollSpawnPoint.</returns>
        public static GameObject SpawnRagdollSpawnPoint(RagdollSpawnPointObject ragdollSpawnPoint)
        {
            Room room = GetRandomRoom(ragdollSpawnPoint.RoomType);

            GameObject gameObject = Object.Instantiate(RagdollSpawnPointObj, GetRelativePosition(ragdollSpawnPoint.Position, room), GetRelativeRotation(ragdollSpawnPoint.Rotation, room));

            RagdollObjectComponent ragdollObjectComponent = gameObject.AddComponent<RagdollObjectComponent>();

            if (string.IsNullOrEmpty(ragdollObjectComponent.RagdollName) && CurrentLoadedMap.RoleNames.TryGetValue(ragdollSpawnPoint.RoleType, out List<string> ragdollNames))
            {
                ragdollObjectComponent.RagdollName = ragdollNames[Random.Range(0, ragdollNames.Count)];
            }
            else
            {
                ragdollObjectComponent.RagdollName = ragdollSpawnPoint.Name;
            }

            ragdollObjectComponent.RagdollRoleType = ragdollSpawnPoint.RoleType;
            ragdollObjectComponent.RagdollDamageType = ragdollSpawnPoint.DamageType;

            SpawnedObjects.Add(gameObject);

            NetworkServer.Spawn(gameObject);

            return gameObject;
        }

        /// <summary>
        /// Spawns a general <see cref="GameObject"/>.
        /// Used by the ToolGun.
        /// </summary>
        /// <param name="position">The postition of the spawned object.</param>
        /// <param name="mode">The current <see cref="ToolGunMode"/>.</param>
        /// <returns>The spawned <see cref="GameObject"/>.</returns>
        public static GameObject SpawnObject(Vector3 position, ToolGunMode mode)
        {
            GameObject gameObject = null;

            if (position.y < 900f && (mode == ToolGunMode.LczDoor || mode == ToolGunMode.HczDoor || mode == ToolGunMode.EzDoor))
            {
                gameObject = Object.Instantiate(mode.GetObjectByMode(), new Vector3(190f, 994f, -97f), Quaternion.identity);

                Timing.CallDelayed(5f, () =>
                {
                    NetworkServer.UnSpawn(gameObject);
                    gameObject.transform.position = position;
                    NetworkServer.Spawn(gameObject);
                });
            }
            else
            {
                gameObject = Object.Instantiate(mode.GetObjectByMode(), position, Quaternion.identity);
            }

            switch (mode)
            {
                case ToolGunMode.LczDoor:
                case ToolGunMode.HczDoor:
                case ToolGunMode.EzDoor:
                    {
                        gameObject.AddComponent<DoorObjectComponent>();
                        break;
                    }

                case ToolGunMode.WorkStation:
                    {
                        gameObject.AddComponent<WorkStation>();
                        break;
                    }

                case ToolGunMode.ItemSpawnPoint:
                    {
                        gameObject.AddComponent<ItemSpawnPointComponent>();
                        break;
                    }

                case ToolGunMode.PlayerSpawnPoint:
                    {
                        gameObject.tag = "SP_173";
                        gameObject.transform.position += Vector3.up * 0.25f;
                        break;
                    }

                case ToolGunMode.RagdollSpawnPoint:
                    {
                        gameObject.transform.position += Vector3.up * 1.5f;
                        gameObject.AddComponent<RagdollObjectComponent>();
                        break;
                    }
            }

            SpawnedObjects.Add(gameObject);

            NetworkServer.Spawn(gameObject);

            return gameObject;
        }

        /// <summary>
        /// Spawns a copy of selected object by a ToolGun.
        /// </summary>
        /// <param name="position">Position of spawned property object.</param>
        /// <param name="prefab">The <see cref="GameObject"/> from which the copy will be spawned.</param>
        public static void SpawnPropertyObject(Vector3 position, GameObject prefab)
        {
            GameObject gameObject = Object.Instantiate(prefab, position, prefab.transform.rotation);

            gameObject.name = gameObject.name.Replace("(Clone)(Clone)(Clone)", "(Clone)");

            SpawnedObjects.Add(gameObject);

            NetworkServer.Spawn(gameObject);

            Log.Debug(gameObject.name, Config.Debug);
        }

        #endregion

        #region Getting Relative Stuff Methods

        /// <summary>
        /// Gets a random <see cref="Room"/> from the <see cref="RoomType"/>.
        /// </summary>
        /// <param name="type">The <see cref="RoomType"/> from which the room should be choosen.</param>
        /// <returns>A random <see cref="Room"/> that has <see cref="Room.Type"/> of the argument.</returns>
        public static Room GetRandomRoom(RoomType type)
        {
            List<Room> validRooms = Map.Rooms.Where(x => x.Type == type).ToList();

            return validRooms[Random.Range(0, validRooms.Count)];
        }

        /// <summary>
        /// Gets a position relative to the <see cref="Room"/>.
        /// </summary>
        /// <param name="position">The object position.</param>
        /// <param name="room">The <see cref="Room"/> whose <see cref="Transform"/> will be used.</param>
        /// <returns>Global position relative to the <see cref="Room"/>. If the <paramref name="type"/> is equal to <see cref="RoomType.Surface"/> the <paramref name="position"/> will be retured with no changes.</returns>
        public static Vector3 GetRelativePosition(Vector3 position, Room room)
        {
            if (room.Type == RoomType.Surface)
            {
                return position;
            }
            else
            {
                return room.transform.TransformPoint(position);
            }
        }

        /// <summary>
        /// Gets a rotation relative to the <see cref="Room"/>.
        /// </summary>
        /// <param name="rotation">The object rotation.</param>
        /// <param name="room">The <see cref="Room"/> whose <see cref="Transform"/> will be used.</param>
        /// <returns>Global rotation relative to the <see cref="Room"/>. If the <paramref name="roomType"/> is equal to <see cref="RoomType.Surface"/> the <paramref name="rotation"/> will be retured with no changes.</returns>
        public static Quaternion GetRelativeRotation(Vector3 rotation, Room room)
        {
            if (room.Type == RoomType.Surface)
            {
                return Quaternion.Euler(rotation);
            }
            else
            {
                return room.transform.rotation * Quaternion.Euler(rotation);
            }
        }

        #endregion

        #region Spawning Indicators

        /// <summary>
        /// Spawns a pickup indicator used for showing where ItemSpawnPoint is.
        /// </summary>
        /// <param name="itemSpawnPoint">The <see cref="GameObject"/> of the ItemSpawnPoint which indicator should indicate.</param>
        public static void SpawnPickupIndicator(GameObject itemSpawnPoint) => SpawnPickupIndicator(itemSpawnPoint.transform.position, itemSpawnPoint.transform.rotation, itemSpawnPoint.GetComponent<ItemSpawnPointComponent>().ItemName, itemSpawnPoint);

        /// <inheritdoc cref="SpawnPickupIndicator(GameObject)"/>
        public static void SpawnPickupIndicator(Vector3 position, Quaternion rotation, string name, GameObject callingItemSpawnPointObject)
        {
            if (Indicators.Values.Contains(callingItemSpawnPointObject))
            {
                GameObject pickupIndicator = Indicators.First(x => x.Value == callingItemSpawnPointObject).Key;

                pickupIndicator.gameObject.transform.position = position;
                pickupIndicator.gameObject.transform.rotation = rotation;
                return;
            }

            ItemType parsedItem;

            if (CustomItem.TryGet(name, out CustomItem custom))
            {
                parsedItem = custom.Type;
            }
            else
            {
                parsedItem = (ItemType)Enum.Parse(typeof(ItemType), name, true);
            }

            GameObject pickupGameObject = Item.Spawn(parsedItem, parsedItem.GetDefaultDurability(), position + (Vector3.up * 0.1f), rotation).gameObject;
            NetworkServer.UnSpawn(pickupGameObject);

            Rigidbody rigidbody = pickupGameObject.GetComponent<Rigidbody>();
            rigidbody.isKinematic = false;
            rigidbody.useGravity = false;
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.freezeRotation = true;
            rigidbody.mass = 100000;

            if (parsedItem.IsWeapon())
                pickupGameObject.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

            pickupGameObject.AddComponent<ItemSpiningComponent>();

            Indicators.Add(pickupGameObject, callingItemSpawnPointObject);

            NetworkServer.Spawn(pickupGameObject);
        }

        /// <summary>
        /// Spawns a dummy (NPC) indicator used for showning where PlayerSpawnPoint and RagdollSpawnPoint are.
        /// </summary>
        /// <param name="callingGameObject">The <see cref="GameObject"/> of PlayerSpawnPoint or RagdollSpawnPoint which indicator should indicate.</param>
        public static void SpawnDummyIndicator(GameObject callingGameObject)
        {
            Log.Error(callingGameObject.name);

            if (callingGameObject.name == "PlayerSpawnPointObject(Clone)")
            {
                SpawnDummyIndicator(callingGameObject.transform.position, callingGameObject.tag.ConvertToRoleType(), callingGameObject);
            }
            else if (callingGameObject.name == "RagdollSpawnPointObject(Clone)")
            {
                SpawnDummyIndicator(callingGameObject.transform.position, callingGameObject.GetComponent<RagdollObjectComponent>().RagdollRoleType, callingGameObject);
            }
        }

        /// <inheritdoc cref="SpawnDummyIndicator(GameObject)"/>
        public static void SpawnDummyIndicator(Vector3 posistion, RoleType type, GameObject callingGameObject)
        {
            if (Indicators.Values.Contains(callingGameObject))
            {
                ReferenceHub dummyIndicator = Indicators.First(x => x.Value == callingGameObject).Key.GetComponent<ReferenceHub>();

                try
                {
                    dummyIndicator.transform.position = posistion;
                    dummyIndicator.playerMovementSync.OverridePosition(posistion, 0f);
                }
                catch
                {
                    return;
                }

                return;
            }

            GameObject dummyObject = Object.Instantiate(NetworkManager.singleton.spawnPrefabs.FirstOrDefault(p => p.gameObject.name == "Player"));

            dummyObject.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            if (callingGameObject.name.Contains("Ragdoll"))
                dummyObject.transform.localScale *= -1;

            dummyObject.transform.position = posistion;

            QueryProcessor processor = dummyObject.GetComponent<QueryProcessor>();

            processor.NetworkPlayerId = QueryProcessor._idIterator++;
            processor._ipAddress = "127.0.0.WAN";

            CharacterClassManager ccm = dummyObject.GetComponent<CharacterClassManager>();
            ccm.CurClass = type;
            ccm.GodMode = true;

            string dummyNickname;

            switch (type)
            {
                case RoleType.NtfCadet:
                    dummyNickname = "MTF";
                    break;

                case RoleType.Scp93953:
                    dummyNickname = "SCP939";
                    break;

                default:
                    dummyNickname = type.ToString();
                    break;
            }

            NicknameSync nicknameSync = dummyObject.GetComponent<NicknameSync>();

            if (callingGameObject.name.Contains("Ragdoll"))
            {
                nicknameSync.Network_myNickSync = "RAGDOLL SPAWNPOINT";
                nicknameSync.CustomPlayerInfo = $"{dummyNickname} RAGDOLL\nSPAWN POINT";
            }
            else
            {
                nicknameSync.Network_myNickSync = "PLAYER SPAWNPOINT";
                nicknameSync.CustomPlayerInfo = $"{dummyNickname}\nSPAWN POINT";
            }

            nicknameSync.ShownPlayerInfo &= ~PlayerInfoArea.Nickname;
            nicknameSync.ShownPlayerInfo &= ~PlayerInfoArea.Role;

            NetworkServer.Spawn(dummyObject);
            PlayerManager.players.Add(dummyObject);
            Indicators.Add(dummyObject, callingGameObject);

            ReferenceHub rh = dummyObject.GetComponent<ReferenceHub>();
            Timing.CallDelayed(0.5f, () =>
            {
                dummyObject.AddComponent<DummySpiningComponent>().Hub = rh;
                rh.playerMovementSync.OverridePosition(posistion, 0f);
            });
        }

        #endregion
    }
}