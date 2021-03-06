﻿using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using EquivalentExchange.Models;
using System.IO;
using System.Reflection;
using StardewValley.Menus;

namespace EquivalentExchange
{

    /// <summary>The mod entry point.</summary>
    public class EquivalentExchange : Mod
    {
        //instantiate config
        private ConfigurationModel Config;

        //this instance of the mod's helper class file, intialized by Entry
        public IModHelper eeHelper;

        //the mod's "static" instance, initialized by Entry. There caN ONly bE ONe
        public static EquivalentExchange instance;

        public SaveDataModel currentPlayerData;

        //config for if the mod is allowed to play sounds
        public static bool canPlaySounds;

        //handles all the things.
        public override void Entry(IModHelper helper)
        {
            //set the static instance variable. is this an oxymoron?
            instance = this;

            //preserve this entry method's helper class because it's.. helpful.
            instance.eeHelper = helper;

            //read the config file, poached from horse whistles, get the configured keys and settings
            Config = helper.ReadConfig<ConfigurationModel>();

            //add handler for the "transmute/copy" button.
            ControlEvents.KeyPressed += ControlEvents_KeyPressed;

            //exclusively to figure out if ctrl or shift have been let go of.
            ControlEvents.KeyReleased += ControlEvents_KeyReleased;

            //wire up the library scraping function to occur on save-loading to defer recipe scraping until all mods are loaded, optimistically.
            SaveEvents.AfterLoad += SaveEvents_AfterLoad;

            //we need this to save our alchemists['] data
            SaveEvents.BeforeSave += SaveEvents_BeforeSave;

            //set texture files in memory, they're tiny things.
            DrawingUtil.HandleTextureCaching();

            //trying something completely different from a patched event hook...
            //gonna try using this to detect the night event heuristically.
            GameEvents.UpdateTick += GameEvents_UpdateTick;

            //wire up the PreRenderHUD event so I can display info bubbles when needed
            GraphicsEvents.OnPreRenderHudEvent += GraphicsEvents_OnPreRenderHudEvent;

            //check for experience bars mod: if it's here we draw hud elements for the new alchemy skill
            CheckForExperienceBarsMod();
            if (hasExperienceBarsMod)
            {
                GraphicsEvents.OnPostRenderHudEvent += GraphicsEvents_OnPostRenderHudEvent;
            }

            //check for all professions mod: if it's here we run a wireup to give the player all skills professions at the right time (or after), when present.
            CheckForAllProfessionsMod();
            if (hasAllProfessionsMod)
            {
                LocationEvents.CurrentLocationChanged += LocationEvents_CurrentLocationChanged; ;
            }

            //add a debug option to give yourself experience
            Helper.ConsoleCommands.Add("player_givealchemyexp", "player_givealchemyexp <amount>", GiveAlchemyExperience);

            //post render event for skills menu
            GraphicsEvents.OnPostRenderGuiEvent += DrawAfterGUI;

            //check for chase's skills
            checkForLuck();
            checkForCooking();
        }

        //integration considerations for chase's skills

        public static bool hasLuck = false;
        private void checkForLuck()
        {
            if (!Helper.ModRegistry.IsLoaded("spacechase0.LuckSkill"))
            {
                Log.info("Luck Skill not found");
                return;
            }

            hasLuck = true;
        }

        public static bool hasCooking = false;
        private void checkForCooking()
        {
            if (!Helper.ModRegistry.IsLoaded("spacechase0.CookingSkill"))
            {
                Log.info("Cooking Skill not found");
                return;
            }

            hasCooking = true;
        }

        private void DrawAfterGUI(object sender, EventArgs args)
        {
            if (Game1.activeClickableMenu is GameMenu)
            {
                GameMenu menu = Game1.activeClickableMenu as GameMenu;
                if (menu.currentTab == GameMenu.skillsTab)
                {
                    var tabs = (List<IClickableMenu>)Util.GetInstanceField(typeof(GameMenu), menu, "pages");
                    var skills = (SkillsPage)tabs[GameMenu.skillsTab];
                    var alchemySkills = new AlchemySkillsPage(skills.xPositionOnScreen, skills.yPositionOnScreen, skills.width, skills.height, 5 + (hasLuck ? 1 : 0) + (hasCooking ? 1 : 0));
                    alchemySkills.draw(Game1.spriteBatch);
                }
            }
        }

