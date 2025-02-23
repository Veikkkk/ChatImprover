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
using Iced.Intel;
using Terraria.WorldBuilding;
using System.Xml.Linq;
using ReLogic.Graphics;


namespace ChatImprover
{
    public class ChatImprover : Mod
    {
        //反射字段复用
        private static MethodInfo handleCommandMethod;
        FieldInfo field_startChatLine;
        FieldInfo field_messages;

        //退格速率
        private static int backSpaceCount;
        private static float backSpaceRate;

        //鼠标滚动速率
        private float leftArrowRate = 0.5f;
        private float rightArrowRate = 0.5f;
        private int leftArrowCount = 5;
        private int rightArrowCount = 5;

        private bool upKeyPressed = false;
        private bool downKeyPressed = false;

        //修复输入法删除bug
        private int lastCompositionStringLength = 0;

        //光标位置
        private static int caretPosition = 0;

        //聊天记录行数
        private static int lineCount = 1;

        public override void Load()
        {
            //Hook位置
            Terraria.On_Main.DrawPlayerChat += DrawPlayerChat;
            Terraria.On_Main.GetInputText += GetInputText;
            Terraria.On_Main.DoUpdate_HandleChat += DoUpdate_HandleChat;
            Terraria.GameContent.UI.Chat.On_NameTagHandler.Terraria_UI_Chat_ITagHandler_Parse += NameTagParse; ;
            Terraria.GameContent.UI.Chat.On_RemadeChatMonitor.DrawChat += DrawChat;

            //反射预处理
            field_startChatLine = typeof(RemadeChatMonitor).GetField("_startChatLine", BindingFlags.NonPublic | BindingFlags.Instance);
            field_messages = typeof(RemadeChatMonitor).GetField("_messages", BindingFlags.NonPublic | BindingFlags.Instance);

        }

        //读取剪贴板
        private static string PasteTextIn(bool allowMultiLine, string newKeys)
        {
            newKeys = ((!allowMultiLine) ? (newKeys + Platform.Get<IClipboard>().Value) : (newKeys + Platform.Get<IClipboard>().MultiLineValue));
            return newKeys;
        }

        //绘制聊天记录
        private void DrawChat(On_RemadeChatMonitor.orig_DrawChat orig, RemadeChatMonitor self, bool drawingPlayerChat)
        {
            /*            FieldInfo fieldInfo = typeof(RemadeChatMonitor).GetField("_showCount", BindingFlags.NonPublic | BindingFlags.Instance);
                        int _showCount = (int)fieldInfo.GetValue(Main.chatMonitor);*/
            int _showCount = ChatImproverConfig.GetshowCount();

            int _startChatLine = (int)field_startChatLine.GetValue(Main.chatMonitor);

            List<ChatMessageContainer> _messages = (List<ChatMessageContainer>)field_messages.GetValue(Main.chatMonitor);

            int remainingChatLines = _startChatLine;
            int messageIndex = 0;
            int lineOffsetInMessage = 0;

            // 计算起始消息和行数
            while (remainingChatLines > 0 && messageIndex < _messages.Count)
            {
                int availableLines = Math.Min(remainingChatLines, _messages[messageIndex].LineCount);
                remainingChatLines -= availableLines;
                lineOffsetInMessage += availableLines;

                if (lineOffsetInMessage == _messages[messageIndex].LineCount)
                {
                    lineOffsetInMessage = 0;
                    messageIndex++;
                }
            }

            int displayedMessages = 0;
            int? hoveredMessageIndex = null;
            int snippetIndex = -1;
            int? hoveredSnippetIndex = null;
            int hoveredSnippet = -1;

            // 预先计算好屏幕高度
            float screenHeight = Main.screenHeight;
            float baseY = screenHeight - 28 * lineCount - 28;

            // 遍历并绘制聊天消息
            float NameLength = 0;
            while (displayedMessages < _showCount && messageIndex < _messages.Count)
            {
                ChatMessageContainer chatMessageContainer = _messages[messageIndex];
                if (!chatMessageContainer.Prepared || !(drawingPlayerChat || chatMessageContainer.CanBeShownWhenChatIsClosed))
                    break;

                if (NameLength == 0)
                {
                    TextSnippet[] firstSnippet = chatMessageContainer.GetSnippetWithInversedIndex(chatMessageContainer.LineCount - 1);
                    if (firstSnippet.Length > 1 && firstSnippet[0].TextOriginal.StartsWith("[n:") && firstSnippet[0].TextOriginal.EndsWith("]"))
                    {
                        TextSnippet space = new TextSnippet(" ");
                        DynamicSpriteFont font = FontAssets.MouseText.Value;
                        NameLength = firstSnippet[0].GetStringLength(font) + space.GetStringLength(font);
                    }
                }


                TextSnippet[] snippetWithInversedIndex = chatMessageContainer.GetSnippetWithInversedIndex(lineOffsetInMessage);
                float horOffset = (lineOffsetInMessage != chatMessageContainer.LineCount - 1) ? NameLength : 0f;
                Vector2 drawPosition = new Vector2(88f + horOffset, baseY - displayedMessages * 21);
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, FontAssets.MouseText.Value, snippetWithInversedIndex, drawPosition, 0f, Vector2.Zero, Vector2.One, out hoveredSnippet);

                if (hoveredSnippet >= 0)
                {
                    hoveredSnippetIndex = hoveredSnippet;
                    hoveredMessageIndex = messageIndex;
                    snippetIndex = lineOffsetInMessage;
                }

                displayedMessages++;
                lineOffsetInMessage++;

                if (lineOffsetInMessage >= chatMessageContainer.LineCount)
                {
                    lineOffsetInMessage = 0;
                    NameLength = 0;
                    messageIndex++;
                }
            }

