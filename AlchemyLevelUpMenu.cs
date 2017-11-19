﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquivalentExchange
{
    // Copy from LevelUpMenu
    public class AlchemyLevelUpMenu : IClickableMenu
    {
        public const int basewidth = 768;

        public const int baseheight = 512;

        public bool informationUp;

        public bool isActive;

        public bool isProfessionChooser;

        private int currentLevel;

        private int timerBeforeStart;

        private Color leftProfessionColor = Game1.textColor;

        private Color rightProfessionColor = Game1.textColor;

        private MouseState oldMouseState;

        private ClickableTextureComponent okButton;

        private List<CraftingRecipe> newCraftingRecipes = new List<CraftingRecipe>();

        private List<string> extraInfoForLevel = new List<string>();

        private List<string> leftProfessionDescription = new List<string>();

        private List<string> rightProfessionDescription = new List<string>();

        private Rectangle sourceRectForLevelIcon;

        private string title;

        private List<EquivalentExchange.Professions> professionsToChoose = new List<EquivalentExchange.Professions>();

        private List<TemporaryAnimatedSprite> littleStars = new List<TemporaryAnimatedSprite>();

        private StardewValley.Farmer player = null;

        public AlchemyLevelUpMenu()
            : base(Game1.viewport.Width / 2 - 384, Game1.viewport.Height / 2 - 256, 768, 512, false)
        {
            this.player = null;
            this.width = Game1.tileSize * 12;
            this.height = Game1.tileSize * 8;
            this.okButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width + 4, this.yPositionOnScreen + this.height - Game1.tileSize - IClickableMenu.borderWidth, Game1.tileSize, Game1.tileSize), Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46, -1, -1), 1f, false);
        }

        public AlchemyLevelUpMenu(StardewValley.Farmer constructorPlayer,  int level)
            : base(Game1.viewport.Width / 2 - 384, Game1.viewport.Height / 2 - 256, 768, 512, false)
        {
            this.player = constructorPlayer;
            this.timerBeforeStart = 250;
            this.isActive = true;
            this.width = Game1.tileSize * 12;
            this.height = Game1.tileSize * 8;
            this.okButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width + 4, this.yPositionOnScreen + this.height - Game1.tileSize - IClickableMenu.borderWidth, Game1.tileSize, Game1.tileSize), Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46, -1, -1), 1f, false);
            this.newCraftingRecipes.Clear();
            this.extraInfoForLevel.Clear();
            player.completelyStopAnimatingOrDoingAction();
            this.informationUp = true;
            this.isProfessionChooser = false;
            this.currentLevel = level;
            this.title = string.Concat(new object[]
            {
                "Level ",
                this.currentLevel,
                " Alchemy"
            });
            this.extraInfoForLevel = this.GetExtraInfoForLevel(this.currentLevel, constructorPlayer.luckLevel, EquivalentExchange.instance.currentPlayerData.HasAurumancerProfession, EquivalentExchange.instance.currentPlayerData.HasTransmuterProfession);
            sourceRectForLevelIcon = new Rectangle(0, 0, 16, 16);
            if (this.currentLevel > 0 && this.currentLevel % 5 == 0)
            {
                this.professionsToChoose.Clear();
                this.isProfessionChooser = true;
                if (this.currentLevel == 5)
                {
                    this.professionsToChoose.Add(EquivalentExchange.Professions.Shaper);
                    this.professionsToChoose.Add(EquivalentExchange.Professions.Sage);
                }
                else if (EquivalentExchange.instance.currentPlayerData.HasShaperProfession)
                {
                    this.professionsToChoose.Add(EquivalentExchange.Professions.Transmuter);
                    this.professionsToChoose.Add(EquivalentExchange.Professions.Adept);                    
                }
                else
                {
                    this.professionsToChoose.Add(EquivalentExchange.Professions.Transmuter);
                    this.professionsToChoose.Add(EquivalentExchange.Professions.Adept);
                }
                this.leftProfessionDescription = AlchemyLevelUpMenu.getProfessionDescription(this.professionsToChoose[0]);
                this.rightProfessionDescription = AlchemyLevelUpMenu.getProfessionDescription(this.professionsToChoose[1]);
            }
            int num = 0;
            this.height = num + Game1.tileSize * 4 + this.extraInfoForLevel.Count<string>() * Game1.tileSize * 3 / 4;
            player.freezePause = 100;
            this.gameWindowSizeChanged(Rectangle.Empty, Rectangle.Empty);
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            this.xPositionOnScreen = Game1.viewport.Width / 2 - this.width / 2;
            this.yPositionOnScreen = Game1.viewport.Height / 2 - this.height / 2;
            this.okButton.bounds = new Rectangle(this.xPositionOnScreen + this.width + 4, this.yPositionOnScreen + this.height - Game1.tileSize - IClickableMenu.borderWidth, Game1.tileSize, Game1.tileSize);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
        }

        public List<string> GetExtraInfoForLevel(int whichLevel, int luckLevel, bool hasAurumancerProfession, bool hasTransmuterProfession)
        {
            double nextCoefficientCost = (Alchemy.GetTransmutationMarkupPercentage(whichLevel, hasTransmuterProfession) - Alchemy.transmutationBonusPerLevel) * 100D;
            string coefficientCost = $"Transmutation Cost: {nextCoefficientCost.ToString()}%";
            double nextCoefficientValue = (Alchemy.GetLiquidationValuePercentage(whichLevel, hasAurumancerProfession) + Alchemy.liquidationBonusPerLevel) * 100D;
            string coefficientValue = $"Liquidation Value: {nextCoefficientValue.ToString()}%";
            double luckyTransmuteMinimum = ((Alchemy.GetLuckyTransmuteChanceWithoutDailyOrProfessionBonuses(whichLevel, luckLevel) + 0.01) * 100);
            double luckyTransmuteMaximum = ((Alchemy.GetLuckyTransmuteChanceWithoutDailyOrProfessionBonuses(whichLevel, luckLevel) + Alchemy.luckNormalizationForFreeTransmutes) * 100);
            string luckyTransmuteChance = $"Lucky Transmute Chance: {luckyTransmuteMinimum.ToString()}% to {luckyTransmuteMaximum.ToString()}%";
            string distanceFromTowerImpact = $"You can be { whichLevel } maps from an alchemical leyline (such as inside the Wizard's Tower) before rebounds become more likely.";

            List<string> extraInfoList = new List<string>();
            extraInfoList.Add(coefficientCost);
            extraInfoList.Add(coefficientValue);
            extraInfoList.Add(luckyTransmuteChance);
            extraInfoList.Add(distanceFromTowerImpact);
            return extraInfoList;
        }

        public static void AddProfessionDescriptions(List<string> list, EquivalentExchange.Professions whichProfession)
        {
            list.Add(GetProfessionName(whichProfession));
            switch (whichProfession)
            {
                case EquivalentExchange.Professions.Shaper:                    
                    list.Add("Daily luck has twice the effect on Lucky Transmutation (costing no stamina).");
                    list.Add("With no other bonuses, chance fluctuates between 2% and 50%.");
                    break;
                case EquivalentExchange.Professions.Sage:                    
                    list.Add($"The base stamina cost of any transmutation is");
                    list.Add($"reduced by a flat { (Alchemy.sageProfessionStaminaDrainBonus * 100D) }%");
                    break;
                case EquivalentExchange.Professions.Transmuter:
                    double nextCoefficientCost = (Alchemy.GetTransmutationMarkupPercentage(10, true) - Alchemy.transmuterTransmutationBonus) * 100D;                    
                    list.Add($"Transmutation Cost reduced to { nextCoefficientCost.ToString() }%");
                    break;
                case EquivalentExchange.Professions.Adept:                    
                    list.Add($"Proximity to leylines (eg. inside the wizard's tower)");
                    list.Add($"increase your chance of a lucky transmute (costing no stamina) by up to 15%.");
                    break;
                case EquivalentExchange.Professions.Aurumancer:
                    double nextCoefficientValue = (Alchemy.GetLiquidationValuePercentage(10, true) + Alchemy.aurumancerLiquidationBonus) * 100D;                    
                    list.Add($"Liquidation Value increased to { nextCoefficientValue.ToString() }%");
                    break;
                case EquivalentExchange.Professions.Conduit:
                    list.Add($"Any transmutation that would fail due to a rebound is now");
                    list.Add($"a lucky transmute (costing no stamina), but you still take damage.");
                    break;
            }
        }

        public static string GetProfessionName(EquivalentExchange.Professions whichProfession)
        {
            switch (whichProfession)
            {
                case EquivalentExchange.Professions.Shaper:
                    return "Shaper";
                case EquivalentExchange.Professions.Sage:
                    return "Sage";
                case EquivalentExchange.Professions.Transmuter:
                    return "Transmuter";
                case EquivalentExchange.Professions.Adept:
                    return "Adept";
                case EquivalentExchange.Professions.Aurumancer:
                    return "Aurumancer";
                case EquivalentExchange.Professions.Conduit:
                    return "Conduit";
            }

            return "???";
        }

        public static List<string> getProfessionDescription(EquivalentExchange.Professions whichProfession)
        {
            List<string> list = new List<string>();
            AlchemyLevelUpMenu.AddProfessionDescriptions(list, whichProfession);
            return list;
        }

        public static string getProfessionTitleFromNumber(EquivalentExchange.Professions whichProfession)
        {
            return GetProfessionName(whichProfession);
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
        }

        public override void performHoverAction(int x, int y)
        {
        }

        public void getImmediateProfessionPerk(int whichProfession)
        {
        }

        public override void update(GameTime time)
        {
            if (!this.isActive)
            {
                base.exitThisMenu(true);
                return;
            }
            for (int i = this.littleStars.Count - 1; i >= 0; i--)
            {
                if (this.littleStars[i].update(time))
                {
                    this.littleStars.RemoveAt(i);
                }
            }
            if (Game1.random.NextDouble() < 0.03)
            {
                Vector2 position = new Vector2(0f, (float)(Game1.random.Next(this.yPositionOnScreen - Game1.tileSize * 2, this.yPositionOnScreen - Game1.pixelZoom) / (Game1.pixelZoom * 5) * Game1.pixelZoom * 5 + Game1.tileSize / 2));
                if (Game1.random.NextDouble() < 0.5)
                {
                    position.X = (float)Game1.random.Next(this.xPositionOnScreen + this.width / 2 - 57 * Game1.pixelZoom, this.xPositionOnScreen + this.width / 2 - 33 * Game1.pixelZoom);
                }
                else
                {
                    position.X = (float)Game1.random.Next(this.xPositionOnScreen + this.width / 2 + 29 * Game1.pixelZoom, this.xPositionOnScreen + this.width - 40 * Game1.pixelZoom);
                }
                if (position.Y < (float)(this.yPositionOnScreen - Game1.tileSize - Game1.pixelZoom * 2))
                {
                    position.X = (float)Game1.random.Next(this.xPositionOnScreen + this.width / 2 - 29 * Game1.pixelZoom, this.xPositionOnScreen + this.width / 2 + 29 * Game1.pixelZoom);
                }
                position.X = position.X / (float)(Game1.pixelZoom * 5) * (float)Game1.pixelZoom * 5f;
                this.littleStars.Add(new TemporaryAnimatedSprite(Game1.mouseCursors, new Rectangle(364, 79, 5, 5), 80f, 7, 1, position, false, false, 1f, 0f, Color.White, (float)Game1.pixelZoom, 0f, 0f, 0f, false)
                {
                    local = true
                });
            }
            if (this.timerBeforeStart > 0)
            {
                this.timerBeforeStart -= time.ElapsedGameTime.Milliseconds;
                return;
            }
            if (this.isActive && this.isProfessionChooser)
            {
                this.leftProfessionColor = Game1.textColor;
                this.rightProfessionColor = Game1.textColor;
                player.completelyStopAnimatingOrDoingAction();
                player.freezePause = 100;
                if (Game1.getMouseY() > this.yPositionOnScreen + Game1.tileSize * 3 && Game1.getMouseY() < this.yPositionOnScreen + this.height)
                {
                    if (Game1.getMouseX() > this.xPositionOnScreen && Game1.getMouseX() < this.xPositionOnScreen + this.width / 2)
                    {
                        this.leftProfessionColor = Color.Green;
                        if (((Mouse.GetState().LeftButton == ButtonState.Pressed && this.oldMouseState.LeftButton == ButtonState.Released) || (Game1.options.gamepadControls && GamePad.GetState(PlayerIndex.One).IsButtonDown(Buttons.A) && !Game1.oldPadState.IsButtonDown(Buttons.A))) && this.readyToClose())
                        {
                            Alchemy.EnableAlchemistProfession(this.professionsToChoose[0]);
                            this.isActive = false;
                            this.informationUp = false;
                            this.isProfessionChooser = false;
                        }
                    }
                    else if (Game1.getMouseX() > this.xPositionOnScreen + this.width / 2 && Game1.getMouseX() < this.xPositionOnScreen + this.width)
                    {
                        this.rightProfessionColor = Color.Green;
                        if (((Mouse.GetState().LeftButton == ButtonState.Pressed && this.oldMouseState.LeftButton == ButtonState.Released) || (Game1.options.gamepadControls && GamePad.GetState(PlayerIndex.One).IsButtonDown(Buttons.A) && !Game1.oldPadState.IsButtonDown(Buttons.A))) && this.readyToClose())
                        {
                            Alchemy.EnableAlchemistProfession(this.professionsToChoose[1]);
                            this.isActive = false;
                            this.informationUp = false;
                            this.isProfessionChooser = false;
                        }
                    }
                }
                this.height = Game1.tileSize * 8;
            }
            this.oldMouseState = Mouse.GetState();
            
            if (this.isActive && this.informationUp)
            {
                player.completelyStopAnimatingOrDoingAction();
                if (this.okButton.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()) && !this.isProfessionChooser)
                {
                    this.okButton.scale = Math.Min(1.1f, this.okButton.scale + 0.05f);
                    if ((this.oldMouseState.LeftButton == ButtonState.Pressed || (Game1.options.gamepadControls && Game1.oldPadState.IsButtonDown(Buttons.A))) && this.readyToClose())
                    {
                        this.getLevelPerk(this.currentLevel);
                        this.isActive = false;
                        this.informationUp = false;
                    }
                }
                else
                {
                    this.okButton.scale = Math.Max(1f, this.okButton.scale - 0.05f);
                }
                player.freezePause = 100;
            }
        }

        public override void receiveKeyPress(Keys key)
        {
        }

        public void getLevelPerk(int level)
        {
        }

        public override void draw(SpriteBatch b)
        {
            if (this.timerBeforeStart > 0)
            {
                return;
            }
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.5f);
            foreach (TemporaryAnimatedSprite current in this.littleStars)
            {
                current.draw(b, false, 0, 0);
            }
            b.Draw(Game1.mouseCursors, new Vector2((float)(this.xPositionOnScreen + this.width / 2 - 58 * Game1.pixelZoom / 2), (float)(this.yPositionOnScreen - Game1.tileSize / 2 + Game1.pixelZoom * 3)), new Rectangle?(new Rectangle(363, 87, 58, 22)), Color.White, 0f, Vector2.Zero, (float)Game1.pixelZoom, SpriteEffects.None, 1f);

            if (this.informationUp)
            {
                if (this.isProfessionChooser)
                {
                    Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true, null, false);
                    base.drawHorizontalPartition(b, this.yPositionOnScreen + Game1.tileSize * 3, false);
                    base.drawVerticalIntersectingPartition(b, this.xPositionOnScreen + this.width / 2 - Game1.tileSize / 2, this.yPositionOnScreen + Game1.tileSize * 3);
                    Utility.drawWithShadow(b, EquivalentExchange.alchemySkillIcon, new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + IClickableMenu.borderWidth), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize / 4)), this.sourceRectForLevelIcon, Color.White, 0f, Vector2.Zero, (float)Game1.pixelZoom, false, 0.88f, -1, -1, 0.35f);
                    b.DrawString(Game1.dialogueFont, this.title, new Vector2((float)(this.xPositionOnScreen + this.width / 2) - Game1.dialogueFont.MeasureString(this.title).X / 2f, (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize / 4)), Game1.textColor);
                    Utility.drawWithShadow(b, EquivalentExchange.alchemySkillIcon, new Vector2((float)(this.xPositionOnScreen + this.width - IClickableMenu.spaceToClearSideBorder - IClickableMenu.borderWidth - Game1.tileSize), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize / 4)), this.sourceRectForLevelIcon, Color.White, 0f, Vector2.Zero, (float)Game1.pixelZoom, false, 0.88f, -1, -1, 0.35f);
                    b.DrawString(Game1.smallFont, "Choose a profession:", new Vector2((float)(this.xPositionOnScreen + this.width / 2) - Game1.smallFont.MeasureString("Choose a profession:").X / 2f, (float)(this.yPositionOnScreen + Game1.tileSize + IClickableMenu.spaceToClearTopBorder)), Game1.textColor);
                    b.DrawString(Game1.dialogueFont, this.leftProfessionDescription[0], new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + Game1.tileSize / 2), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize * 5 / 2)), this.leftProfessionColor);
                    b.Draw(Game1.mouseCursors, new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + this.width / 2 - Game1.tileSize * 2), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize * 5 / 2 - Game1.tileSize / 4)), new Rectangle?(new Rectangle((int)this.professionsToChoose[0] % 6 * 16, 624 + (int)this.professionsToChoose[0] / 6 * 16, 16, 16)), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
                    for (int i = 1; i < this.leftProfessionDescription.Count<string>(); i++)
                    {
                        b.DrawString(Game1.smallFont, Game1.parseText(this.leftProfessionDescription[i], Game1.smallFont, this.width / 2 - 64), new Vector2((float)(-4 + this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + Game1.tileSize / 2), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize * 5 / 2 + 8 + Game1.tileSize * (i + 1))), this.leftProfessionColor);
                    }
                    b.DrawString(Game1.dialogueFont, this.rightProfessionDescription[0], new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + this.width / 2), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize * 5 / 2)), this.rightProfessionColor);
                    b.Draw(Game1.mouseCursors, new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + this.width - Game1.tileSize * 2), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize * 5 / 2 - Game1.tileSize / 4)), new Rectangle?(new Rectangle((int)this.professionsToChoose[1] % 6 * 16, 624 + (int)this.professionsToChoose[1] / 6 * 16, 16, 16)), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
                    for (int j = 1; j < this.rightProfessionDescription.Count<string>(); j++)
                    {
                        b.DrawString(Game1.smallFont, Game1.parseText(this.rightProfessionDescription[j], Game1.smallFont, this.width / 2 - 48), new Vector2((float)(-4 + this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + this.width / 2), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize * 5 / 2 + 8 + Game1.tileSize * (j + 1))), this.rightProfessionColor);
                    }
                }
                else
                {
                    Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true, null, false);
                    Utility.drawWithShadow(b, EquivalentExchange.alchemySkillIcon, new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + IClickableMenu.borderWidth), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize / 4)), this.sourceRectForLevelIcon, Color.White, 0f, Vector2.Zero, (float)Game1.pixelZoom, false, 0.88f, -1, -1, 0.35f);
                    b.DrawString(Game1.dialogueFont, this.title, new Vector2((float)(this.xPositionOnScreen + this.width / 2) - Game1.dialogueFont.MeasureString(this.title).X / 2f, (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize / 4)), Game1.textColor);
                    Utility.drawWithShadow(b, EquivalentExchange.alchemySkillIcon, new Vector2((float)(this.xPositionOnScreen + this.width - IClickableMenu.spaceToClearSideBorder - IClickableMenu.borderWidth - Game1.tileSize), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize / 4)), this.sourceRectForLevelIcon, Color.White, 0f, Vector2.Zero, (float)Game1.pixelZoom, false, 0.88f, -1, -1, 0.35f);
                    int num = this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + Game1.tileSize * 5 / 4;
                    foreach (string current2 in this.extraInfoForLevel)
                    {
                        b.DrawString(Game1.smallFont, current2, new Vector2((float)(this.xPositionOnScreen + this.width / 2) - Game1.smallFont.MeasureString(current2).X / 2f, (float)num), Game1.textColor);
                        num += Game1.tileSize * 3 / 4;
                    }
                    foreach (CraftingRecipe current3 in this.newCraftingRecipes)
                    {
                        b.DrawString(Game1.smallFont, "New " + (current3.isCookingRecipe ? "cooking" : "crafting") + " recipe: " + current3.name, new Vector2((float)(this.xPositionOnScreen + this.width / 2) - Game1.smallFont.MeasureString("New crafting recipe: " + current3.name).X / 2f - (float)Game1.tileSize, (float)(num + (current3.bigCraftable ? (Game1.tileSize * 3 / 5) : (Game1.tileSize / 5)))), Game1.textColor);
                        current3.drawMenuView(b, (int)((float)(this.xPositionOnScreen + this.width / 2) + Game1.smallFont.MeasureString("New crafting recipe: " + current3.name).X / 2f - (float)(Game1.tileSize * 3 / 4)), num - Game1.tileSize / 4, 0.88f, true);
                        num += (current3.bigCraftable ? (Game1.tileSize * 2) : Game1.tileSize) + Game1.pixelZoom * 2;
                    }
                    this.okButton.draw(b);
                }
                base.drawMouse(b);
            }
        }
    }
}