        //command to give yourself experience for debug purposes primarily

        private void GiveAlchemyExperience(object sender, string[] args)
        {
            if (args.Length != 1)
            {
                Log.info("Command format: giveAlchemyExp <amount>");
                return;
            }

            int amt = 0;
            try
            {
                amt = Convert.ToInt32(args[0]);
            }
            catch (Exception e)
            {
                Log.error("Bad experience amount.");
                return;
            }

            Alchemy.AddAlchemyExperience(amt);
            Log.info("Added " + amt + " alchemy experience.");
        }

        public List<int> showLevelUpMenusByRank = new List<int>();

        internal void AddSkillUpMenuAppearance(int alchemyLevel)
        {
            showLevelUpMenusByRank.Add(alchemyLevel);
        }

        //internal default value for the repeat rate starting point of the auto-fire functionality of transmute/liquidate when the buttons are held.
        private const int AUTO_REPEAT_UPDATE_RATE_REFRESH = 50;

        int heldCounter = 1;
        int updateTickCount = AUTO_REPEAT_UPDATE_RATE_REFRESH;
        private void GameEvents_UpdateTick(object sender, EventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
            //unsure if this does what I think it does
            if (Game1.player.isEmoting && Game1.player.CurrentEmote == 24)
            {
                //the player has just answered "yes" to sleep? or the player passed out like a chump.
                if (Game1.currentLocation.lastQuestionKey == null || Game1.currentLocation.lastQuestionKey.Equals("Sleep"))
                    AddEndOfNightMenus();
            }

            if (transmuteKeyHeld)
            {
                heldCounter++;
                if (heldCounter % updateTickCount == 0)
                {
                    HandleEitherTransmuteEvent(Config.TransmuteKey.ToString());
                    updateTickCount = (int)Math.Floor(Math.Max(1, updateTickCount * 0.9F));
                }
            }
            if (liquidateKeyHeld)
            {
                heldCounter++;
                if (heldCounter % updateTickCount == 0)
                {
                    HandleEitherTransmuteEvent(Config.LiquidateKey.ToString());
                    updateTickCount = (int)Math.Floor(Math.Max(1, updateTickCount * 0.9F));
                }
            }
        }

        //show the level up menus at night when you hit a profession breakpoint.
        private void AddEndOfNightMenus()
        {
            if (!Context.IsWorldReady)
                return;

            //trick the game into a full black backdrop early, this just keeps the sequence from looking bizarre.
            Game1.fadeToBlackAlpha = 1F;

            bool playerNeedsLevelFiveProfession = currentPlayerData.AlchemyLevel >= 5 && !Game1.player.professions.Contains((int)Professions.Shaper) && !Game1.player.professions.Contains((int)Professions.Sage);
            bool playerNeedsLevelTenProfession = currentPlayerData.AlchemyLevel >= 10 && !Game1.player.professions.Contains((int)Professions.Transmuter) && !Game1.player.professions.Contains((int)Professions.Adept) && !Game1.player.professions.Contains((int)Professions.Aurumancer) && !Game1.player.professions.Contains((int)Professions.Conduit);
            bool playerGainedALevel = showLevelUpMenusByRank.Count() > 0;

            //nothing requires our intervention, bypass this method as it is heavy on logic and predecate searches, and we don't want to fire those every #&!$ing tick
            if (!playerGainedALevel && !playerNeedsLevelFiveProfession && !playerNeedsLevelTenProfession)
                return;

            if (playerGainedALevel)
            {
                for (int i = showLevelUpMenusByRank.Count() - 1; i >= 0; --i)
                {
                    int level = showLevelUpMenusByRank[i];
                    //search for existing levelups already injected into the night menu routine.
                    List<IClickableMenu> existingLevelUps = Game1.endOfNightMenus.Where(x => x.GetType().Equals(typeof(AlchemyLevelUpMenu)) && ((AlchemyLevelUpMenu)x).GetLevel() == level).ToList();
                    //excuse the plural, this check is testing for *this level* specifically.
                    if (existingLevelUps.Count == 0)
                    {
                        Game1.endOfNightMenus.Push(new AlchemyLevelUpMenu(level));
                    }
                }
                //presume we've added all the levels we need, wipe this thing.
                showLevelUpMenusByRank.Clear();
            }
            else if (playerNeedsLevelFiveProfession)
            {
                List<IClickableMenu> existingLevelUps = Game1.endOfNightMenus.Where(x => x.GetType().Equals(typeof(AlchemyLevelUpMenu)) && ((AlchemyLevelUpMenu)x).GetLevel() == 5).ToList();
                if (existingLevelUps.Count == 0)
                    Game1.endOfNightMenus.Push(new AlchemyLevelUpMenu(5));
            }
            else if (playerNeedsLevelTenProfession)
            {
                List<IClickableMenu> existingLevelUps = Game1.endOfNightMenus.Where(x => x.GetType().Equals(typeof(AlchemyLevelUpMenu)) && ((AlchemyLevelUpMenu)x).GetLevel() == 10).ToList();
                if (existingLevelUps.Count == 0)
                    Game1.endOfNightMenus.Push(new AlchemyLevelUpMenu(10));
            }
        }