            if (hoveredMessageIndex.HasValue && hoveredSnippetIndex.HasValue)
            {
                TextSnippet[] snippetWithInversedIndex2 = _messages[hoveredMessageIndex.Value].GetSnippetWithInversedIndex(snippetIndex);
                snippetWithInversedIndex2[hoveredSnippetIndex.Value].OnHover();
                if (Main.mouseLeft && Main.mouseLeftRelease)
                    snippetWithInversedIndex2[hoveredSnippetIndex.Value].OnClick();
            }
        }

        //玩家名格式和颜色
        private TextSnippet NameTagParse(On_NameTagHandler.orig_Terraria_UI_Chat_ITagHandler_Parse orig, NameTagHandler self, string text, Color baseColor, string options)
        {
            string processedText = text.Replace("\\[", "[").Replace("\\]", "]");
            string finalText = ChatImproverConfig.GetLeftSymbol() + processedText + ChatImproverConfig.GetRightSymbol();
            return new TextSnippet(finalText, new Color(
                Convert.ToByte(Convert.ToInt32(ChatImproverConfig.GetnameColor().Substring(1, 2), 16)),
                Convert.ToByte(Convert.ToInt32(ChatImproverConfig.GetnameColor().Substring(3, 2), 16)),
                Convert.ToByte(Convert.ToInt32(ChatImproverConfig.GetnameColor().Substring(5, 2), 16))
            ));
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

            //鼠标滚动
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

            //上下键翻页
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

            //换行
            bool isCtrlPressed = Main.inputText.IsKeyDown(Keys.LeftControl) || Main.inputText.IsKeyDown(Keys.RightControl);

            if (isCtrlPressed)
            {
                if (Main.inputText.IsKeyDown(Keys.Enter) && !Main.oldInputText.IsKeyDown(Keys.Enter))
                {
                    Main.chatText = Main.chatText.Insert(caretPosition, "\n");
                    caretPosition += 1;
                    lineCount += 1;
                    return;
                }
            }

            if (text != Main.chatText)
            {
                SoundEngine.PlaySound(SoundID.MenuTick);
                lineCount = Main.chatText.Split('\n').Length;
            }


            if (!Main.inputTextEnter || !Main.chatRelease)
                return;

            bool handled = Main.chatText.Length > 0 && Main.chatText[0] == '/' && HandleCommand(Main.chatText);
            if (Main.chatText != "" && !handled)
            {
                ChatMessage message = ChatManager.Commands.CreateOutgoingMessage(Main.chatText);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    ChatHelper.SendChatMessageFromClient(message);
                else if (Main.netMode == NetmodeID.SinglePlayer)
                    ChatManager.Commands.ProcessIncomingMessage(message, Main.myPlayer);
            }


            Main.chatText = "";
            caretPosition = 0;
            lineCount = 1;
            Main.ClosePlayerChat();
            Main.chatRelease = false;
            SoundEngine.PlaySound(SoundID.MenuClose);

        }

