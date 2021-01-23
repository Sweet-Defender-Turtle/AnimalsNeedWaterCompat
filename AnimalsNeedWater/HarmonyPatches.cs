﻿using Harmony;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Tools;
using System;
using xTile.Layers;
using xTile.Tiles;
// ReSharper disable InconsistentNaming

namespace AnimalsNeedWater
{
    internal class HarmonyPatches
    {
        /// <summary> Patch for the FarmAnimal.dayUpdate method. </summary>
        [HarmonyPriority(500)]
        public static void AnimalDayUpdate(ref FarmAnimal __instance, ref GameLocation environtment)
        {
            if (__instance.home != null &&
                !((AnimalHouse) __instance.home.indoors.Value).animals.ContainsKey(__instance.myID.Value) &&
                environtment is Farm && !__instance.home.animalDoorOpen.Value) return;
            
            if (__instance.home != null && __instance.home.nameOfIndoors.ToLower().Contains("coop"))
            {
                // check whether CoopsWithWateredTrough contains the coop the animal lives in and whether it was able to drink outside or not
                if (ModData.CoopsWithWateredTrough.Contains(__instance.home.nameOfIndoors.ToLower()) || ModData.FullAnimals.Contains(__instance.displayName))
                {
                    // increase friendship points if any of the conditions above is met
                    __instance.friendshipTowardFarmer.Value += Math.Abs(ModEntry.Instance.Config.FriendshipPointsForWateredTrough);
                }
            }
            else if (__instance.home != null && __instance.home.nameOfIndoors.ToLower().Contains("barn"))
            {
                // check whether BarnsWithWateredTrough contains the coop the animal lives in and whether it was able to drink outside or not
                if (ModData.BarnsWithWateredTrough.Contains(__instance.home.nameOfIndoors.ToLower()) || ModData.FullAnimals.Contains(__instance.displayName))
                {
                    // increase friendship points if any of the conditions above is met
                    __instance.friendshipTowardFarmer.Value += Math.Abs(ModEntry.Instance.Config.FriendshipPointsForWateredTrough);
                }
            }
        }

        /// <summary> Patch for the FarmAnimal.behaviors method. </summary>
        [HarmonyPriority(600)]
        public static bool AnimalBehaviors(ref bool __result, ref FarmAnimal __instance, ref GameTime time, ref GameLocation location)
        {
            // return false if the animal's home is null
            if (__instance.home == null)
                __result = false;

            if (!Game1.IsClient)
            {
                if (__instance.controller != null)
                    __result = true;
                if (location.IsOutdoors && !ModData.FullAnimals.Contains(__instance.displayName) && __instance.controller == null && (Game1.random.NextDouble() < 0.001 && FarmAnimal.NumPathfindingThisTick < FarmAnimal.MaxPathfindingPerTick) && ModEntry.Instance.Config.AnimalsCanDrinkOutside)
                {
                    // pathfind to the closest water tile
                    ++FarmAnimal.NumPathfindingThisTick;
                    __instance.controller = new PathFindController(__instance, location, WaterEndPointFunction, -1, false, BehaviorAfterFindingWater, 200, Point.Zero);
                }
            }

            return true;
        }

        /// <summary> Search for water tiles. </summary>
        public static bool WaterEndPointFunction(
          PathNode currentPoint,
          Point endPoint,
          GameLocation location,
          Character c)
        {
            if (!ModEntry.Instance.Config.AnimalsCanOnlyDrinkFromWaterBodies)
            {
                return location.CanRefillWateringCanOnTile(currentPoint.x - 1, currentPoint.y) || location.CanRefillWateringCanOnTile(currentPoint.x, currentPoint.y - 1) || location.CanRefillWateringCanOnTile(currentPoint.x, currentPoint.y + 1) || location.CanRefillWateringCanOnTile(currentPoint.x + 1, currentPoint.y);
            }

            return location.isOpenWater(currentPoint.x - 1, currentPoint.y) || location.isOpenWater(currentPoint.x, currentPoint.y - 1) || location.isOpenWater(currentPoint.x, currentPoint.y + 1) || location.isOpenWater(currentPoint.x + 1, currentPoint.y);
        }