        //misleading event wireup is actually for the has-all-professions mod, which enables all professions at the appropriate level.
        private void LocationEvents_CurrentLocationChanged(object sender, EventArgsCurrentLocationChanged e)
        {
            if (hasAllProfessionsMod)
            {
                List<int> professions = Game1.player.professions;
                List<List<int>> list = new List<List<int>> { Professions.firstRankProfessions, Professions.secondRankProfessions };
                foreach (List<int> current in list)
                {
                    bool flag = professions.Intersect(current).Any<int>();
                    if (flag)
                    {
                        foreach (int current2 in current)
                        {
                            bool flag2 = !professions.Contains(current2);
                            if (flag2)
                            {
                                professions.Add(current2);
                            }
                        }
                    }
                }
            }
        }

        //hooked for drawing the experience bar on screen when experience bars mod is present.
        private void GraphicsEvents_OnPostRenderHudEvent(object sender, EventArgs e)
        {
            DrawingUtil.DoPostRenderHudEvent();
        }

        //ensures that the wizard tower and witch hut are leylines for the mod by default.
        private static string[] VANILLA_LEYLINE_LOCATIONS = new string[] { "WizardHouse", "WitchHut", "Desert" };

        private void InitializeVanillaLeylines()
        {
            foreach (string leyline in VANILLA_LEYLINE_LOCATIONS)
            {
                if (Game1.getLocationFromName(leyline) == null)
                    Log.error($"{leyline} is missing, there is a very bad problem and you will not be going to space today.");
                else
                    Game1.getLocationFromName(leyline)?.map.Properties.Add(Alchemy.LEYLINE_PROPERTY_INDICATOR, 0F);
            }
        }

        //fires when loading a save, initializes the item blacklist and loads player save data.
        private void SaveEvents_AfterLoad(object sender, EventArgs e)
        {
            InitializePlayerData();
            InitializeVanillaLeylines();
            PopulateItemLibrary();
        }

        //handles reading current player json file and loading them into memory
        private void InitializePlayerData()
        {
            // save is loaded
            if (Context.IsWorldReady)
            {
                //fetch the alchemy save for this game file.
                instance.currentPlayerData = instance.eeHelper.ReadJsonFile<SaveDataModel>(Path.Combine(Constants.CurrentSavePath, $"{Game1.uniqueIDForThisGame.ToString()}.json"));

                //we want to generate the save data model, but we don't save it until we're supposed to, to prevent data from saving prematurely (thus generating a new multiplayer ID)
                if (instance.currentPlayerData == null)
                {
                    instance.currentPlayerData = new SaveDataModel(Game1.uniqueIDForThisGame);
                }
            }
        }

        //handles writing "each" player's json save to the appropriate file.
        private void SaveEvents_BeforeSave(object sender, EventArgs e)
        {
            SavePlayerData();
        }

        private void SavePlayerData()
        {
            instance.eeHelper.WriteJsonFile<SaveDataModel>(Path.Combine(Constants.CurrentSavePath, $"{ Game1.uniqueIDForThisGame.ToString()}.json"), instance.currentPlayerData);
        }

        /// <summary>Update the mod's config.json file from the current <see cref="Config"/>.</summary>
        internal void SaveConfig()
        {
            eeHelper.WriteConfig(Config);
        }

