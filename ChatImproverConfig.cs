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

    [DefaultValue(true)]
    public bool isMouseScrollingEnabled { get; set; }

    [DefaultValue(true)]
    public bool isCaretMovable { get; set; }

    [DefaultValue(true)]
    public bool isPageNavigationEnabled { get; set; }

    [DefaultValue(true)]
    public bool isImeDeleteFixEnabled { get; set; }


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