        /// <summary> Animal behavior after finding water tile. </summary>
        public static void BehaviorAfterFindingWater(Character c, GameLocation environment)
        {
            // return if the animal is already on the list
            if (ModData.FullAnimals.Contains(c.displayName))
                return;

            // do the 'happy' emote and add the animal to the Full Animals list
            c.doEmote(32);
            ModData.FullAnimals.Add(c.displayName);
        }

        /// <summary> Patch for the AnimalHouse.performToolAction method. </summary>
        [HarmonyPriority(500)]
        public static bool AnimalHouseToolAction(ref AnimalHouse __instance, ref Tool t, ref int tileX, ref int tileY)
        {
            GameLocation gameLocation = Game1.currentLocation;

            if (t.BaseName != "Watering Can" || ((WateringCan) t).WaterLeft <= 0) return false;
            
            if (Game1.currentLocation.Name.ToLower().Contains("coop") && !ModData.CoopsWithWateredTrough.Contains(__instance.NameOrUniqueName.ToLower()))
            {
                if (__instance.getBuilding().nameOfIndoorsWithoutUnique.ToLower() == "coop")
                {
                    foreach (TroughTile troughTile in ModEntry.Instance.CurrentTroughPlacementProfile.coopTroughTiles)
                    {
                        if (troughTile.TileX != tileX || troughTile.TileY != tileY) continue;
                        
                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coopTroughTiles)
                        {
                            gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                        }

                        Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                        Layer frontLayer = gameLocation.map.GetLayer("Front");
                        TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coopTroughTiles)
                        {
                            if (tile.Layer.Equals("Buildings"))
                                buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            else if (tile.Layer.Equals("Front"))
                                frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                        }

                        ModData.CoopsWithWateredTrough.Add(__instance.NameOrUniqueName.ToLower());
                        ModEntry.Instance.ChangeCoopTexture(__instance.getBuilding(), false);

                        foreach (FarmAnimal animal in __instance.animals.Values)
                        {
                            if (ModEntry.Instance.Config.ShowLoveBubblesOverAnimalsWhenWateredTrough)
                            {
                                animal.doEmote(20);
                            }
                            animal.friendshipTowardFarmer.Value += Math.Abs(ModEntry.Instance.Config.AdditionalFriendshipPointsForWateredTroughWithAnimalsInsideBuilding);
                        }
                    }
                }
                else if (__instance.getBuilding().nameOfIndoorsWithoutUnique.ToLower() == "coop2")
                {
                    foreach (TroughTile troughTile in ModEntry.Instance.CurrentTroughPlacementProfile.coop2TroughTiles)
                    {
                        if (troughTile.TileX != tileX || troughTile.TileY != tileY) continue;
                        
                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop2TroughTiles)
                        {
                            gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                        }

                        Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                        Layer frontLayer = gameLocation.map.GetLayer("Front");
                        TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop2TroughTiles)
                        {
                            if (tile.Layer.Equals("Buildings"))
                                buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            else if (tile.Layer.Equals("Front"))
                                frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                        }

                        ModData.CoopsWithWateredTrough.Add(__instance.NameOrUniqueName.ToLower());
                        ModEntry.Instance.ChangeBigCoopTexture(__instance.getBuilding(), false);