        private static bool allowInfoBubbleToRender = false;
        private static void GraphicsEvents_OnPreRenderHudEvent(object sender, EventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (Game1.eventUp)
                return;

            if (!allowInfoBubbleToRender)
                return;

            //per the advice of Ento, abort if the player is in an event
            if (Game1.CurrentEvent != null)
                return;

            //something may have gone wrong if this is null, maybe there's no save data?
            if (Game1.player != null)
            {
                //get the player's current item
                Item heldItem = Game1.player.CurrentItem;

                //player is holding item
                if (heldItem != null)
                {
                    //get the item's ID
                    int heldItemID = heldItem.parentSheetIndex;

                    //abort any transmutation event for blacklisted items or items that for whatever reason can't exist in world.
                    if (blackListedItemIDs.Contains(heldItemID) || !heldItem.canBeDropped())
                    {
                        return;
                    }

                    //get the transmutation value, it's based on what it's worth to the player, including profession bonuses. This affects both cost and value.
                    int actualValue = ((StardewValley.Object)heldItem).sellToStorePrice();
                    int transmuteCost = (int)Math.Ceiling(Alchemy.GetTransmutationMarkupPercentage() * actualValue);
                    int liquidateValue = (int)Math.Floor(Alchemy.GetLiquidationValuePercentage() * actualValue);
                    float staminaDrain = (float)Math.Round(Alchemy.GetStaminaCostForTransmutation(actualValue), 2);
                    float luckyChance = (float)Math.Round(Alchemy.GetLuckyTransmuteChance() * 100, 2);
                    float reboundChance = (float)Math.Round(Alchemy.GetReboundChance(false, false) * 100, 2);
                    int reboundDamage = Alchemy.GetReboundDamage(actualValue);

                    int xPos = -15;
                    int yPos = 0;// Game1.viewport.Height / 2 - 200;
                    int xSize = 240;
                    int ySize = 320;
                    int dialogPositionMarkerX = xPos + 40;
                    int dialogPositionMarkerY = yPos + 100;
                    string cost = $"Make -{transmuteCost.ToString()}g";
                    string value = $"Melt +{liquidateValue.ToString()}g";
                    string luck = $"Luck {luckyChance.ToString()}%";
                    string stam = $"Stam -{staminaDrain.ToString()}";
                    string rebound = $"Fail {reboundChance.ToString()}%";
                    string damage = $"HP -{reboundDamage.ToString()}";
                    int rowSpacing = 30;
                    Game1.drawDialogueBox(xPos, yPos, xSize, ySize, false, true, (string)null, false);
                    Game1.spriteBatch.DrawString(Game1.smallFont, cost, new Microsoft.Xna.Framework.Vector2(dialogPositionMarkerX, dialogPositionMarkerY), Game1.textColor);
                    dialogPositionMarkerY += rowSpacing;
                    Game1.spriteBatch.DrawString(Game1.smallFont, value, new Microsoft.Xna.Framework.Vector2(dialogPositionMarkerX, dialogPositionMarkerY), Game1.textColor);
                    dialogPositionMarkerY += rowSpacing;
                    Game1.spriteBatch.DrawString(Game1.smallFont, luck, new Microsoft.Xna.Framework.Vector2(dialogPositionMarkerX, dialogPositionMarkerY), Game1.textColor);
                    dialogPositionMarkerY += rowSpacing;
                    Game1.spriteBatch.DrawString(Game1.smallFont, stam, new Microsoft.Xna.Framework.Vector2(dialogPositionMarkerX, dialogPositionMarkerY), Game1.textColor);
                    dialogPositionMarkerY += rowSpacing;
                    Game1.spriteBatch.DrawString(Game1.smallFont, rebound, new Microsoft.Xna.Framework.Vector2(dialogPositionMarkerX, dialogPositionMarkerY), Game1.textColor);
                    dialogPositionMarkerY += rowSpacing;
                    Game1.spriteBatch.DrawString(Game1.smallFont, damage, new Microsoft.Xna.Framework.Vector2(dialogPositionMarkerX, dialogPositionMarkerY), Game1.textColor);
                    dialogPositionMarkerY += rowSpacing;
                }
            }
        }