        //发送指令
        private bool HandleCommand(string chatText)
        {
            MethodInfo method = typeof(CommandLoader).GetMethod("HandleCommand", BindingFlags.NonPublic | BindingFlags.Static);

            if (method != null)
            {
                object[] parameters = new object[]
                {
                chatText,
                new ChatCommandCaller()
                };
                var result = (bool)method.Invoke(null, parameters);
                return result;
            }
            else
            {
                return false;
            }
        }

        internal class ChatCommandCaller : CommandCaller
        {
            public CommandType CommandType => CommandType.Chat;
            public Player Player => Main.player[Main.myPlayer];

            public void Reply(string text, Color color = default(Color))
            {
                if (color == default(Color))
                    color = Color.White;
                foreach (var line in text.Split('\n'))
                    Main.NewText(line, color.R, color.G, color.B);
            }
        }

        private string GetInputText(On_Main.orig_GetInputText orig, string oldString, bool allowMultiLine = false)
        {
            try
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
                    else if (Main.inputText.IsKeyDown(Keys.V) && !Main.oldInputText.IsKeyDown(Keys.V))
                    {
                        text2 = PasteTextIn(true, text2);
                        int num = 470;
                        num = (int)(Main.screenWidth * (1f / Main.UIScale)) - 330;
                        string[] lines = text2.Split('\n'); // 按行分割文本
                        text2 = string.Join("\n", SplitTextArray(lines, num, FontAssets.MouseText.Value));

                    }
                }

                else if (Main.inputText.PressingShift())
                {
                    if (Main.inputText.IsKeyDown(Keys.Delete) && !Main.oldInputText.IsKeyDown(Keys.Delete)) { Platform.Get<IClipboard>().Value = oldString; text = ""; }
                    if (Main.inputText.IsKeyDown(Keys.Insert) && !Main.oldInputText.IsKeyDown(Keys.Insert)) text2 = PasteTextIn(allowMultiLine, text2);
                }


                //光标移动
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

                if (text2.Length > 0)
                {
                    text = text.Insert(caretPosition, text2);
                    caretPosition += text2.Length;
                }

                Main.oldInputText = Main.inputText;
                Main.inputText = Keyboard.GetState();