                        foreach (FarmAnimal animal in __instance.animals.Values)
                        {
                            if (ModEntry.Instance.Config.ShowLoveBubblesOverAnimalsWhenWateredTrough)
                            {
                                animal.doEmote(20);
                            }
                            animal.friendshipTowardFarmer.Value += Math.Abs(ModEntry.Instance.Config.AdditionalFriendshipPointsForWateredTroughWithAnimalsInsideBuilding);
                        }
                    }
                }
                else if (__instance.getBuilding().nameOfIndoorsWithoutUnique.ToLower() == "coop3")
                {
                    foreach (TroughTile troughTile in ModEntry.Instance.CurrentTroughPlacementProfile.coop3TroughTiles)
                    {
                        if (troughTile.TileX != tileX || troughTile.TileY != tileY) continue;
                        
                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop3TroughTiles)
                        {
                            gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                        }

                        Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                        Layer frontLayer = gameLocation.map.GetLayer("Front");
                        TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop3TroughTiles)
                        {
                            if (tile.Layer.Equals("Buildings"))
                                buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            else if (tile.Layer.Equals("Front"))
                                frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                        }

                        ModData.CoopsWithWateredTrough.Add(__instance.NameOrUniqueName.ToLower());

                        foreach (FarmAnimal animal in __instance.animals.Values)
                        {
                            if (ModEntry.Instance.Config.ShowLoveBubblesOverAnimalsWhenWateredTrough)
                            {
                                animal.doEmote(20);
                            }
                            animal.friendshipTowardFarmer.Value += Math.Abs(ModEntry.Instance.Config.AdditionalFriendshipPointsForWateredTroughWithAnimalsInsideBuilding);
                        }
                    }
                }
            }
            else if (Game1.currentLocation.Name.ToLower().Contains("barn") && !ModData.BarnsWithWateredTrough.Contains(__instance.NameOrUniqueName.ToLower()))
            {
                if (__instance.getBuilding().nameOfIndoorsWithoutUnique.ToLower() == "barn")
                {
                    foreach (TroughTile troughTile in ModEntry.Instance.CurrentTroughPlacementProfile.barnTroughTiles)
                    {
                        if (troughTile.TileX != tileX || troughTile.TileY != tileY) continue;
                        
                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barnTroughTiles)
                        {
                            gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                        }

                        Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                        Layer frontLayer = gameLocation.map.GetLayer("Front");
                        TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barnTroughTiles)
                        {
                            if (tile.Layer.Equals("Buildings"))
                                buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            else if (tile.Layer.Equals("Front"))
                                frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                        }

                        ModData.BarnsWithWateredTrough.Add(__instance.NameOrUniqueName.ToLower());

                        foreach (FarmAnimal animal in __instance.animals.Values)
                        {
                            if (ModEntry.Instance.Config.ShowLoveBubblesOverAnimalsWhenWateredTrough)
                            {
                                animal.doEmote(20);
                            }
                            animal.friendshipTowardFarmer.Value += Math.Abs(ModEntry.Instance.Config.AdditionalFriendshipPointsForWateredTroughWithAnimalsInsideBuilding);
                        }
                    }
                }
                else if (__instance.getBuilding().nameOfIndoorsWithoutUnique.ToLower() == "barn2")
                {
                    foreach (TroughTile troughTile in ModEntry.Instance.CurrentTroughPlacementProfile.barn2TroughTiles)
                    {
                        if (troughTile.TileX != tileX || troughTile.TileY != tileY) continue;
                        
                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn2TroughTiles)
                        {
                            gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                        }

                        Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                        Layer frontLayer = gameLocation.map.GetLayer("Front");
                        TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn2TroughTiles)
                        {
                            if (tile.Layer.Equals("Buildings"))
                                buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            else if (tile.Layer.Equals("Front"))
                                frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                        }

                        ModData.BarnsWithWateredTrough.Add(__instance.NameOrUniqueName.ToLower());

                        foreach (FarmAnimal animal in __instance.animals.Values)
                        {
                            if (ModEntry.Instance.Config.ShowLoveBubblesOverAnimalsWhenWateredTrough)
                            {
                                animal.doEmote(20);
                            }
                            animal.friendshipTowardFarmer.Value += Math.Abs(ModEntry.Instance.Config.AdditionalFriendshipPointsForWateredTroughWithAnimalsInsideBuilding);
                        }
                    }
                }
                else if (__instance.getBuilding().nameOfIndoorsWithoutUnique.ToLower() == "barn3")
                {
                    foreach (TroughTile troughTile in ModEntry.Instance.CurrentTroughPlacementProfile.barn3TroughTiles)
                    {
                        if (troughTile.TileX != tileX || troughTile.TileY != tileY) continue;
                        
                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn3TroughTiles)
                        {
                            gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                        }

                        Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                        Layer frontLayer = gameLocation.map.GetLayer("Front");
                        TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                        foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn3TroughTiles)
                        {
                            if (tile.Layer.Equals("Buildings"))
                                buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            else if (tile.Layer.Equals("Front"))
                                frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                        }

                        ModData.BarnsWithWateredTrough.Add(__instance.NameOrUniqueName.ToLower());

                        foreach (FarmAnimal animal in __instance.animals.Values)
                        {
                            if (ModEntry.Instance.Config.ShowLoveBubblesOverAnimalsWhenWateredTrough)
                            {
                                animal.doEmote(20);
                            }
                            animal.friendshipTowardFarmer.Value += Math.Abs(ModEntry.Instance.Config.AdditionalFriendshipPointsForWateredTroughWithAnimalsInsideBuilding);
                        }
                    }
                }
            }

            return false;
        }

        /// <summary> Patch for the warpFarmer method. </summary>
        [HarmonyPriority(500)]
        public static void WarpFarmer(Game1 __instance, ref string locationName, ref int tileX, ref int tileY, ref int facingDirectionAfterWarp, ref bool isStructure)
        {
            string locationNameWithoutUnique = Game1.getLocationFromName(locationName, isStructure).Name;
            Building building = null;

            if (locationName.ToLower().Contains("coop") || locationName.ToLower().Contains("barn"))
                building = ((AnimalHouse)Game1.getLocationFromName(locationName)).getBuilding();

            if ((ModData.BarnsWithWateredTrough.Contains(locationName.ToLower()) || ModData.CoopsWithWateredTrough.Contains(locationName.ToLower())) && building != null)
            {
                if (locationNameWithoutUnique.Contains("Coop"))
                {
                    switch (building.nameOfIndoorsWithoutUnique.ToLower())
                    {
                        case "coop":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coopTroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coopTroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            }

                            break;
                        }
                        case "coop2":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop2TroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop2TroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            }

                            break;
                        }
                        case "coop3":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop3TroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop3TroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            }

                            break;
                        }
                    }
                }
                else if (locationNameWithoutUnique.Contains("Barn"))
                {
                    switch (building.nameOfIndoorsWithoutUnique.ToLower())
                    {
                        case "barn":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barnTroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barnTroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            }

                            break;
                        }
                        case "barn2":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn2TroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn2TroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            }

                            break;
                        }
                        case "barn3":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn3TroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn3TroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.FullTroughTilesheetIndex);
                            }

                            break;
                        }
                    }
                }
            }
            else if((!ModData.BarnsWithWateredTrough.Contains(locationName.ToLower()) || !ModData.CoopsWithWateredTrough.Contains(locationName.ToLower())) && building != null)
            {
                if (locationNameWithoutUnique.Contains("Coop"))
                {
                    switch (building.nameOfIndoorsWithoutUnique.ToLower())
                    {
                        case "coop":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coopTroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coopTroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                            }

                            break;
                        }
                        case "coop2":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop2TroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop2TroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                            }

                            break;
                        }
                        case "coop3":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop3TroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.coop3TroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                            }

                            break;
                        }
                    }
                }
                else if (locationNameWithoutUnique.Contains("Barn"))
                {
                    switch (building.nameOfIndoorsWithoutUnique.ToLower())
                    {
                        case "barn":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barnTroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barnTroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                            }

                            break;
                        }
                        case "barn2":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn2TroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn2TroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                            }

                            break;
                        }
                        case "barn3":
                        {
                            GameLocation gameLocation = building.indoors.Value;

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn3TroughTiles)
                            {
                                gameLocation.removeTile(tile.TileX, tile.TileY, tile.Layer);
                            }

                            Layer buildingsLayer = gameLocation.map.GetLayer("Buildings");
                            Layer frontLayer = gameLocation.map.GetLayer("Front");
                            TileSheet tilesheet = gameLocation.map.GetTileSheet("z_waterTroughTilesheet");

                            foreach (TroughTile tile in ModEntry.Instance.CurrentTroughPlacementProfile.barn3TroughTiles)
                            {
                                if (tile.Layer.Equals("Buildings"))
                                    buildingsLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(buildingsLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                                else if (tile.Layer.Equals("Front"))
                                    frontLayer.Tiles[tile.TileX, tile.TileY] = new StaticTile(frontLayer, tilesheet, BlendMode.Alpha, tileIndex: tile.EmptyTroughTilesheetIndex);
                            }

                            break;
                        }
                    }
                }
            }
        }
    }
}
