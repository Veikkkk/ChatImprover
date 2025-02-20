using Microsoft.Xna.Framework.Graphics;
using ReLogic.Localization.IME;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI.Chat;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using Microsoft.Xna.Framework;
using ReLogic.OS;
using Stubble.Core.Imported;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using static Terraria.GameContent.UI.States.UIVirtualKeyboard;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.Map;
using System.Reflection;
using Microsoft.Xna.Framework.Audio;
using Terraria.ID;
using Terraria.Net;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Net.Mime.MediaTypeNames;
using System.Text;

namespace ChatImprover
{
    public class ChatImprover : Mod
    {
        public static ModKeybind CustomKey { get; private set; }
        private static MethodInfo handleCommandMethod;

        private static int backSpaceCount;
        private static float backSpaceRate;

        private float leftArrowRate = 0.5f;  // 左箭头移动的速率
        private float rightArrowRate = 0.5f; // 右箭头移动的速率
        private int leftArrowCount = 5;  // 控制左箭头按下的计数
        private int rightArrowCount = 5; // 控制右箭头按下的计数

        private bool upKeyPressed = false;
        private bool downKeyPressed = false;

        private int lastCompositionStringLength = 0;

        private static int caretPosition = 0;  // 光标位置，初始为 0

        private static string PasteTextIn(bool allowMultiLine, string newKeys)
        {
            newKeys = ((!allowMultiLine) ? (newKeys + Platform.Get<IClipboard>().Value) : (newKeys + Platform.Get<IClipboard>().MultiLineValue));
            return newKeys;
        }
        public override void Load()
        {
            Terraria.On_Main.DrawPlayerChat += DrawPlayerChat;
            Terraria.On_Main.GetInputText += GetInputText;
            Terraria.On_Main.DoUpdate_HandleChat += DoUpdate_HandleChat;

        }