        //handles the release key event for figuring out if control or shift is let go of
        public static void ControlEvents_KeyReleased(object sender, EventArgsKeyPressed e)
        {
            //let the app know the shift key is released
            if (e.KeyPressed == leftShiftKey || e.KeyPressed == rightShiftKey)
                SetModifyingControlKeyState(e.KeyPressed, false);

            //pop up a window with information about your success rates, transmute rates, the leyline locale, useful stuff to know about what you're holding.. etc.
            if (instance.Config.TransmuteInfoKey.Equals(e.KeyPressed.ToString()))
            {
                allowInfoBubbleToRender = false;
            }

            //the key for transmuting is pressed, fire once and then initiate the callback routine to auto-fire.
            if (instance.Config.TransmuteKey.Equals(e.KeyPressed.ToString()))
            {
                transmuteKeyHeld = false;
                instance.heldCounter = 1;
                instance.updateTickCount = AUTO_REPEAT_UPDATE_RATE_REFRESH;
            }

            //the key pressed is one of the mods keys.. I'm doing this so I don't fire logic for anything unless either of the mod's keys were pressed.            
            if (instance.Config.LiquidateKey.Equals(e.KeyPressed.ToString()))
            {
                liquidateKeyHeld = false;
                instance.heldCounter = 1;
                instance.updateTickCount = AUTO_REPEAT_UPDATE_RATE_REFRESH;
            }
        }

        //remembers the state of the mod control keys so we can do some fancy stuff.
        public static bool transmuteKeyHeld = false;
        public static bool liquidateKeyHeld = false;

        //handles the key press event for figuring out if control or shift is held down, or either of the mod's major transmutation actions is being attempted.
        public static void ControlEvents_KeyPressed(object sender, EventArgsKeyPressed e)
        {
            //let the app know the shift key is held
            if (e.KeyPressed == leftShiftKey || e.KeyPressed == rightShiftKey)
                SetModifyingControlKeyState(e.KeyPressed, true);

            //pop up a window with information about your success rates, transmute rates, the leyline locale, useful stuff to know about what you're holding.. etc.
            if (instance.Config.TransmuteInfoKey.Equals(e.KeyPressed.ToString()))
            {
                allowInfoBubbleToRender = true;
            }

            //the key for transmuting is pressed, fire once and then initiate the callback routine to auto-fire.
            if (instance.Config.TransmuteKey.Equals(e.KeyPressed.ToString()))
            {
                transmuteKeyHeld = true;
                HandleEitherTransmuteEvent(e.KeyPressed.ToString());
            }

            //the key pressed is one of the mods keys.. I'm doing this so I don't fire logic for anything unless either of the mod's keys were pressed.            
            if (instance.Config.LiquidateKey.Equals(e.KeyPressed.ToString()))
            {
                liquidateKeyHeld = true;
                HandleEitherTransmuteEvent(e.KeyPressed.ToString());
            }

            //the key pressed is one of the mods keys.. I'm doing this so I don't fire logic for anything unless either of the mod's keys were pressed.            
            if (instance.Config.NormalizeKey.Equals(e.KeyPressed.ToString()))
            {                
                HandleEitherTransmuteEvent(e.KeyPressed.ToString());
            }
        }

        //sets up the basic structure of either transmute event, since they have some common ground
        private static void HandleEitherTransmuteEvent(string keyPressed)
        {
            // save is loaded
            if (Context.IsWorldReady)
            {
                //per the advice of Ento, abort if the player is in an event
                if (Game1.CurrentEvent != null)
                    return;

                //something may have gone wrong if this is null, maybe there's no save data?
                if (Game1.player != null)
                {
                    //get the player's current item
                    Item heldItem = Game1.player.CurrentItem;

                    //player is holding item
                    if (heldItem != null)
                    {
                        //get the item's ID
                        int heldItemID = heldItem.parentSheetIndex;

                        //abort any transmutation event for blacklisted items or items that for whatever reason can't exist in world.
                        if (blackListedItemIDs.Contains(heldItemID) || !heldItem.canBeDropped())
                        {
                            return;
                        }

                        //get the transmutation value, it's based on what it's worth to the player, including profession bonuses. This affects both cost and value.
                        int actualValue = ((StardewValley.Object)heldItem).sellToStorePrice();

                        //try to transmute [copy] the item
                        if (keyPressed.ToString() == instance.Config.TransmuteKey)
                        {
                            Alchemy.HandleTransmuteEvent(heldItem, actualValue);
                        }

                        //try to liquidate the item [sell for gold]
                        if (keyPressed.ToString() == instance.Config.LiquidateKey)
                        {
                            Alchemy.HandleLiquidateEvent(heldItem, actualValue);
                        }

                        //try to normalize the item [make all items of a different quality one quality and exchange any remainder for gold]
                        if (keyPressed.ToString() == instance.Config.NormalizeKey)
                        {
                            Alchemy.HandleNormalizeEvent(heldItem, actualValue);
                        }
                    }
                }
            }
        }

