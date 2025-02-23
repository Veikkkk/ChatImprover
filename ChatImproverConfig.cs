using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader.Config;
using tModPorter;

public class ChatImproverConfig : ModConfig
{
    static ChatImproverConfig instance;

    ChatImproverConfig()
    {
        instance = this;
    }
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Header("CommonConfig")]
    [DefaultValue(true)]
    public bool isMouseScrollingEnabled { get; set; }

    [DefaultValue(true)]
    public bool isCaretMovable { get; set; }

    [DefaultValue(true)]
    public bool isPageNavigationEnabled { get; set; }

    [DefaultValue(true)]
    public bool isImeDeleteFixEnabled { get; set; }


    [Header("NameFormat")]
    [DefaultValue("")]
    public string LeftSymbol { get; set; }

    [DefaultValue(":")]
    public string RightSymbol { get; set; }

    [DefaultValue("#a364ff")]
    public string nameColor { get; set; }

    [Header("BetaConfig")]
    [Range(10, 30)]
    [DefaultValue(15)]
    [Slider]
    public int showCount { get; set; }

    public static int GetshowCount()
    {
        return instance.showCount;
    }
    public static string GetnameColor()
    {
        string color16 = instance.nameColor.Replace("#", "");
        if (color16.Length == 6 && color16.All(c => "0123456789abcdef".Contains(char.ToLower(c))))
        {
            return instance.nameColor;
        }
        else
        {
            instance.nameColor = "#a364ff";
            return instance.nameColor;
        }

    }

    public static string GetLeftSymbol()
    {
        return instance.LeftSymbol;
    }

    public static string GetRightSymbol()
    {
        return instance.RightSymbol;
    }


    public static bool GetIsMouseScrollingEnabled()
    {
        return instance.isMouseScrollingEnabled;
    }

    public static bool GetIsCaretMovable()
    {
        return instance.isCaretMovable;
    }

    public static bool GetIsPageNavigationEnabled()
    {
        return instance.isPageNavigationEnabled;
    }

    public static bool GetIsImeDeleteFixEnabled()
    {
        return instance.isImeDeleteFixEnabled;
    }
}

