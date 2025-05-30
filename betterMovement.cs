using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using HarmonyLib;
using GenericModConfigMenu;

namespace BetterMovement {
   
   public class ModConfig {
      public KeybindList ToggleKey { get; set; } = KeybindList.Parse("Space");
   }
   
   public class BetterMovement : Mod {
      
      private ModConfig Config;
      
      public override void Entry(IModHelper helper) {
         Config = helper.ReadConfig<ModConfig>();
         
         helper.Events.Input.ButtonPressed += OnButtonPressed;
         helper.Events.Input.ButtonsChanged += OnButtonsChanged;
         Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
         
         Harmony harmony = new(ModManifest.UniqueID);
         harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.GetKeyboardState)),
            postfix: new HarmonyMethod(typeof(BetterMovement), nameof(GetKeyboardState_Postfix))
         );
      }
      
      private void OnGameLaunched(object sender, GameLaunchedEventArgs e) {
         // get Generic Mod Config Menu's API (if it's installed)
         var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
         if (configMenu is null) return;

         configMenu.Register(
           mod: ModManifest,
           reset: () => Config = new ModConfig(),
           save: () => Helper.WriteConfig(Config)
         );

         configMenu.AddKeybindList(
           mod: ModManifest,
           name: () => "Toggle Key",
           getValue: () => Config.ToggleKey,
           setValue: value => Config.ToggleKey = value
         );
      }
      
      public static bool isMoving = false;
      public static List<Keys> simKeys = new List<Keys>();
      
      public static void GetKeyboardState_Postfix (ref KeyboardState __result) {
         if (isMoving) {
            List<Keys> pressedKeys = new List<Keys>(__result.GetPressedKeys());
            pressedKeys.AddRange(simKeys);
            __result = new KeyboardState(pressedKeys.ToArray());
         }
      }
      
      private bool newMovementScheme = true;
      private SButton forward;
      
      private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e) {
         if (Config.ToggleKey.JustPressed()) {
            newMovementScheme = !newMovementScheme;
         }
      }
      
      private void OnButtonPressed(object sender, ButtonPressedEventArgs e) {
         if (!Context.CanPlayerMove)
            return;
         
         switch(e.Button) {
            case SButton.DPadUp:
               Helper.Input.Suppress(e.Button);
               Game1.player.faceDirection(0);
               break;
            case SButton.DPadRight:
               Helper.Input.Suppress(e.Button);
               Game1.player.faceDirection(1);
               break;
            case SButton.DPadDown:
               Helper.Input.Suppress(e.Button);
               Game1.player.faceDirection(2);
               break;
            case SButton.DPadLeft:
               Helper.Input.Suppress(e.Button);
               Game1.player.faceDirection(3);
               break;
         }
         
         if (newMovementScheme) {
            if (  Game1.options.moveLeftButton.Any(p => p.ToSButton() == e.Button) || 
                  Game1.options.moveRightButton.Any(p => p.ToSButton() == e.Button) || 
                  Game1.options.moveDownButton.Any(p => p.ToSButton() == e.Button)
               )
            {
               Helper.Input.Suppress(e.Button);
            }
            
            if (Game1.options.moveUpButton.Any(p => p.ToSButton() == e.Button)) {
               Helper.Input.Suppress(e.Button);
               forward = e.Button;
               isMoving = true;
               Helper.Events.GameLoop.UpdateTicking += OnTick;
            }
         }
      }
      
      private void OnTick(object sender, UpdateTickingEventArgs e) {
         if (!Helper.Input.IsSuppressed(forward)) {
            isMoving = false;
            Helper.Events.GameLoop.UpdateTicking -= OnTick;
            return;
         }
         
         //vector pointing from the player to the cursor
         ICursorPosition cp = Helper.Input.GetCursorPosition();
         Vector2 d = cp.Tile - Game1.player.Tile;
         
         //tan(22.5deg)
         const float t = 0.4142f;
         
         const int edgeMargin = 5;
         
         simKeys.Clear();
         
         if (d == Vector2.Zero) {
            if (cp.ScreenPixels.X < edgeMargin)
               simKeys.Add(Game1.options.moveLeftButton[0].key);
            
            if (cp.ScreenPixels.Y < edgeMargin)
               simKeys.Add(Game1.options.moveUpButton[0].key);
            
            if (cp.ScreenPixels.X > Game1.viewport.Width - edgeMargin)
               simKeys.Add(Game1.options.moveRightButton[0].key);
            
            if (cp.ScreenPixels.Y > Game1.viewport.Height - edgeMargin)
               simKeys.Add(Game1.options.moveDownButton[0].key);
            
         } else if (d.Y > d.X*t) {
            // lower half
            if (d.X > -d.Y*t) {
               // lower right quadrant
               if (d.X > d.Y*t) {
                  simKeys.Add(Game1.options.moveDownButton[0].key);
                  simKeys.Add(Game1.options.moveRightButton[0].key);
               } else {
                  simKeys.Add(Game1.options.moveDownButton[0].key);
               }
            } else {
               // lower left quadrant
               if (d.Y > -d.X*t) {
                  simKeys.Add(Game1.options.moveDownButton[0].key);
                  simKeys.Add(Game1.options.moveLeftButton[0].key);
               } else {
                  simKeys.Add(Game1.options.moveLeftButton[0].key);
               }
            }
         } else {
            // upper half
            if (d.X > -d.Y*t) {
               // upper right quadrant
               if (d.Y > -d.X*t) {
                  simKeys.Add(Game1.options.moveRightButton[0].key);
               } else {
                  simKeys.Add(Game1.options.moveUpButton[0].key);
                  simKeys.Add(Game1.options.moveRightButton[0].key);
               }
            } else {
               // upper left quadrant
               if (d.X > d.Y*t) {
                  simKeys.Add(Game1.options.moveUpButton[0].key);
               } else {
                  simKeys.Add(Game1.options.moveUpButton[0].key);
                  simKeys.Add(Game1.options.moveLeftButton[0].key);
               }
            }
         }
      }
   }
}