        //control key modifiers [shift and ctrl], I include both for a more robust "is either pressed" mechanic.
        public static bool leftShiftKeyPressed = false;
        public static bool rightShiftKeyPressed = false;

        //simple consts to keep code clean, both shift keys, both control keys.
        public const Keys leftShiftKey = Keys.LeftShift;
        public const Keys rightShiftKey = Keys.RightShift;

        //convenience methods for detecting when either keys are pressed to modify amount desired from liquidation/transmutes.
        public static bool IsShiftKeyPressed()
        {
            return leftShiftKeyPressed || rightShiftKeyPressed;
        }

        //handler for which flag to set when X key is pressed/released
        public static void SetModifyingControlKeyState(Keys keyChanged, bool isPressed)
        {
            switch (keyChanged)
            {
                case leftShiftKey:
                    leftShiftKeyPressed = isPressed;
                    break;
                case rightShiftKey:
                    rightShiftKeyPressed = isPressed;
                    break;
                default:
                    break;
            }
        }
        
        //holds a list of item IDs which are invalid for transmutation due to being created by recipes. This is to help avoid positive value feedback loops.
        public static List<int> blackListedItemIDs = new List<int>();

        public void PopulateItemLibrary()
        {
            //the point of this routine is to find all of the objects that are created from a recipe. This mod will only transmute raw materials
            //so anything that is cooked and crafted should not be possible to transmute. This is sort of for balance reasons? It's OP anyway.

            //Now we're iterating over these two lists to obtain a list of IDs which are invalid for transmutation
            Dictionary<string, string>[] recipeDictionaries = { CraftingRecipe.craftingRecipes, CraftingRecipe.cookingRecipes };
            foreach (Dictionary<string, string> recipeDictionary in recipeDictionaries)
            {
                foreach (KeyValuePair<string, string> recipe in recipeDictionary)
                {
                    //values are tokenized by a / and then subtokenized by spaces
                    string[] recipeValues = recipe.Value.Split('/');

                    //index 2 of this array is the output ID and amount, tokenized by spaces. Not all outputs have an amount, it defaults to 1.
                    //we don't care about quantity anyway.
                    string[] recipeOutputs = recipeValues[2].Split(' ');

                    //index 0 of this array is, thus, the output ID.
                    int.TryParse(recipeOutputs[0], out int recipeItemID);

                    //add the recipe item ID to the list of items the player can't transmute
                    blackListedItemIDs.Add(recipeItemID);
                }

            }

            //iterate over game objects
            foreach (KeyValuePair<int, string> entry in Game1.objectInformation)
            {
                //get basic vars

                //id
                int itemID = entry.Key;

                //literally everything else
                string itemValue = entry.Value;

                //split 'everything' into an array
                string[] parsedValues = itemValue.Split('/');

                //item cost/value is index 1
                int.TryParse(parsedValues[1], out int itemCost);

                //objects with a cost of 0 are blacklisted
                if (itemCost < 1)
                    blackListedItemIDs.Add(itemID);
            }

            //prismatic shard is blacklisted.
            blackListedItemIDs.Add(StardewValley.Object.prismaticShardIndex);
        }

        //hopefully the stuff needed to support spacechase0's show-experience-bars mod can start here

        public static bool hasExperienceBarsMod = false;

        public void CheckForExperienceBarsMod()
        {
            if (!Helper.ModRegistry.IsLoaded("spacechase0.ExperienceBars"))
            {
                Log.info("Experience Bars not found");
                return;
            }

            hasAllProfessionsMod = true;

            Log.info("Experience Bars mod found, adding alchemy experience bar renderer.");
        }

        public static bool hasAllProfessionsMod = false;
        public void CheckForAllProfessionsMod()
        {
            if (!eeHelper.ModRegistry.IsLoaded("community.AllProfessions"))
            {
                Log.info("All Professions not found.");
                return;
            }

            Log.info("All Professions mod found. You will get every alchemy profession for your level.");
            hasAllProfessionsMod = true;
        }
    }
}