        private void DoUpdate_HandleChat(On_Main.orig_DoUpdate_HandleChat orig)
        {

            if (Main.CurrentInputTextTakerOverride != null)
            {
                Main.drawingPlayerChat = false;
                return;
            }

            if (Main.editSign)
                Main.drawingPlayerChat = false;

            if (PlayerInput.UsingGamepad)
                Main.drawingPlayerChat = false;

            if (!Main.drawingPlayerChat)
            {
                Main.chatMonitor.ResetOffset();
                return;
            }

            int linesOffset = 0;
            if (ChatImproverConfig.GetIsMouseScrollingEnabled())
            {
                int scrollSpeed = 1;
                if (PlayerInput.ScrollWheelDeltaForUI > 0)
                    linesOffset = scrollSpeed;
                else if (PlayerInput.ScrollWheelDeltaForUI < 0)
                    linesOffset = -scrollSpeed;
                if (linesOffset != 0)
                    Main.chatMonitor.Offset(linesOffset);
            }

            if (ChatImproverConfig.GetIsPageNavigationEnabled())
            {
                if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up) && !upKeyPressed)
                {
                    upKeyPressed = true;
                    linesOffset = 10;
                }
                else if (!Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up))
                {
                    upKeyPressed = false;
                }
                if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down) && !downKeyPressed)
                {
                    downKeyPressed = true;
                    linesOffset = -10;
                }
                else if (!Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down))
                {
                    downKeyPressed = false;
                }
                Main.chatMonitor.Offset(linesOffset);
            }

            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                Main.drawingPlayerChat = false;

            string text = Main.chatText;
            Main.chatText = GetInputText(null, Main.chatText);
            int num = 470;
            num = (int)((float)Main.screenWidth * (1f / Main.UIScale)) - 330;
            if (text != Main.chatText)
            {
                for (float x = ChatManager.GetStringSize(FontAssets.MouseText.Value, Main.chatText, Vector2.One).X; x > (float)num; x = ChatManager.GetStringSize(FontAssets.MouseText.Value, Main.chatText, Vector2.One).X)
                {
                    int num2 = Math.Max(0, (int)(x - (float)num) / 100);
                    Main.chatText = Main.chatText.Substring(0, Main.chatText.Length - 1 - num2);
                }
            }

            if (text != Main.chatText)
                SoundEngine.PlaySound(SoundID.MenuTick);

            if (!Main.inputTextEnter || !Main.chatRelease)
                return;

            //指令类访问不了,让他自己处理吧
            bool handled = Main.chatText.Length > 0 && Main.chatText[0] == '/';
            if (handled)
            {
                orig();
                return;
            }

            if (Main.chatText != "")
            {
                ChatMessage message = ChatManager.Commands.CreateOutgoingMessage(Main.chatText);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    ChatHelper.SendChatMessageFromClient(message);
                else if (Main.netMode == NetmodeID.SinglePlayer)
                    ChatManager.Commands.ProcessIncomingMessage(message, Main.myPlayer);
            }


            Main.chatText = "";
            caretPosition = 0;
            Main.ClosePlayerChat();
            Main.chatRelease = false;
            SoundEngine.PlaySound(SoundID.MenuClose);

        }

        private string GetInputText(On_Main.orig_GetInputText orig, string oldString, bool allowMultiLine = false)
        {
            if (!Main.drawingPlayerChat)
            {
                return orig(oldString, allowMultiLine);
            }

            if (Main.dedServ || !Main.hasFocus) return Main.dedServ ? "" : oldString;

            Main.inputTextEnter = false;
            Main.inputTextEscape = false;
            string text = oldString ?? "";
            string text2 = "";

            bool isCtrlPressed = Main.inputText.IsKeyDown(Keys.LeftControl) || Main.inputText.IsKeyDown(Keys.RightControl);
            bool isAltPressed = Main.inputText.IsKeyDown(Keys.LeftAlt) || Main.inputText.IsKeyDown(Keys.RightAlt);

            if (isCtrlPressed && !isAltPressed)
            {
                if (Main.inputText.IsKeyDown(Keys.Z) && !Main.oldInputText.IsKeyDown(Keys.Z)) text = "";
                else if (Main.inputText.IsKeyDown(Keys.X) && !Main.oldInputText.IsKeyDown(Keys.X)) { Platform.Get<IClipboard>().Value = oldString; text = ""; }
                else if ((Main.inputText.IsKeyDown(Keys.C) || Main.inputText.IsKeyDown(Keys.Insert)) && !Main.oldInputText.IsKeyDown(Keys.C)) Platform.Get<IClipboard>().Value = oldString;
                else if (Main.inputText.IsKeyDown(Keys.V) && !Main.oldInputText.IsKeyDown(Keys.V)) text2 = PasteTextIn(allowMultiLine, text2);
            }

            else if (Main.inputText.PressingShift())
            {
                if (Main.inputText.IsKeyDown(Keys.Delete) && !Main.oldInputText.IsKeyDown(Keys.Delete)) { Platform.Get<IClipboard>().Value = oldString; text = ""; }
                if (Main.inputText.IsKeyDown(Keys.Insert) && !Main.oldInputText.IsKeyDown(Keys.Insert)) text2 = PasteTextIn(allowMultiLine, text2);
            }


            if (ChatImproverConfig.GetIsCaretMovable())
            {
                HandleCursorMovement(text);
            }

            for (int i = 0; i < Main.keyCount; i++)
            {
                int num = Main.keyInt[i];
                string key = Main.keyString[i];
                if (num == 13) Main.inputTextEnter = true;
                else if (num == 27) Main.inputTextEscape = true;
                else if (num >= 32 && num != 127) text2 += key;
            }

            Main.keyCount = 0;

            // 只有在有新输入时才执行插入
            if (text2.Length > 0)
            {
                text = text.Insert(caretPosition, text2);
                caretPosition += text2.Length;  // 更新光标位置
            }

            Main.oldInputText = Main.inputText;
            Main.inputText = Keyboard.GetState();

            HandleBackspace(ref text);
            return text;
        }

        private void HandleBackspace(ref string text)
        {
            var pressedKeys = Main.inputText.GetPressedKeys();
            var oldPressedKeys = Main.oldInputText.GetPressedKeys();
            bool flag = false;
            if (Main.inputText.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Back) && Main.oldInputText.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Back))
            {
                backSpaceRate -= 0.05f;
                if (backSpaceRate < 0f)
                    backSpaceRate = 0f;

                if (backSpaceCount <= 0)
                {
                    backSpaceCount = (int)Math.Round(backSpaceRate);
                    flag = true;
                }

                backSpaceCount--;
            }
            else
            {
                backSpaceRate = 7f;
                backSpaceCount = 15;
            }
            for (int j = 0; j < pressedKeys.Length; j++)
            {
                bool flag2 = true;
                for (int k = 0; k < oldPressedKeys.Length; k++)
                {
                    if (pressedKeys[j] == oldPressedKeys[k])
                        flag2 = false;
                }

                bool canDeleteContent;
                if (ChatImproverConfig.GetIsImeDeleteFixEnabled())
                {
                    canDeleteContent = lastCompositionStringLength < 1;
                }
                else
                {
                    canDeleteContent = true;
                }

                if (string.Concat(pressedKeys[j]) == "Back" && (flag2 || flag) && text.Length > 0 && canDeleteContent)
                {
                    TextSnippet[] array = ChatManager.ParseMessage(text, Microsoft.Xna.Framework.Color.White).ToArray();
                    if (!array[array.Length - 1].DeleteWhole)
                    {
                        text = text.Remove(caretPosition - 1, 1);
                        caretPosition = Math.Max(0, --caretPosition);
                    }
                    else
                    {
                        int deleteLength = array[array.Length - 1].TextOriginal.Length;
                        text = text.Remove(Math.Max(0, caretPosition - deleteLength), deleteLength);

                        caretPosition = Math.Max(0, caretPosition - deleteLength);
                    }
                }

                string compositionString = Platform.Get<IImeService>().CompositionString;
                if (compositionString != null) lastCompositionStringLength = compositionString.Length;
            }
        }

        private void HandleCursorMovement(string text)
        {
            var pressedKeys = Main.inputText.GetPressedKeys();
            var oldPressedKeys = Main.oldInputText.GetPressedKeys();
            bool flag = false;

            // 左箭头（向左移动光标）
            if (Main.inputText.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Left) && Main.oldInputText.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Left))
            {
                leftArrowRate -= 0.05f;
                if (leftArrowRate < 0f)
                    leftArrowRate = 0f;

                if (leftArrowCount <= 0)
                {
                    leftArrowCount = (int)Math.Round(leftArrowRate);
                    flag = true;
                }

                leftArrowCount--;
            }
            else
            {
                leftArrowRate = 7f;
                leftArrowCount = 15;
            }

            // 右箭头（向右移动光标）
            if (Main.inputText.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Right) && Main.oldInputText.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Right))
            {
                rightArrowRate -= 0.05f;
                if (rightArrowRate < 0f)
                    rightArrowRate = 0f;

                if (rightArrowCount <= 0)
                {
                    rightArrowCount = (int)Math.Round(rightArrowRate);
                    flag = true;
                }

                rightArrowCount--;
            }
            else
            {
                rightArrowRate = 7f;
                rightArrowCount = 15;
            }

            // 处理光标的移动：检查是否按下左或右箭头
            for (int j = 0; j < pressedKeys.Length; j++)
            {
                bool flag2 = true;
                for (int k = 0; k < oldPressedKeys.Length; k++)
                {
                    if (pressedKeys[j] == oldPressedKeys[k])
                        flag2 = false;
                }

                bool canMoveCursor = text.Length > 0;

                if (string.Concat(pressedKeys[j]) == "Left" && (flag2 || flag) && caretPosition > 0 && canMoveCursor)
                {

                    caretPosition = Math.Max(0, caretPosition - 1); // 左移光标
                }
                else if (string.Concat(pressedKeys[j]) == "Right" && (flag2 || flag) && caretPosition < text.Length && canMoveCursor)
                {
                    caretPosition = Math.Min(text.Length, caretPosition + 1); // 右移光标
                }
            }
        }

        private void DrawPlayerChat(On_Main.orig_DrawPlayerChat orig, Main self)
        {
            TextSnippet[] array = null;
            if (Main.drawingPlayerChat)
                PlayerInput.WritingText = true;

            Main.instance.HandleIME();
            if (Main.drawingPlayerChat)
            {
                Main.instance.textBlinkerCount++;
                if (Main.instance.textBlinkerCount >= 20)
                {
                    if (Main.instance.textBlinkerState == 0)
                        Main.instance.textBlinkerState = 1;
                    else
                        Main.instance.textBlinkerState = 0;

                    Main.instance.textBlinkerCount = 0;
                }

                if (Main.screenWidth > 800)
                {
                    int num = Main.screenWidth - 300;
                    int num2 = 78;
                    Main.spriteBatch.Draw(TextureAssets.TextBack.Value, new Vector2(num2, Main.screenHeight - 36), new Microsoft.Xna.Framework.Rectangle(0, 0, TextureAssets.TextBack.Width() - 100, TextureAssets.TextBack.Height()), new Microsoft.Xna.Framework.Color(100, 100, 100, 100), 0f, default(Vector2), 1f, SpriteEffects.None, 0f);
                    num -= 400;
                    num2 += 400;
                    while (num > 0)
                    {
                        if (num > 300)
                        {
                            Main.spriteBatch.Draw(TextureAssets.TextBack.Value, new Vector2(num2, Main.screenHeight - 36), new Microsoft.Xna.Framework.Rectangle(100, 0, TextureAssets.TextBack.Width() - 200, TextureAssets.TextBack.Height()), new Microsoft.Xna.Framework.Color(100, 100, 100, 100), 0f, default(Vector2), 1f, SpriteEffects.None, 0f);
                            num -= 300;
                            num2 += 300;
                        }
                        else
                        {
                            Main.spriteBatch.Draw(TextureAssets.TextBack.Value, new Vector2(num2, Main.screenHeight - 36), new Microsoft.Xna.Framework.Rectangle(TextureAssets.TextBack.Width() - num, 0, TextureAssets.TextBack.Width() - (TextureAssets.TextBack.Width() - num), TextureAssets.TextBack.Height()), new Microsoft.Xna.Framework.Color(100, 100, 100, 100), 0f, default(Vector2), 1f, SpriteEffects.None, 0f);
                            num = 0;
                        }
                    }
                }
                else
                {
                    Main.spriteBatch.Draw(TextureAssets.TextBack.Value, new Vector2(78f, Main.screenHeight - 36), new Microsoft.Xna.Framework.Rectangle(0, 0, TextureAssets.TextBack.Width(), TextureAssets.TextBack.Height()), new Microsoft.Xna.Framework.Color(100, 100, 100, 100), 0f, default(Vector2), 1f, SpriteEffects.None, 0f);
                }
                int hoveredSnippet = -1;

                /*                StringBuilder sb = new StringBuilder(Main.chatText);

                                //光标闪烁处理
                                string insertText = Main.instance.textBlinkerState == 1 ? "|" : " ";
                                if (string.IsNullOrEmpty(text))
                                {
                                    text = insertText;
                                }
                                else if (caretPosition >= 0 && caretPosition <= text.Length)
                                {
                                    lock (caretPositionLock)
                                    {
                                        text = text.Insert(caretPosition, insertText);
                                    }
                                }

                                //IME字符串处理
                                string compositionString = Platform.Get<IImeService>().CompositionString;
                                if (compositionString != null && compositionString.Length > 0)
                                    text = text.Insert(caretPosition, $"[c/FFF014:{compositionString}]");*/
                StringBuilder sb = new StringBuilder(Main.chatText);

                // 光标闪烁处理
                string insertText = Main.instance.textBlinkerState == 1 ? "|" : " ";
                if (string.IsNullOrEmpty(sb.ToString()) || !ChatImproverConfig.GetIsCaretMovable())
                {
                    sb.Append(insertText);
                }
                else if (caretPosition >= 0 && caretPosition <= sb.Length)
                {
                    sb.Insert(caretPosition, insertText);
                }

                // IME 字符串处理
                string compositionString = Platform.Get<IImeService>().CompositionString;
                if (compositionString != null && compositionString.Length > 0)
                {
                    sb.Insert(caretPosition, $"[c/FFF014:{compositionString}]");
                }

                List<TextSnippet> list = ChatManager.ParseMessage(sb.ToString(), Microsoft.Xna.Framework.Color.White);

                array = list.ToArray();
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, FontAssets.MouseText.Value, array, new Vector2(88f, Main.screenHeight - 30), 0f, Vector2.Zero, Vector2.One, out hoveredSnippet);
                if (hoveredSnippet > -1)
                {
                    array[hoveredSnippet].OnHover();
                    if (Main.mouseLeft && Main.mouseLeftRelease)
                        array[hoveredSnippet].OnClick();
                }
            }

            Main.chatMonitor.DrawChat(Main.drawingPlayerChat);
            if (Main.drawingPlayerChat && array != null)
            {
                Vector2 stringSize = ChatManager.GetStringSize(FontAssets.MouseText.Value, array, Vector2.Zero);
                Main.instance.DrawWindowsIMEPanel(new Vector2(88f, Main.screenHeight - 30) + new Vector2(stringSize.X + 10f, -6f));
            }

            TimeLogger.DetailedDrawTime(10);
        }

        public override void Unload()
        {
            // 卸载时解除注册
            CustomKey = null;
        }

        private static bool ShouldOverrideChat()
        {
            return true;
        }

    }
}
