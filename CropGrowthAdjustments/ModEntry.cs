﻿using System;
using System.Collections.Generic;
using System.Linq;
using CropGrowthAdjustments.Patching;
using CropGrowthAdjustments.Types;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace CropGrowthAdjustments
{
    /// <summary> The mod entry class loaded by SMAPI. </summary>
    public class ModEntry : Mod
    {
        public static ContentPackManager ContentPackManager = new ContentPackManager();
        public static IMonitor ModMonitor;
        public static IModHelper ModHelper;
        
        /// <summary> The mod entry point, called after the mod is first loaded. </summary>
        /// <param name="helper"> Provides simplified APIs for writing mods. </param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            
            ContentPackManager.InitializeContentPacks(helper, Monitor);

            ModMonitor = Monitor;
            ModHelper = helper;
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("TileSheets/Crops"))
            {
                e.Edit(EditCropTilesheet, AssetEditPriority.Late);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Crops"))
            {
                e.Edit(EditCropData, AssetEditPriority.Late);
            }
        }
        
        private void EditCropTilesheet(IAssetData asset)
        {
            var editor = asset.AsImage();

            if (Game1.currentLocation == null)
            {
                return;
            }
            
            foreach (var adjustments in ContentPackManager.ContentPacks)
            {
                foreach (var cropAdjustment in adjustments.CropAdjustments)
                {
                    if (cropAdjustment.SpecialSpritesForSeasons == null) continue;
                    
                    // replace crop sprites with any applicable special sprite replacements
                    foreach (var specialSprite in cropAdjustment.SpecialSpritesForSeasons)
                    {
                        if (Utility.IsInAnyOfSpecifiedLocations(specialSprite.GetLocationsToIgnore(), Game1.currentLocation)) continue;
                        if (specialSprite.GetSeason() != Game1.season) continue;
                        
                        Texture2D sourceImage = specialSprite.SpritesTexture;
                        // load the image if it hasn't been loaded yet
                        if (sourceImage == null) {
                            try
                            {
                                sourceImage = adjustments.ContentPack.ModContent.Load<Texture2D>(specialSprite.Sprites);
                            }
                            catch (Exception e)
                            {
                                Monitor.Log($"{adjustments.ContentPack.Manifest.Name} - Could not load special sprites for {cropAdjustment.CropProduceName} (season: {specialSprite.Season}), the plant might be invisible: {e}.", LogLevel.Error);
                                continue;
                            }
                            specialSprite.SpritesTexture = sourceImage;
                        }
                        
                        int x = cropAdjustment.RowInCropSpriteSheet % 2 == 0 ? 0 : 128;
                        int y = (int) (Math.Floor(cropAdjustment.RowInCropSpriteSheet / 2f) * 32);
                        editor.PatchImage(sourceImage, targetArea: new Rectangle(x, y, 128, 32));
                    }
                }
            }
        }
        
        private void EditCropData(IAssetData asset)
        {
            var cropData = asset.AsDictionary<string, CropData>();
            
            foreach (var adjustments in ContentPackManager.ContentPacks)
            {
                foreach (var cropAdjustment in adjustments.CropAdjustments)
                {
                    var cropProduceItemId = cropAdjustment.CropProduceItemId;
                            
                    foreach (var itemId in cropData.Data.Keys)
                    {
                        if(cropData.Data[itemId].HarvestItemId != cropProduceItemId.ToString()) continue;
                                
                        cropData.Data[itemId].Seasons = cropAdjustment.GetSeasonsToGrowIn();
                                
                        break;
                    }
                }
            }
        }
        
        private void ApplyHarmonyPatches()
        {
            var harmony = new Harmony("GZhynko.CropGrowthAdjustments");
            
            harmony.Patch(
                AccessTools.Method(typeof(HoeDirt), nameof(HoeDirt.dayUpdate)),
                new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.HoeDirtDayUpdate))
            );
            
            harmony.Patch(
                AccessTools.Method(typeof(IndoorPot), nameof(IndoorPot.DayUpdate)), 
                new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.IndoorPotDayUpdate))
            );

            harmony.Patch(
                AccessTools.Method(typeof(Crop), nameof(Crop.newDay)),
                new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.CropNewDay))
            );
            
            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), "resetLocalState", new Type[] {}),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(HarmonyPatches.GameLocationResetLocalState))
            );
        }

        /// <summary> Raised after the game is launched, right before the first update tick. This happens once per game session (unrelated to loading saves). All mods are loaded and initialised at this point, so this is a good time to set up mod integrations. </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            ApplyHarmonyPatches();
            
            var jsonAssetsApi = Helper.ModRegistry.GetApi<IJsonAssetsApi>("spacechase0.JsonAssets");
            if (jsonAssetsApi != null)
            {
                jsonAssetsApi.IdsAssigned += (_, _) => OnJsonAssetsIdsAssigned(jsonAssetsApi);
            }
            else
            {
                ContentPackManager.AssignCropProduceItemIds(Helper, null);
                ContentPackManager.AssignCropRowsInSpritesheet(Helper);
                Helper.GameContent.InvalidateCache("Data/Crops");
            }
        }

        private void OnJsonAssetsIdsAssigned(IJsonAssetsApi jsonAssetsApi)
        {
            ContentPackManager.AssignCropProduceItemIds(Helper, jsonAssetsApi);
            Helper.GameContent.InvalidateCache("Data/Crops");
            
            ContentPackManager.AssignCropRowsInSpritesheet(Helper);
        }
    }
}