                //退格处理
                HandleBackspace(ref text, isCtrlPressed);
                return text;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                return "";
            }
        }
        public List<string> SplitTextArray(string[] texts, float maxWidth, DynamicSpriteFont font)
        {
            List<string> result = new List<string>();

            foreach (string text in texts)
            {
                string textCopy = text;
                while (!string.IsNullOrEmpty(textCopy))
                {
                    int bestSplit = textCopy.Length;

                    // 从头开始寻找最长不超过 maxWidth 的部分
                    for (int i = 1; i <= textCopy.Length; i++)
                    {
                        string subText = textCopy.Substring(0, i);
                        float width = ChatManager.GetStringSize(font, subText, Vector2.One).X;

                        if (width > maxWidth)
                        {
                            bestSplit = i - 1;
                            break;
                        }
                    }

                    // 添加符合宽度的部分到结果
                    result.Add(textCopy.Substring(0, bestSplit));

                    // 剩余部分继续循环
                    textCopy = textCopy.Substring(bestSplit);
                }
            }

            return result;
        }

        private void HandleBackspace(ref string text, bool isCtrlPressed)
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
                        if (isCtrlPressed)
                        {
                            string textBeforeCaret = text.Substring(0, caretPosition - 1);
                            textBeforeCaret = textBeforeCaret.TrimEnd();
                            int spaceIndex = textBeforeCaret.LastIndexOf(' ', textBeforeCaret.Length - 1);
                            if (spaceIndex >= 0)
                            {
                                text = text.Remove(spaceIndex + 1, caretPosition - spaceIndex - 1);
                                caretPosition = spaceIndex + 1;
                            }
                            else
                            {
                                text = text.Remove(0, caretPosition);
                                caretPosition = 0;
                            }
                        }
                        else
                        {
                            text = text.Remove(caretPosition - 1, 1);
                            caretPosition = Math.Max(0, --caretPosition);
                        }
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

                    caretPosition = Math.Max(0, caretPosition - 1);
                }
                else if (string.Concat(pressedKeys[j]) == "Right" && (flag2 || flag) && caretPosition < text.Length && canMoveCursor)
                {
                    caretPosition = Math.Min(text.Length, caretPosition + 1);
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

                int width = Main.screenWidth - 300;
                int height = lineCount * 28;
                int startX = 78;
                int startY = Main.screenHeight - 6 - height;//- 36
                Color panelColor = new Color(100, 100, 100, 100);
                DrawBackgroundPanel(startX, startY, width, height, TextureAssets.TextBack.Value, panelColor);


                int hoveredSnippet = -1;
                StringBuilder sb = new StringBuilder(Main.chatText);

                //光标闪烁
                string insertText = Main.instance.textBlinkerState == 1 ? "|" : " ";
                if (string.IsNullOrEmpty(sb.ToString()) || !ChatImproverConfig.GetIsCaretMovable())
                {
                    sb.Append(insertText);
                }
                else if (caretPosition >= 0 && caretPosition <= sb.Length)
                {
                    sb.Insert(caretPosition, insertText);
                }

                string compositionString = Platform.Get<IImeService>().CompositionString;
                if (compositionString != null && compositionString.Length > 0)
                {
                    sb.Insert(caretPosition, $"[c/FFF014:{compositionString}]");
                }

                //处理Tag
                List<TextSnippet> list = ChatManager.ParseMessage(sb.ToString(), Microsoft.Xna.Framework.Color.White);
                array = list.ToArray();

                //绘制框内文本
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, FontAssets.MouseText.Value, array, new Vector2(88f, Main.screenHeight - height), 0f, Vector2.Zero, Vector2.One, out hoveredSnippet);//Main.screenHeight - 30
                if (hoveredSnippet > -1)
                {
                    array[hoveredSnippet].OnHover();
                    if (Main.mouseLeft && Main.mouseLeftRelease)
                        array[hoveredSnippet].OnClick();
                }
            }

            //绘制聊天记录
            Main.chatMonitor.DrawChat(Main.drawingPlayerChat);
            if (Main.drawingPlayerChat && array != null)
            {
                Vector2 stringSize = ChatManager.GetStringSize(FontAssets.MouseText.Value, array, Vector2.Zero);
                Main.instance.DrawWindowsIMEPanel(new Vector2(88f, Main.screenHeight - 30) + new Vector2(stringSize.X + 10f, -6f));
            }
            TimeLogger.DetailedDrawTime(10);
        }

        //九宫格绘制贴图法
        void DrawBackgroundPanel(int startX, int startY, int width, int height, Texture2D texture, Color color)
        {
            int cornerSize = 10; // 角落固定大小
            int edgeWidth = texture.Width - (cornerSize * 2); // 横向边缘宽度
            int edgeHeight = texture.Height - (cornerSize * 2); // 纵向边缘高度

            Main.spriteBatch.Draw(texture, new Vector2(startX, startY),
    new Rectangle(0, 0, cornerSize, cornerSize),
    color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(texture, new Rectangle(startX + cornerSize, startY, width - (cornerSize * 2), cornerSize),
    new Rectangle(cornerSize, 0, edgeWidth, cornerSize),
    color);

            Main.spriteBatch.Draw(texture, new Vector2(startX + width - cornerSize, startY),
    new Rectangle(texture.Width - cornerSize, 0, cornerSize, cornerSize),
    color);

            Main.spriteBatch.Draw(texture, new Rectangle(startX, startY + cornerSize, cornerSize, height - (cornerSize * 2)),
    new Rectangle(0, cornerSize, cornerSize, edgeHeight),
    color);

            Main.spriteBatch.Draw(texture, new Rectangle(startX + cornerSize, startY + cornerSize, width - (cornerSize * 2), height - (cornerSize * 2)),
    new Rectangle(cornerSize, cornerSize, edgeWidth, edgeHeight),
    color);

            Main.spriteBatch.Draw(texture, new Rectangle(startX + width - cornerSize, startY + cornerSize, cornerSize, height - (cornerSize * 2)),
    new Rectangle(texture.Width - cornerSize, cornerSize, cornerSize, edgeHeight),
    color);

            Main.spriteBatch.Draw(texture, new Vector2(startX, startY + height - cornerSize),
    new Rectangle(0, texture.Height - cornerSize, cornerSize, cornerSize),
    color);

            Main.spriteBatch.Draw(texture, new Rectangle(startX + cornerSize, startY + height - cornerSize, width - (cornerSize * 2), cornerSize),
    new Rectangle(cornerSize, texture.Height - cornerSize, edgeWidth, cornerSize),
    color);

            Main.spriteBatch.Draw(texture, new Vector2(startX + width - cornerSize, startY + height - cornerSize),
    new Rectangle(texture.Width - cornerSize, texture.Height - cornerSize, cornerSize, cornerSize),
    color);
        }

        public override void Unload()
        {
        }

    }
}
